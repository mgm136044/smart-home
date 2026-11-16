namespace SmartHome.Hub.Transport;

/// <summary>
/// ESP32와 줄 단위로 주고받는 통로. Wi-Fi TCP, USB 시리얼, 가상 장치가 모두 이 모양이에요.
/// 프로토콜은 그대로 두고 통로만 바꿔 끼울 수 있어요.
/// </summary>
public interface ITransport
{
    /// <summary>화면에 보여줄 이름 (예: "Wi-Fi TCP esp32-01 192.168.0.20")</summary>
    string Name { get; }

    bool IsConnected { get; }

    /// <summary>한 줄 받음. 백그라운드 스레드에서 호출돼요(UI는 BeginInvoke 필요).</summary>
    event Action<string>? LineReceived;

    event Action? ConnectionChanged;

    /// <summary>접속 대기, 재접속, 읽기 루프. 앱이 끝날 때까지 돌아요.</summary>
    Task RunAsync(CancellationToken ct);

    Task SendLineAsync(string line, CancellationToken ct = default);
}
