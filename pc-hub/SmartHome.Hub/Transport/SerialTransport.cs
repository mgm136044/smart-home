using System.IO.Ports;
using System.Text;
using SmartHome.Hub.Core;

namespace SmartHome.Hub.Transport;

/// <summary>
/// USB 시리얼 백업 통로. hubsettings.json의 SerialPort(예: COM4)가 비어 있으면 사용하지 않아요.
/// 포트를 못 열거나 케이블이 빠지면 3초마다 다시 시도해요.
/// </summary>
public sealed class SerialTransport : ITransport
{
    readonly string _portName;
    readonly int _baud;
    readonly HubLog _log;
    readonly SemaphoreSlim _gate = new(1, 1);
    volatile SerialPort? _port;

    public SerialTransport(string portName, int baud, HubLog log)
    {
        _portName = portName;
        _baud = baud;
        _log = log;
    }

    public string Name => $"USB {_portName}";
    public bool IsConnected => _port is { IsOpen: true };

    public event Action<string>? LineReceived;
    public event Action? ConnectionChanged;

    public async Task RunAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_portName)) return;

        var warned = false;
        while (!ct.IsCancellationRequested)
        {
            SerialPort? port = null;
            try
            {
                port = new SerialPort(_portName, _baud)
                {
                    NewLine = "\n",
                    Encoding = Encoding.UTF8,
                    DtrEnable = false, // 포트를 열 때 ESP32가 재부팅되는 것 방지
                    RtsEnable = false,
                    ReadTimeout = 500,
                    WriteTimeout = 1000,
                };
                port.Open();
                warned = false;
                _port = port;
                _log.Write("NET", $"USB 시리얼 연결: {_portName} ({_baud}bps)");
                ConnectionChanged?.Invoke();
                await Task.Run(() => ReadLoop(port, ct), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
            {
                if (_port is null && !warned)
                {
                    _log.Write("WARN", $"{_portName} 열기 실패: {ex.Message} (3초마다 다시 시도)");
                    warned = true;
                }
            }
            finally
            {
                if (_port is not null)
                {
                    await _gate.WaitAsync(CancellationToken.None);
                    _port = null;
                    _gate.Release();
                    _log.Write("NET", $"USB 시리얼 끊김: {_portName}");
                    ConnectionChanged?.Invoke();
                }
                port?.Dispose();
            }

            try { await Task.Delay(3000, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    public async Task SendLineAsync(string line, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var port = _port;
            if (port is not { IsOpen: true }) throw new InvalidOperationException($"{_portName} 포트가 열려 있지 않아요");
            port.WriteLine(line);
        }
        finally
        {
            _gate.Release();
        }
    }

    void ReadLoop(SerialPort port, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            string line;
            try { line = port.ReadLine(); }
            catch (TimeoutException) { continue; }

            line = line.TrimEnd('\r');
            if (line.Length > 0) LineReceived?.Invoke(line);
        }
        ct.ThrowIfCancellationRequested();
    }
}
