using SmartHome.Hub.Core;

namespace SmartHome.Hub.Transport;

/// <summary>
/// 하드웨어 없이 개발할 때 쓰는 가상 장치. 명령에 ACK로 답하고 1초마다 센서값을 보내요.
/// 실제 ESP32(TCP/USB)가 연결되면 라우터가 그쪽을 우선 사용해요.
/// </summary>
public sealed class FakeTransport : ITransport
{
    readonly DeviceSimulator _sim = new();
    volatile bool _enabled;

    public FakeTransport(bool enabled) => _enabled = enabled;

    public string Name => "가상 장치";
    public bool IsConnected => _enabled;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            _enabled = value;
            ConnectionChanged?.Invoke();
        }
    }

    public event Action<string>? LineReceived;
    public event Action? ConnectionChanged;

    public async Task RunAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(ct))
            if (_enabled) LineReceived?.Invoke(_sim.NextSensorLine());
    }

    public async Task SendLineAsync(string line, CancellationToken ct = default)
    {
        if (!_enabled) throw new InvalidOperationException("가상 장치가 꺼져 있어요");
        await Task.Delay(Random.Shared.Next(20, 60), ct); // 실제 장치처럼 약간의 지연
        foreach (var reply in _sim.Handle(line)) LineReceived?.Invoke(reply);
    }
}
