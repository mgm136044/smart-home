using System.Globalization;

namespace SmartHome.Hub.Core;

/// <summary>
/// ESP32 + Arduino 동작을 흉내 내는 가상 장치.
/// 허브의 "가상 장치" 모드와 tools/Esp32Sim 콘솔 앱이 같은 코드를 써요.
/// </summary>
public sealed class DeviceSimulator
{
    readonly object _gate = new();
    readonly Random _rnd = new();
    bool _fan, _light, _door;
    int _curtain;
    double _temp = 24.0, _hum = 45.0, _lux = 420;

    /// <summary>PC가 보낸 명령 한 줄을 처리하고, 장치가 돌려줄 응답 줄들을 반환해요.</summary>
    public IReadOnlyList<string> Handle(string line)
    {
        var cmd = Protocol.ParseCommand(line);
        if (cmd is null) return [];

        lock (_gate)
        {
            if (cmd.Device == "PING") return [StateLine()];

            switch (cmd.Device, cmd.Action)
            {
                case (Devices.Fan, "ON"): _fan = true; break;
                case (Devices.Fan, "OFF"): _fan = false; break;
                case (Devices.Light, "ON"): _light = true; break;
                case (Devices.Light, "OFF"): _light = false; break;
                case (Devices.Curtain, "SET") when cmd.Value is >= 0 and <= 100: _curtain = cmd.Value.Value; break;
                case (Devices.Door, "OPEN"): _door = true; break;
                case (Devices.Door, "CLOSE"): _door = false; break;
                default: return [$"#ERR,{cmd.Id},{cmd.Device},BAD_COMMAND"];
            }
            return [$"#ACK,{cmd.Id},{cmd.Device},{StateOf(cmd.Device)}"];
        }
    }

    public string StateLine()
    {
        lock (_gate)
            return string.Create(CultureInfo.InvariantCulture,
                $"#STATE,FAN={B(_fan)},LIGHT={B(_light)},CURTAIN={_curtain},DOOR={B(_door)}");
    }

    /// <summary>1초마다 호출: 센서값을 조금씩 바꿔서 SENS 줄을 만들어요. 팬을 켜면 온습도가 내려가요.</summary>
    public string NextSensorLine()
    {
        lock (_gate)
        {
            _temp = Math.Clamp(_temp + (_rnd.NextDouble() - 0.5) * 0.2 + (_fan ? -0.04 : 0.02), 18, 32);
            _hum = Math.Clamp(_hum + (_rnd.NextDouble() - 0.5) * 0.6 + (_fan ? -0.08 : 0.03), 30, 80);
            _lux = Math.Clamp(_lux + (_rnd.NextDouble() - 0.5) * 25 + (_light ? 2 : -1), 50, 1000);
            var ir = _rnd.NextDouble() < 0.05 ? 1 : 0;
            return string.Create(CultureInfo.InvariantCulture,
                $"#SENS,T={_temp:F1},H={_hum:F1},L={_lux:F0},IR={ir}");
        }
    }

    /// <summary>ESP32가 2초마다 보내는 상태 줄 흉내 (신호 세기는 -45~-60dBm 사이에서 흔들려요)</summary>
    public string StatusLine(TimeSpan uptime, bool arduino = true)
    {
        lock (_gate)
            return string.Create(CultureInfo.InvariantCulture,
                $"#STATUS,ARDUINO={B(arduino)},RSSI={_rnd.Next(-60, -44)},UP={(long)uptime.TotalSeconds}");
    }

    string StateOf(string device) => device switch
    {
        Devices.Fan => _fan ? "ON" : "OFF",
        Devices.Light => _light ? "ON" : "OFF",
        Devices.Curtain => _curtain.ToString(CultureInfo.InvariantCulture),
        Devices.Door => _door ? "OPEN" : "CLOSED",
        _ => "?",
    };

    static int B(bool b) => b ? 1 : 0;
}
