using System.Text.Json;

namespace SmartHome.Hub.Core;

/// <summary>hubsettings.json (또는 hubsettings.local.json) 설정값</summary>
public sealed class HubSettings
{
    public int WebPort { get; set; } = 5000;
    public int TcpPort { get; set; } = 5001;
    public string ListenAddress { get; set; } = "0.0.0.0";
    public string Token { get; set; } = "change-me";
    public string SerialPort { get; set; } = "";
    public int SerialBaud { get; set; } = 115200;
    public bool UseFakeDevice { get; set; } = true;
    public int CommandTimeoutMs { get; set; } = 2000;
    public int CommandRetries { get; set; } = 1;

    /// <summary>라즈베리파이 제스처 앱 미리보기 주소 (예: http://192.168.137.44:8080). 비우면 대시보드에 카메라 칸이 안 나와요.</summary>
    public string CameraUrl { get; set; } = "";

    public static HubSettings Load(string path)
    {
        if (!File.Exists(path)) return new HubSettings();
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        return JsonSerializer.Deserialize<HubSettings>(File.ReadAllText(path), options) ?? new HubSettings();
    }
}
