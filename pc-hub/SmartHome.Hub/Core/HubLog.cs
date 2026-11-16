namespace SmartHome.Hub.Core;

public sealed record LogEntry(DateTime Time, string Kind, string Text);

/// <summary>
/// 통신/명령 로그. 최근 300개는 메모리에, 전체는 logs/hub-날짜.log 파일에 남겨요.
/// Kind: SYS, NET, TX, RX, CMD, FAIL, WARN, EVT, ERROR
/// </summary>
public sealed class HubLog
{
    const int MaxRecent = 300;

    readonly object _gate = new();
    readonly Queue<LogEntry> _recent = new();
    readonly string _dir;

    public event Action<LogEntry>? Written;

    public HubLog(string dir)
    {
        _dir = dir;
        Directory.CreateDirectory(dir);
    }

    public void Write(string kind, string text)
    {
        var entry = new LogEntry(DateTime.Now, kind, text);
        lock (_gate)
        {
            _recent.Enqueue(entry);
            while (_recent.Count > MaxRecent) _recent.Dequeue();
            try
            {
                File.AppendAllText(
                    Path.Combine(_dir, $"hub-{entry.Time:yyyyMMdd}.log"),
                    $"{entry.Time:HH:mm:ss.fff} [{kind}] {text}{Environment.NewLine}");
            }
            catch (IOException) { /* 로그 파일 문제로 허브가 멈추면 안 돼요 */ }
        }
        Written?.Invoke(entry);
    }

    public IReadOnlyList<LogEntry> Recent()
    {
        lock (_gate) return _recent.ToList();
    }
}
