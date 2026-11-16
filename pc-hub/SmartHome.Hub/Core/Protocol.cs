using System.Globalization;

namespace SmartHome.Hub.Core;

/// <summary>장치 이름 (프로토콜에서 쓰는 대문자 이름)</summary>
public static class Devices
{
    public const string Fan = "FAN";
    public const string Light = "LIGHT";
    public const string Curtain = "CURTAIN";
    public const string Door = "DOOR";

    public static readonly string[] All = [Fan, Light, Curtain, Door];

    public static string? Normalize(string? name) => name?.Trim().ToUpperInvariant() switch
    {
        Fan => Fan,
        Light => Light,
        Curtain => Curtain,
        Door => Door,
        _ => null,
    };
}

/// <summary>PC → ESP32 명령: <c>@id,DEVICE,ACTION[,value]</c> 또는 <c>@id,PING</c></summary>
public sealed record CommandMessage(int Id, string Device, string Action, int? Value);

/// <summary>ESP32 → PC 메시지 (# 으로 시작)</summary>
public abstract record DeviceMessage;
public sealed record AckMessage(int Id, string Device, string State) : DeviceMessage;
public sealed record ErrMessage(int Id, string Device, string Reason) : DeviceMessage;
public sealed record SensMessage(IReadOnlyDictionary<string, double> Values) : DeviceMessage;
public sealed record StateMessage(IReadOnlyDictionary<string, string> Values) : DeviceMessage;
public sealed record EvtMessage(string Source, string Event) : DeviceMessage;
public sealed record HelloMessage(string DeviceId, string Token) : DeviceMessage;

/// <summary>ESP32 상태 (2초마다): <c>#STATUS,ARDUINO=1,RSSI=-55,UP=123</c></summary>
public sealed record StatusMessage(IReadOnlyDictionary<string, string> Values) : DeviceMessage;

/// <summary>
/// 줄 단위 프로토콜. 한 줄에 메시지 하나, '\n'으로 끝나요. TCP와 USB 시리얼이 똑같이 써요.
/// 자세한 규격은 docs/protocol.md 참고.
/// </summary>
public static class Protocol
{
    /// <summary>상태 재동기화 + 생존 확인용. id 0은 PING 전용이에요.</summary>
    public const string Ping = "@0,PING";

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>"//"로 시작하는 줄은 펌웨어 디버그 출력이라 무시해요 (USB 시리얼로 같이 들어와요).</summary>
    public static bool IsComment(string? line) => line is not null && line.TrimStart().StartsWith("//", StringComparison.Ordinal);

    public static string Command(int id, string device, string action, int? value = null) =>
        value is null
            ? string.Create(Inv, $"@{id},{device},{action}")
            : string.Create(Inv, $"@{id},{device},{action},{value.Value}");

    public static CommandMessage? ParseCommand(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        line = line.Trim();
        if (!line.StartsWith('@')) return null;

        var p = line[1..].Split(',');
        if (p.Length < 2 || !TryInt(p[0], out var id)) return null;

        var device = p[1].Trim().ToUpperInvariant();
        if (device == "PING") return new CommandMessage(id, "PING", "", null);
        if (p.Length < 3) return null;

        int? value = p.Length >= 4 && TryInt(p[3], out var v) ? v : null;
        return new CommandMessage(id, device, p[2].Trim().ToUpperInvariant(), value);
    }

    public static DeviceMessage? Parse(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        line = line.Trim();
        if (!line.StartsWith('#')) return null;

        var p = line[1..].Split(',');
        switch (p[0].Trim().ToUpperInvariant())
        {
            case "ACK" when p.Length >= 4 && TryInt(p[1], out var ackId):
                return new AckMessage(ackId, p[2].Trim().ToUpperInvariant(), p[3].Trim().ToUpperInvariant());

            case "ERR" when p.Length >= 4 && TryInt(p[1], out var errId):
                return new ErrMessage(errId, p[2].Trim().ToUpperInvariant(), p[3].Trim());

            case "SENS":
                var sens = new Dictionary<string, double>();
                foreach (var (key, raw) in Pairs(p))
                    if (double.TryParse(raw, NumberStyles.Float, Inv, out var num)) sens[key] = num;
                return new SensMessage(sens);

            case "STATE":
                return new StateMessage(Pairs(p).ToDictionary(x => x.Key, x => x.Value));

            case "EVT" when p.Length >= 3:
                return new EvtMessage(p[1].Trim().ToUpperInvariant(), p[2].Trim().ToUpperInvariant());

            case "HELLO" when p.Length >= 3:
                return new HelloMessage(p[1].Trim(), p[2].Trim());

            case "STATUS":
                return new StatusMessage(Pairs(p).ToDictionary(x => x.Key, x => x.Value));

            default:
                return null;
        }
    }

    static bool TryInt(string s, out int value) =>
        int.TryParse(s.Trim(), NumberStyles.Integer, Inv, out value);

    static IEnumerable<KeyValuePair<string, string>> Pairs(string[] parts)
    {
        for (var i = 1; i < parts.Length; i++)
        {
            var kv = parts[i].Split('=', 2);
            if (kv.Length == 2) yield return new(kv[0].Trim().ToUpperInvariant(), kv[1].Trim());
        }
    }
}
