// ESP32 시뮬레이터: 실제 ESP32처럼 허브(PC:5001)에 Wi-Fi TCP로 접속해서
// HELLO → 명령에 ACK → 1초마다 센서값 → PING에 STATE 로 응답해요.
//
// 사용법:
//   dotnet run --project tools/Esp32Sim -- [--host 127.0.0.1] [--port 5001] [--token change-me]
//                                         [--id esp32-sim] [--delay 30] [--mute] [--verbose]
//   --mute     명령에 ACK를 안 보냄 (허브의 타임아웃/재시도 테스트용)
//   --verbose  센서값, PING까지 전부 출력

using System.Net.Sockets;
using System.Text;
using SmartHome.Hub.Core;

Console.OutputEncoding = Encoding.UTF8;

string Arg(string name, string fallback)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
}

var host = Arg("--host", "127.0.0.1");
var port = int.Parse(Arg("--port", "5001"));
var token = Arg("--token", "change-me");
var deviceId = Arg("--id", "esp32-sim");
var delayMs = int.Parse(Arg("--delay", "30"));
var mute = args.Contains("--mute");
var verbose = args.Contains("--verbose");
var sim = new DeviceSimulator();

Console.WriteLine($"ESP32 시뮬레이터 · {host}:{port} 에 접속해요 (Ctrl+C로 종료){(mute ? " · ACK 안 보냄" : "")}");

while (true)
{
    try
    {
        using var client = new TcpClient { NoDelay = true };
        await client.ConnectAsync(host, port);
        var stream = client.GetStream();
        var reader = new StreamReader(stream, Encoding.UTF8);
        var writer = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\n", AutoFlush = true };
        var sendLock = new SemaphoreSlim(1, 1);

        async Task Send(string line)
        {
            await sendLock.WaitAsync();
            try { await writer.WriteLineAsync(line); }
            finally { sendLock.Release(); }
            if (verbose || !(line.StartsWith("#SENS") || line.StartsWith("#STATE"))) Console.WriteLine($"→ {line}");
        }

        await Send($"#HELLO,{deviceId},{token}");
        Console.WriteLine("허브에 접속했어요.");

        using var stop = new CancellationTokenSource();
        var sensorTask = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            try
            {
                while (await timer.WaitForNextTickAsync(stop.Token)) await Send(sim.NextSensorLine());
            }
            catch (Exception) { /* 연결이 끊기면 종료 */ }
        });

        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            var isPing = line == Protocol.Ping;
            if (verbose || !isPing) Console.WriteLine($"← {line}");
            if (mute && !isPing) continue;
            if (!isPing) await Task.Delay(delayMs);
            foreach (var reply in sim.Handle(line)) await Send(reply);
        }

        stop.Cancel();
        await sensorTask;
        Console.WriteLine("허브가 연결을 닫았어요.");
    }
    catch (Exception ex) when (ex is SocketException or IOException)
    {
        Console.WriteLine($"연결 실패/끊김: {ex.Message}");
    }

    Console.WriteLine("3초 뒤 다시 접속해요...");
    await Task.Delay(3000);
}
