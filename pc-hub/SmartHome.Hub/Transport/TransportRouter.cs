using SmartHome.Hub.Core;

namespace SmartHome.Hub.Transport;

/// <summary>
/// 여러 통로를 우선순위대로 묶어요. 연결된 것 중 가장 앞의 것을 써요.
/// 기본 순서: Wi-Fi TCP → USB 시리얼 → 가상 장치.
/// 그래서 Wi-Fi가 끊기면 USB로, 둘 다 없으면 가상 장치로 자동으로 넘어가요.
/// </summary>
public sealed class TransportRouter : ITransport
{
    readonly ITransport[] _items;
    readonly HubLog _log;
    ITransport? _lastActive;

    public TransportRouter(HubLog log, params ITransport[] items)
    {
        _log = log;
        _items = items;
        foreach (var t in items)
        {
            // 지금 사용 중인 통로에서 온 줄만 위로 올려요 (센서값 중복 방지)
            t.LineReceived += line => { if (ReferenceEquals(Active, t)) LineReceived?.Invoke(line); };
            t.ConnectionChanged += OnChildChanged;
        }
        _lastActive = Active;
    }

    public ITransport? Active => _items.FirstOrDefault(t => t.IsConnected);
    public string Name => Active?.Name ?? "오프라인";
    public bool IsConnected => Active is not null;

    public event Action<string>? LineReceived;
    public event Action? ConnectionChanged;

    public Task RunAsync(CancellationToken ct) => Task.WhenAll(_items.Select(t => RunOne(t, ct)));

    public Task SendLineAsync(string line, CancellationToken ct = default) =>
        (Active ?? throw new InvalidOperationException("ESP32와 연결되어 있지 않아요")).SendLineAsync(line, ct);

    async Task RunOne(ITransport t, CancellationToken ct)
    {
        try { await t.RunAsync(ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) { _log.Write("ERROR", $"{t.Name} 중단: {ex.Message}"); }
    }

    void OnChildChanged()
    {
        var now = Active;
        if (!ReferenceEquals(now, _lastActive))
        {
            _lastActive = now;
            _log.Write("NET", now is null ? "연결된 장치 없음" : $"사용 중인 통로: {now.Name}");
            if (now is not null) _ = PingAsync(now); // 통로가 바뀌면 전체 상태를 다시 받아요
        }
        ConnectionChanged?.Invoke();
    }

    static async Task PingAsync(ITransport t)
    {
        try { await t.SendLineAsync(Protocol.Ping); }
        catch { /* 다음 하트비트에서 다시 시도 */ }
    }
}
