using SmartHome.Hub.Core;
using SmartHome.Hub.UI;

namespace SmartHome.Hub;

internal static class Program
{
    /// <summary>
    /// 실행 옵션
    ///   --fake      가상 장치 켜기          --no-fake   가상 장치 끄기
    ///   --local     이 PC에서만 접속 (127.0.0.1, 방화벽 창이 안 떠요)
    ///   --headless  창 없이 서버만 실행 (테스트용)
    /// 설정 파일: hubsettings.local.json이 있으면 그걸, 없으면 hubsettings.json을 읽어요.
    /// </summary>
    [STAThread]
    static int Main(string[] args)
    {
        var baseDir = AppContext.BaseDirectory;
        var localSettings = Path.Combine(baseDir, "hubsettings.local.json");
        var settings = HubSettings.Load(File.Exists(localSettings) ? localSettings : Path.Combine(baseDir, "hubsettings.json"));
        if (args.Contains("--fake")) settings.UseFakeDevice = true;
        if (args.Contains("--no-fake")) settings.UseFakeDevice = false;
        if (args.Contains("--local")) settings.ListenAddress = "127.0.0.1";
        var headless = args.Contains("--headless");

        var host = new HubHost(settings, Path.Combine(baseDir, "logs"));
        try
        {
            Task.Run(host.StartAsync).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            var msg = $"허브를 시작하지 못했어요.\n{ex.Message}\n\n포트 {settings.WebPort} 또는 {settings.TcpPort}를 다른 프로그램이 쓰고 있는지 확인하세요.";
            host.Log.Write("ERROR", msg.Replace('\n', ' '));
            if (!headless) MessageBox.Show(msg, "SmartHome Hub", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }

        if (headless)
        {
            using var exit = new ManualResetEventSlim();
            AppDomain.CurrentDomain.ProcessExit += (_, _) => exit.Set();
            exit.Wait();
        }
        else
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm(host));
        }

        // UI 스레드가 끝난 뒤라 스레드풀에서 정리 (교착 방지)
        Task.Run(host.StopAsync).Wait(TimeSpan.FromSeconds(5));
        return 0;
    }
}
