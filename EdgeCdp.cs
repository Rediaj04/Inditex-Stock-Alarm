using System.Diagnostics;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace InditexStockAlarm;

public sealed class EdgeCdp
{
    public const string HeadlessArgs =
        "--headless=new --mute-audio --window-size=1400,1000 --user-agent=\"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36\"";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    private readonly string _edgePath;
    private readonly string _profileDir;
    private readonly int _port;
    private Process? _proc;

    public EdgeCdp(string edgePath, string profileDir, int port = 9333)
    {
        _edgePath = edgePath;
        _profileDir = profileDir;
        _port = port;
        Name = BrowserName(edgePath);
    }

    public string Name { get; }

    public string BaseUrl => $"http://127.0.0.1:{_port}";

    public static string? FindBrowser()
    {
        var candidates = new[]
        {
            // Edge
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "Edge", "Application", "msedge.exe"),
            // Chrome
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Google", "Chrome", "Application", "chrome.exe"),
            // Brave
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "BraveSoftware", "Brave-Browser", "Application", "brave.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "BraveSoftware", "Brave-Browser", "Application", "brave.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BraveSoftware", "Brave-Browser", "Application", "brave.exe")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate)) return candidate;
        }

        foreach (var exe in new[] { "msedge.exe", "chrome.exe", "brave.exe" })
        {
            var found = FromAppPaths(exe);
            if (found is not null) return found;
        }

        return null;
    }

    public static string BrowserName(string path)
    {
        var name = Path.GetFileName(path).ToLowerInvariant();
        if (name.Contains("edge")) return "Edge";
        if (name.Contains("chrome")) return "Chrome";
        if (name.Contains("brave")) return "Brave";
        return "navegador";
    }

    private static string? FromAppPaths(string exeName)
    {
        try
        {
            foreach (var root in new[] { Microsoft.Win32.Registry.LocalMachine, Microsoft.Win32.Registry.CurrentUser })
            {
                using var key = root.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + exeName);
                if (key?.GetValue(null) is string path && !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    return path;
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }

    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        try
        {
            using var r = await Http.GetAsync(BaseUrl + "/json/version", ct);
            return r.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task StartAsync(string startUrl, CancellationToken ct, string? extraArgs = null)
    {
        if (await PingAsync(ct)) return;

        Directory.CreateDirectory(_profileDir);
        var args = new StringBuilder()
            .Append("--remote-debugging-port=").Append(_port)
            .Append(" --remote-allow-origins=*")
            .Append(" --user-data-dir=\"").Append(_profileDir).Append('"')
            .Append(" --no-first-run --no-default-browser-check")
            .Append(" --disable-features=msEdgeFirstRunExperience,msEdgeSidebarV2")
            .Append(" --disable-backgrounding-occluded-windows")
            .Append(" --disable-renderer-backgrounding")
            .Append(" --start-maximized")
            .Append(' ').Append(extraArgs ?? "")
            .Append(" --new-window \"").Append(startUrl).Append('"')
            .ToString();

        _proc = Process.Start(new ProcessStartInfo(_edgePath, args)
        {
            UseShellExecute = false
        });

        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(50))
        {
            await Task.Delay(700, ct);
            if (await PingAsync(ct)) return;
        }

        throw new InvalidOperationException(
            "El navegador no ha abierto el puerto de depuración. Prueba a cerrar las ventanas de Edge de este perfil o revisa el antivirus.");
    }

    private async Task<List<TargetInfo>> GetTargetsAsync(CancellationToken ct)
    {
        var json = await Http.GetStringAsync(BaseUrl + "/json/list", ct);
        using var doc = JsonDocument.Parse(json);
        var list = new List<TargetInfo>();
        foreach (var e in doc.RootElement.EnumerateArray())
        {
            if (!e.TryGetProperty("type", out var typeEl) || typeEl.GetString() != "page") continue;
            if (!e.TryGetProperty("webSocketDebuggerUrl", out var wsEl)) continue;
            var ws = wsEl.GetString();
            if (string.IsNullOrEmpty(ws)) continue;
            var url = e.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
            var id = e.TryGetProperty("id", out var i) ? i.GetString() ?? "" : "";
            list.Add(new TargetInfo(id, url, ws));
        }
        return list;
    }

    private static readonly string[] InditexHosts =
    {
        "bershka.com", "zara.com", "pullandbear.com", "stradivarius.com",
        "oysho.com", "massimodutti.com", "uterque.com", "zarahome.com"
    };

    private static bool IsInditexUrl(string url) =>
        InditexHosts.Any(h => url.Contains(h, StringComparison.OrdinalIgnoreCase));

    public async Task<CdpPage> GetMonitorPageAsync(string preferredUrl, CancellationToken ct)
    {
        var list = await GetTargetsAsync(ct);
        var page = list.FirstOrDefault(x => IsInditexUrl(x.Url));
        page ??= list.FirstOrDefault(x => !x.Url.StartsWith("devtools://", StringComparison.OrdinalIgnoreCase)
                                          && !x.Url.StartsWith("edge://", StringComparison.OrdinalIgnoreCase));
        page ??= list.FirstOrDefault();

        if (page is null)
            return await NewTabAsync(preferredUrl, ct);

        var cdpPage = await CdpPage.ConnectAsync(page.WebSocketUrl, ct);
        if (!IsInditexUrl(page.Url))
            await cdpPage.NavigateAsync(preferredUrl, ct);
        return cdpPage;
    }

    public async Task<CdpPage> NewTabAsync(string url, CancellationToken ct)
    {
        var endpoint = BaseUrl + "/json/new?" + Uri.EscapeDataString(url);
        HttpResponseMessage resp;
        try
        {
            resp = await Http.PutAsync(endpoint, null, ct);
        }
        catch
        {
            resp = await Http.GetAsync(endpoint, ct);
        }

        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"No se pudo crear la pestaña ({resp.StatusCode}).");

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var ws = doc.RootElement.TryGetProperty("webSocketDebuggerUrl", out var w) ? w.GetString() : null;
        if (string.IsNullOrEmpty(ws))
            throw new InvalidOperationException("La pestaña nueva no expone CDP.");

        var page = await CdpPage.ConnectAsync(ws, ct);
        page.Url = url;
        return page;
    }

    public void KillBrowser()
    {
        try
        {
            if (_proc is { HasExited: false }) _proc.Kill(true);
        }
        catch
        {
            // ignored
        }
    }

    private sealed record TargetInfo(string Id, string Url, string WebSocketUrl);
}

public sealed class CdpPage : IAsyncDisposable
{
    private readonly ClientWebSocket _ws;
    private readonly CancellationTokenSource _cts = new();
    private readonly Dictionary<int, TaskCompletionSource<string>> _pending = new();
    private int _nextId;

    private CdpPage(ClientWebSocket ws)
    {
        _ws = ws;
        _ = Task.Run(ReceiveLoopAsync);
    }

    public string Url { get; set; } = "";
    public bool IsOpen => _ws.State == WebSocketState.Open;

    public static async Task<CdpPage> ConnectAsync(string wsUrl, CancellationToken ct)
    {
        var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri(wsUrl), ct);
        return new CdpPage(ws);
    }

    private async Task ReceiveLoopAsync()
    {
        var buffer = new byte[1 << 16];
        try
        {
            while (_ws.State == WebSocketState.Open && !_cts.IsCancellationRequested)
            {
                var sb = new StringBuilder();
                WebSocketReceiveResult res;
                do
                {
                    res = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                    if (res.MessageType == WebSocketMessageType.Close) return;
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, res.Count));
                } while (!res.EndOfMessage);

                HandleMessage(sb.ToString());
            }
        }
        catch
        {
            // ignored
        }
        finally
        {
            foreach (var tcs in _pending.Values)
                tcs.TrySetException(new InvalidOperationException("Se perdió la conexión con la página."));
            _pending.Clear();
        }
    }

    private void HandleMessage(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (!doc.RootElement.TryGetProperty("id", out var idEl) || !idEl.TryGetInt32(out var id)) return;
            if (!_pending.Remove(id, out var tcs)) return;
            tcs.TrySetResult(text);
        }
        catch
        {
            // ignored
        }
    }

    private async Task<string> SendAsync(string method, string paramsJson, int timeoutMs, CancellationToken ct)
    {
        if (_ws.State != WebSocketState.Open)
            throw new InvalidOperationException("La página no está conectada.");

        var id = Interlocked.Increment(ref _nextId);
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;

        var payload = $"{{\"id\":{id},\"method\":\"{method}\",\"params\":{paramsJson}}}";
        await _ws.SendAsync(Encoding.UTF8.GetBytes(payload), WebSocketMessageType.Text, true, ct);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs, timeoutCts.Token));
        timeoutCts.Cancel();

        if (completed != tcs.Task)
        {
            _pending.Remove(id);
            throw new TimeoutException($"{method}: sin respuesta en {timeoutMs / 1000} s.");
        }

        return await tcs.Task;
    }

    public async Task<JsonElement?> EvaluateAsync(string expression, int timeoutMs = 30000, CancellationToken ct = default)
    {
        var p = new JsonObject
        {
            ["expression"] = expression,
            ["awaitPromise"] = true,
            ["returnByValue"] = true,
            ["userGesture"] = true
        }.ToJsonString();

        var resp = await SendAsync("Runtime.evaluate", p, timeoutMs, ct);
        using var doc = JsonDocument.Parse(resp);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var err))
            throw new InvalidOperationException("CDP: " + err.GetRawText());

        var result = root.GetProperty("result");
        if (result.TryGetProperty("exceptionDetails", out var details))
        {
            var text = details.TryGetProperty("exception", out var ex) && ex.TryGetProperty("description", out var d)
                ? d.GetString()
                : details.GetRawText();
            throw new InvalidOperationException("JS: " + text);
        }

        if (!result.TryGetProperty("result", out var inner)) return null;
        if (!inner.TryGetProperty("value", out var value)) return null;
        return value.Clone();
    }

    public async Task NavigateAsync(string url, CancellationToken ct)
    {
        var p = new JsonObject { ["url"] = url }.ToJsonString();
        await SendAsync("Page.navigate", p, 20000, ct);
        Url = url;
    }

    public async Task<bool> WaitForAsync(string boolExpression, int timeoutMs, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var value = await EvaluateAsync(boolExpression, 10000, ct);
                if (value is { ValueKind: JsonValueKind.True }) return true;
            }
            catch
            {
                // seguimos intentando
            }
            await Task.Delay(600, ct);
        }
        return false;
    }

    public async Task BringToFrontAsync(CancellationToken ct)
    {
        try
        {
            await SendAsync("Page.bringToFront", "{}", 5000, ct);
        }
        catch
        {
            // ignored
        }
    }

    public async Task ReloadAsync(CancellationToken ct)
    {
        await SendAsync("Page.reload", "{\"ignoreCache\":false}", 20000, ct);
    }

    public async Task EnableFastModeAsync(CancellationToken ct)
    {
        try
        {
            await SendAsync("Network.enable", "{}", 6000, ct);
            const string blocked =
                "{\"urls\":[\"*.png\",\"*.jpg\",\"*.jpeg\",\"*.webp\",\"*.gif\",\"*.svg\",\"*.woff\",\"*.woff2\",\"*.ttf\",\"*.mp4\",\"*.webm\"]}";
            await SendAsync("Network.setBlockedURLs", blocked, 6000, ct);
        }
        catch
        {
            // opcional
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _cts.Cancel();
            if (_ws.State == WebSocketState.Open)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await _ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", cts.Token);
            }
        }
        catch
        {
            // ignored
        }

        _ws.Dispose();
        _cts.Dispose();
    }
}
