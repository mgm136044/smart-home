using System.Net;
using System.Net.Sockets;
using System.Text;
using SmartHome.Hub.Core;

namespace SmartHome.Hub.Transport;

/// <summary>
/// ESP32가 Wi-Fi로 접속해 오는 TCP 서버 (기본 포트 5001). 한 번에 ESP32 하나만 받아요.
/// 접속 순서: ESP32가 "#HELLO,장치ID,토큰" → 토큰이 맞으면 연결 유지.
/// 5초마다 PING을 보내고, 6초 동안 아무 줄도 안 오면 끊긴 것으로 처리해요.
/// </summary>
public sealed class TcpTransport : ITransport
{
    static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(6);
    static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(5);

    readonly IPAddress _address;
    readonly int _port;
    readonly string _token;
    readonly HubLog _log;
    readonly SemaphoreSlim _gate = new(1, 1);
    volatile StreamWriter? _writer;
    string _peer = "";

    public TcpTransport(IPAddress address, int port, string token, HubLog log)
    {
        _address = address;
        _port = port;
        _token = token;
        _log = log;
    }

    public string Name => $"Wi-Fi TCP {_peer}";
    public bool IsConnected => _writer is not null;

    public event Action<string>? LineReceived;
    public event Action? ConnectionChanged;

    public Task RunAsync(CancellationToken ct) => Task.WhenAll(AcceptLoopAsync(ct), HeartbeatLoopAsync(ct));

    public async Task SendLineAsync(string line, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var writer = _writer ?? throw new InvalidOperationException("ESP32(Wi-Fi)와 연결되어 있지 않아요");
            await writer.WriteLineAsync(line.AsMemory(), ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    async Task AcceptLoopAsync(CancellationToken ct)
    {
        var listener = new TcpListener(_address, _port);
        listener.Start();
        _log.Write("NET", $"ESP32 접속 대기: {_address}:{_port}");
        try
        {
            while (!ct.IsCancellationRequested)
            {
                using var client = await listener.AcceptTcpClientAsync(ct);
                await HandleClientAsync(client, ct);
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        client.NoDelay = true;
        var ip = (client.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString() ?? "?";
        var stream = client.GetStream();
        var reader = new StreamReader(stream, Encoding.UTF8);
        using var idle = CancellationTokenSource.CreateLinkedTokenSource(ct);
        idle.CancelAfter(IdleTimeout);
        try
        {
            if (Protocol.Parse(await reader.ReadLineAsync(idle.Token)) is not HelloMessage hello || hello.Token != _token)
            {
                _log.Write("WARN", $"{ip}: 인증 실패 (HELLO 또는 토큰 확인), 연결 끊음");
                return;
            }

            _peer = $"{hello.DeviceId} {ip}";
            _writer = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\n", AutoFlush = true };
            _log.Write("NET", $"ESP32 접속: {_peer}");
            ConnectionChanged?.Invoke();

            string? line;
            while ((line = await reader.ReadLineAsync(idle.Token)) is not null)
            {
                idle.CancelAfter(IdleTimeout); // 줄이 올 때마다 타이머 초기화
                if (line.Length > 0) LineReceived?.Invoke(line);
            }
            _log.Write("NET", "ESP32가 연결을 닫았어요");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _log.Write("WARN", $"{ip}: {IdleTimeout.TotalSeconds:0}초 동안 응답 없음, 연결 끊음");
        }
        catch (IOException ex)
        {
            _log.Write("WARN", $"ESP32 연결 끊김: {ex.Message}");
        }
        finally
        {
            if (_writer is not null)
            {
                await _gate.WaitAsync(CancellationToken.None); // 보내는 중이면 끝날 때까지 기다림
                _writer = null;
                _gate.Release();
                ConnectionChanged?.Invoke();
            }
        }
    }

    async Task HeartbeatLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(HeartbeatInterval);
        while (await timer.WaitForNextTickAsync(ct))
        {
            if (!IsConnected) continue;
            try { await SendLineAsync(Protocol.Ping, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { }
        }
    }
}
