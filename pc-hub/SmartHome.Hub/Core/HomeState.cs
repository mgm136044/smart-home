using System.Globalization;

namespace SmartHome.Hub.Core;

public sealed record SensorSample(DateTime Time, double Temp, double Hum, double Lux, int Ir);

/// <summary>ESP32가 알려주는 상태: Wi-Fi 신호 세기(dBm), Arduino 응답 여부, 부팅 후 시간(초)</summary>
public sealed record EspStatus(int? Rssi, bool Arduino, long Uptime, bool Simulated);

public sealed record HomeSnapshot(
    bool Fan,
    bool Light,
    int Curtain,
    bool Door,
    SensorSample? Sensors,
    bool Connected,
    string Link,
    EspStatus? Esp);

/// <summary>장치·센서 상태의 단일 저장소. WinForms, 웹 대시보드, 라즈베리파이 모두 여기 값을 봐요.</summary>
public sealed class HomeState
{
    const int MaxHistory = 3600; // 1초 간격이면 1시간

    readonly object _gate = new();
    readonly Queue<SensorSample> _history = new();
    bool _fan, _light, _door;
    int _curtain;
    SensorSample? _last;
    bool _connected;
    string _link = "오프라인";
    EspStatus? _esp;

    /// <summary>장치 상태나 연결 상태가 바뀌었을 때</summary>
    public event Action<HomeSnapshot>? Changed;

    /// <summary>센서값이 들어왔을 때 (보통 1초마다)</summary>
    public event Action<SensorSample>? SensorUpdated;

    public HomeSnapshot Snapshot()
    {
        lock (_gate) return new HomeSnapshot(_fan, _light, _curtain, _door, _last, _connected, _link, _esp);
    }

    public IReadOnlyList<SensorSample> History(DateTime since)
    {
        lock (_gate) return _history.Where(s => s.Time >= since).ToList();
    }

    public void ApplyAck(AckMessage ack)
    {
        lock (_gate) ApplyOne(ack.Device, ack.State);
        Changed?.Invoke(Snapshot());
    }

    public void ApplyState(StateMessage msg)
    {
        bool changed;
        lock (_gate)
        {
            var before = (_fan, _light, _curtain, _door);
            foreach (var (key, value) in msg.Values) ApplyOne(key, value);
            changed = before != (_fan, _light, _curtain, _door);
        }
        if (changed) Changed?.Invoke(Snapshot());
    }

    public void ApplySensors(SensMessage msg)
    {
        SensorSample sample;
        lock (_gate)
        {
            var v = msg.Values;
            sample = new SensorSample(
                DateTime.Now,
                v.TryGetValue("T", out var t) ? t : _last?.Temp ?? 0,
                v.TryGetValue("H", out var h) ? h : _last?.Hum ?? 0,
                v.TryGetValue("L", out var l) ? l : _last?.Lux ?? 0,
                v.TryGetValue("IR", out var ir) ? (int)ir : _last?.Ir ?? 0);
            _last = sample;
            _history.Enqueue(sample);
            while (_history.Count > MaxHistory) _history.Dequeue();
        }
        SensorUpdated?.Invoke(sample);
    }

    public void SetLink(bool connected, string link)
    {
        lock (_gate)
        {
            if (_connected == connected && _link == link) return;
            if (_link != link) _esp = null; // 통로가 바뀌면 이전 ESP32 상태는 버려요
            _connected = connected;
            _link = link;
        }
        Changed?.Invoke(Snapshot());
    }

    /// <returns>Arduino 응답 여부가 바뀌었으면 새 값, 아니면 null</returns>
    public bool? ApplyStatus(StatusMessage msg)
    {
        bool notify;
        bool? arduinoChanged = null;
        lock (_gate)
        {
            var v = msg.Values;
            int? rssi = v.TryGetValue("RSSI", out var r) && int.TryParse(r, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rv) ? rv : null;
            var arduino = !v.TryGetValue("ARDUINO", out var a) || IsOn(a); // 키가 없으면 정상으로 봐요
            var up = v.TryGetValue("UP", out var u) && long.TryParse(u, NumberStyles.Integer, CultureInfo.InvariantCulture, out var uv) ? uv : 0;
            var sim = v.TryGetValue("SIM", out var s) && IsOn(s);
            var next = new EspStatus(rssi, arduino, up, sim);

            // 처음 받은 상태는 '정상'에서 바뀐 것으로 봐요 (처음부터 Arduino가 없으면 경고)
            if ((_esp?.Arduino ?? true) != arduino) arduinoChanged = arduino;
            // 신호는 5dBm 이상 바뀔 때만 화면에 알려요 (2초마다 오니까)
            notify = _esp is null || _esp.Arduino != arduino || _esp.Simulated != sim
                     || Math.Abs((_esp.Rssi ?? 0) - (rssi ?? 0)) >= 5;
            _esp = next;
        }
        if (notify) Changed?.Invoke(Snapshot());
        return arduinoChanged;
    }

    // FAN=1 / FAN=ON / CURTAIN=70 / DOOR=OPEN 형식을 모두 처리
    void ApplyOne(string key, string value)
    {
        switch (key.ToUpperInvariant())
        {
            case Devices.Fan: _fan = IsOn(value); break;
            case Devices.Light: _light = IsOn(value); break;
            case Devices.Door: _door = IsOn(value); break;
            case Devices.Curtain when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pos):
                _curtain = Math.Clamp(pos, 0, 100);
                break;
        }
    }

    static bool IsOn(string v) => v.Trim().ToUpperInvariant() is "1" or "ON" or "OPEN" or "TRUE";
}
