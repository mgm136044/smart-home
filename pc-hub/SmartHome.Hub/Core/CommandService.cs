using System.Collections.Concurrent;
using System.Diagnostics;
using SmartHome.Hub.Transport;

namespace SmartHome.Hub.Core;

/// <param name="Code">실패 종류: bad_request, offline, timeout, device_error, cancelled (성공이면 null)</param>
public sealed record CommandResult(bool Ok, string Device, string? State, string? Error, string? Code, long Ms, string Source);

/// <summary>
/// 모든 명령(휴대폰, 제스처, 자동화, WinForms 버튼)이 거치는 곳.
/// 명령마다 번호를 붙여 보내고, 같은 번호의 ACK가 오면 완료로 처리해요.
/// 응답이 없으면 같은 번호로 한 번 더 보내요(명령이 절대값이라 다시 보내도 안전).
/// </summary>
public sealed class CommandService
{
    sealed record Wire(string Action, int? Value);

    readonly ITransport _transport;
    readonly HomeState _state;
    readonly HubSettings _settings;
    readonly HubLog _log;
    readonly ConcurrentDictionary<int, TaskCompletionSource<DeviceMessage>> _pending = new();
    int _nextId;

    /// <summary>장치가 보낸 이벤트 (#EVT). 나중에 자동화 규칙이 구독해요.</summary>
    public event Action<EvtMessage>? EventReceived;

    public CommandService(ITransport transport, HomeState state, HubSettings settings, HubLog log)
    {
        _transport = transport;
        _state = state;
        _settings = settings;
        _log = log;
        _transport.LineReceived += OnLine;
        _transport.ConnectionChanged += () => _state.SetLink(_transport.IsConnected, _transport.Name);
        _state.SetLink(_transport.IsConnected, _transport.Name);
    }

    /// <param name="device">fan, light, curtain, door (대소문자 무관)</param>
    /// <param name="action">on, off, toggle, open, close, set</param>
    /// <param name="value">curtain set일 때 0~100</param>
    /// <param name="source">명령 출처 (phone, gesture, auto, winforms ...)</param>
    public async Task<CommandResult> ExecuteAsync(string device, string action, int? value, string source, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var dev = Devices.Normalize(device);
        if (dev is null) return Fail(device ?? "", "알 수 없는 장치예요", "bad_request");

        var act = (action ?? "").Trim().ToUpperInvariant();
        var wire = Resolve(dev, act, value);
        if (wire is null)
        {
            var why = dev == Devices.Curtain && act == "SET"
                ? "커튼 값은 0~100 사이여야 해요"
                : $"{dev}에서 지원하지 않는 동작이에요: {action}";
            return Fail(dev, why, "bad_request");
        }

        if (!_transport.IsConnected) return Fail(dev, "ESP32와 연결되어 있지 않아요", "offline");

        var id = NextId();
        var line = Protocol.Command(id, dev, wire.Action, wire.Value);
        try
        {
            for (var attempt = 0; attempt <= _settings.CommandRetries; attempt++)
            {
                var tcs = new TaskCompletionSource<DeviceMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
                _pending[id] = tcs;
                _log.Write("TX", attempt == 0 ? line : $"{line} (재시도 {attempt})");
                try
                {
                    await _transport.SendLineAsync(line, ct);
                    var reply = await tcs.Task.WaitAsync(TimeSpan.FromMilliseconds(_settings.CommandTimeoutMs), ct);

                    if (reply is AckMessage ack)
                    {
                        _state.ApplyAck(ack);
                        _log.Write("CMD", $"{source}: {dev} → {ack.State} ({sw.ElapsedMilliseconds}ms)");
                        return new CommandResult(true, dev, ack.State, null, null, sw.ElapsedMilliseconds, source);
                    }
                    if (reply is ErrMessage err) return Fail(dev, DescribeError(err.Reason), "device_error");
                }
                catch (TimeoutException)
                {
                    _log.Write("WARN", $"{line} 응답 없음 ({_settings.CommandTimeoutMs}ms)");
                }
            }
            return Fail(dev, "응답 시간 초과", "timeout");
        }
        catch (OperationCanceledException)
        {
            return Fail(dev, "요청이 취소됐어요", "cancelled");
        }
        catch (Exception ex)
        {
            return Fail(dev, $"전송 실패: {ex.Message}", "offline");
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }

        CommandResult Fail(string d, string error, string code)
        {
            _log.Write("FAIL", $"{source}: {d} {action} → {error}");
            return new CommandResult(false, d, null, error, code, sw.ElapsedMilliseconds, source);
        }
    }

    /// <summary>요청을 장치에 보낼 절대값 명령으로 바꿔요. 토글은 여기서 현재 상태를 보고 ON/OFF로 바뀌어요.</summary>
    Wire? Resolve(string device, string action, int? value)
    {
        var s = _state.Snapshot();
        return (device, action) switch
        {
            (Devices.Fan or Devices.Light, "ON" or "OFF") => new Wire(action, null),
            (Devices.Fan, "TOGGLE") => new Wire(s.Fan ? "OFF" : "ON", null),
            (Devices.Light, "TOGGLE") => new Wire(s.Light ? "OFF" : "ON", null),
            (Devices.Curtain, "OPEN") => new Wire("SET", 100),
            (Devices.Curtain, "CLOSE") => new Wire("SET", 0),
            (Devices.Curtain, "SET") when value is >= 0 and <= 100 => new Wire("SET", value),
            (Devices.Curtain, "TOGGLE") => new Wire("SET", s.Curtain >= 50 ? 0 : 100),
            (Devices.Door, "OPEN" or "CLOSE") => new Wire(action, null),
            (Devices.Door, "TOGGLE") => new Wire(s.Door ? "CLOSE" : "OPEN", null),
            _ => null,
        };
    }

    static string DescribeError(string reason) => reason.ToUpperInvariant() switch
    {
        "NO_ARDUINO" => "Arduino가 응답하지 않아요 (배선/전원 확인)",
        "BAD_COMMAND" => "장치가 모르는 명령이에요",
        "BAD_VALUE" => "값이 범위를 벗어났어요",
        "BUSY" => "장치가 아직 움직이는 중이에요",
        _ => $"장치 오류: {reason}",
    };

    int NextId()
    {
        var id = Interlocked.Increment(ref _nextId) % 10000;
        return id == 0 ? NextId() : id; // 0은 PING 전용
    }

    void OnLine(string line)
    {
        if (Protocol.IsComment(line)) return; // 펌웨어 디버그 출력

        switch (Protocol.Parse(line))
        {
            case AckMessage ack:
                _log.Write("RX", line);
                if (_pending.TryGetValue(ack.Id, out var waiting)) waiting.TrySetResult(ack);
                else _state.ApplyAck(ack); // 늦게 온 ACK나 장치가 스스로 바꾼 상태
                break;

            case ErrMessage err:
                _log.Write("RX", line);
                if (_pending.TryGetValue(err.Id, out var waitingErr)) waitingErr.TrySetResult(err);
                break;

            case SensMessage sens:
                _state.ApplySensors(sens); // 1초마다 와서 로그에는 안 남겨요
                break;

            case StateMessage st:
                _state.ApplyState(st); // PING 응답 (5초마다)
                break;

            case EvtMessage evt:
                _log.Write("EVT", line);
                EventReceived?.Invoke(evt);
                break;

            case StatusMessage status:
                var arduino = _state.ApplyStatus(status);
                if (arduino == false) _log.Write("WARN", "ESP32: Arduino가 응답하지 않아요 (배선/전원 확인)");
                if (arduino == true) _log.Write("NET", "ESP32: Arduino 연결됨");
                break;

            case HelloMessage:
                break;

            default:
                _log.Write("RX?", line);
                break;
        }
    }
}
