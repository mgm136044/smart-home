using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using SmartHome.Hub.Core;

namespace SmartHome.Hub.Web;

public sealed record DeviceRequest(string? Action, int? Value);

/// <summary>
/// WinForms 안에서 도는 웹 서버 (Kestrel).
/// - 정적 파일: wwwroot (휴대폰/아이패드 대시보드)
/// - REST: /api/state, /api/devices/{device}, /api/sensors, /api/logs
/// - WebSocket: /ws (state, sensor, log 이벤트를 실시간으로 보내요)
/// </summary>
public sealed class WebServer : IAsyncDisposable
{
    // 한글을 \uXXXX로 바꾸지 않고 그대로 보내요
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // 카메라 스트림은 끝이 없어서 전체 시간 제한을 끄고, 접속만 3초 안에 되는지 봐요
    static readonly HttpClient CameraHttp = new() { Timeout = Timeout.InfiniteTimeSpan };

    readonly ConcurrentDictionary<Guid, Client> _clients = new();
    WebApplication? _app;

    sealed class Client(WebSocket socket)
    {
        public WebSocket Socket { get; } = socket;
        public SemaphoreSlim Lock { get; } = new(1, 1); // WebSocket은 동시에 두 번 보내면 안 돼요
    }

    public async Task StartAsync(HubSettings settings, HomeState state, CommandService commands, HubLog log)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot"),
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls($"http://{settings.ListenAddress}:{settings.WebPort}");

        var app = builder.Build();
        app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });
        app.UseDefaultFiles();
        app.UseStaticFiles(new StaticFileOptions
        {
            // 개발 중에 휴대폰이 옛날 JS를 캐시하지 않게
            OnPrepareResponse = c => c.Context.Response.Headers.CacheControl = "no-cache",
        });

        app.MapGet("/api/state", () => state.Snapshot());

        app.MapPost("/api/devices/{device}", async (string device, DeviceRequest body, HttpContext http) =>
        {
            var source = http.Request.Headers["X-Source"].FirstOrDefault() ?? "web";
            var r = await commands.ExecuteAsync(device, body.Action ?? "", body.Value, source, http.RequestAborted);
            var status = r.Code switch
            {
                null => 200,
                "bad_request" => 400,
                "offline" => 503,
                "timeout" => 504,
                "device_error" => 502,
                _ => 500,
            };
            return Results.Json(r, Json, statusCode: status);
        });

        app.MapGet("/api/sensors", (int? minutes) =>
            state.History(DateTime.Now.AddMinutes(-Math.Clamp(minutes ?? 10, 1, 60))));

        app.MapGet("/api/logs", () => log.Recent());

        // 라즈베리파이 카메라 미리보기를 허브가 대신 받아서 전달해요.
        // 그래서 아이패드는 라즈베리파이 주소를 몰라도 허브 주소만으로 카메라를 볼 수 있어요.
        app.MapGet("/api/camera", () => new { enabled = !string.IsNullOrWhiteSpace(settings.CameraUrl), url = settings.CameraUrl });
        app.MapGet("/camera/stream", (HttpContext ctx) => ProxyCameraAsync(ctx, settings.CameraUrl, "/stream"));
        app.MapGet("/camera/snapshot.jpg", (HttpContext ctx) => ProxyCameraAsync(ctx, settings.CameraUrl, "/snapshot.jpg"));

        app.Map("/ws", (RequestDelegate)(ctx => HandleSocketAsync(ctx, state)));

        state.Changed += snap => Broadcast("state", snap);
        state.SensorUpdated += sample => Broadcast("sensor", sample);
        log.Written += entry => Broadcast("log", entry);

        await app.StartAsync();
        _app = app;
    }

    static async Task ProxyCameraAsync(HttpContext ctx, string cameraUrl, string path)
    {
        if (string.IsNullOrWhiteSpace(cameraUrl))
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        using var connect = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);
        connect.CancelAfter(TimeSpan.FromSeconds(3));
        HttpResponseMessage upstream;
        try
        {
            upstream = await CameraHttp.GetAsync(cameraUrl.TrimEnd('/') + path, HttpCompletionOption.ResponseHeadersRead, connect.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            ctx.Response.StatusCode = StatusCodes.Status502BadGateway; // 제스처 앱이 안 돌고 있거나 주소가 틀림
            return;
        }

        using (upstream)
        {
            ctx.Response.StatusCode = (int)upstream.StatusCode;
            ctx.Response.ContentType = upstream.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
            ctx.Response.Headers.CacheControl = "no-cache";
            ctx.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
            var buffer = new byte[32 * 1024];
            try
            {
                await using var source = await upstream.Content.ReadAsStreamAsync(ctx.RequestAborted);
                int n;
                while ((n = await source.ReadAsync(buffer, ctx.RequestAborted)) > 0)
                {
                    await ctx.Response.Body.WriteAsync(buffer.AsMemory(0, n), ctx.RequestAborted);
                    await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException or HttpRequestException)
            {
                // 브라우저를 닫았거나 제스처 앱이 멈춤
            }
        }
    }

    async Task HandleSocketAsync(HttpContext ctx, HomeState state)
    {
        if (!ctx.WebSockets.IsWebSocketRequest)
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var socket = await ctx.WebSockets.AcceptWebSocketAsync();
        var id = Guid.NewGuid();
        var client = new Client(socket);
        _clients[id] = client;
        try
        {
            await SendAsync(id, client, Serialize("state", state.Snapshot()));
            var buffer = new byte[1024];
            while (socket.State == WebSocketState.Open)
            {
                var r = await socket.ReceiveAsync(buffer.AsMemory(), ctx.RequestAborted);
                if (r.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                    break;
                }
            }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException) { }
        finally
        {
            _clients.TryRemove(id, out _);
        }
    }

    void Broadcast(string type, object data)
    {
        if (_clients.IsEmpty) return;
        var bytes = Serialize(type, data);
        foreach (var (id, client) in _clients) _ = SendAsync(id, client, bytes);
    }

    static byte[] Serialize(string type, object data) =>
        JsonSerializer.SerializeToUtf8Bytes(new { type, data }, Json);

    async Task SendAsync(Guid id, Client client, byte[] bytes)
    {
        await client.Lock.WaitAsync();
        try
        {
            if (client.Socket.State == WebSocketState.Open)
                await client.Socket.SendAsync(new ReadOnlyMemory<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch
        {
            _clients.TryRemove(id, out _);
        }
        finally
        {
            client.Lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is null) return;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await _app.StopAsync(cts.Token); } catch (OperationCanceledException) { }
        await _app.DisposeAsync();
        _app = null;
    }
}
