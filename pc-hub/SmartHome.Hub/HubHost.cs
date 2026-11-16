using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using SmartHome.Hub.Core;
using SmartHome.Hub.Transport;
using SmartHome.Hub.Web;

namespace SmartHome.Hub;

/// <summary>허브 구성요소를 만들고 시작/종료해요. WinForms 모드와 --headless 모드가 같이 써요.</summary>
public sealed class HubHost
{
    readonly CancellationTokenSource _cts = new();
    Task? _transportTask;

    public HubSettings Settings { get; }
    public HubLog Log { get; }
    public HomeState State { get; } = new();
    public FakeTransport Fake { get; }
    public TcpTransport Tcp { get; }
    public SerialTransport Serial { get; }
    public TransportRouter Router { get; }
    public CommandService Commands { get; }
    public WebServer Web { get; } = new();

    public HubHost(HubSettings settings, string logDir)
    {
        Settings = settings;
        Log = new HubLog(logDir);
        Tcp = new TcpTransport(IPAddress.Parse(settings.ListenAddress), settings.TcpPort, settings.Token, Log);
        Serial = new SerialTransport(settings.SerialPort, settings.SerialBaud, Log);
        Fake = new FakeTransport(settings.UseFakeDevice);
        Router = new TransportRouter(Log, Tcp, Serial, Fake); // 우선순위: Wi-Fi TCP → USB → 가상 장치
        Commands = new CommandService(Router, State, settings, Log);
    }

    public async Task StartAsync()
    {
        await Web.StartAsync(Settings, State, Commands, Log);
        _transportTask = Task.Run(() => Router.RunAsync(_cts.Token));
        Log.Write("SYS", $"허브 시작 · 대시보드 {string.Join(", ", DashboardUrls())}");
    }

    public async Task StopAsync()
    {
        _cts.Cancel();
        await Web.DisposeAsync();
        if (_transportTask is not null)
        {
            try { await _transportTask.WaitAsync(TimeSpan.FromSeconds(3)); }
            catch { /* 종료 중 */ }
        }
    }

    /// <summary>휴대폰에서 접속할 주소 목록 (이 PC의 IPv4 주소들)</summary>
    public IReadOnlyList<string> DashboardUrls()
    {
        if (Settings.ListenAddress is not ("0.0.0.0" or "*"))
            return [$"http://{Settings.ListenAddress}:{Settings.WebPort}"];

        var urls = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address))
            .Select(a => $"http://{a.Address}:{Settings.WebPort}")
            .Distinct()
            .ToList();
        return urls.Count > 0 ? urls : [$"http://localhost:{Settings.WebPort}"];
    }
}
