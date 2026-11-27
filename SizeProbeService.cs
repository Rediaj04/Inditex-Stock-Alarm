using System.Text.Json;

namespace InditexStockAlarm;

internal static class SizeProbeService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public static async Task<SizeProbeResult> ProbeAsync(EdgeCdp edge, string url, string? extraArgs = null)
    {
        try
        {
            await edge.StartAsync(url, CancellationToken.None, extraArgs ?? EdgeCdp.HeadlessArgs);

            var page = await edge.NewTabAsync(url, CancellationToken.None);
            try
            {
                await page.EnableFastModeAsync(CancellationToken.None);
                await page.WaitForAsync("document.readyState === 'complete'", 30000, CancellationToken.None);
                await Task.Delay(1500, CancellationToken.None);

                ItemUrl.TryParse(url, out var colorId, out _);

                // 1) Intento por API (funciona incluso si la web no pinta las tallas)
                string? apiReason = null;
                try
                {
                    var apiValue = await page.EvaluateAsync(
                        MonitorScripts.ResolveApiScript(colorId), 45000, CancellationToken.None);
                    var api = Parse(apiValue?.GetString());
                    if (api is { Ok: true } && api.Sizes.Count > 0)
                    {
                        return new SizeProbeResult
                        {
                            Ok = true,
                            Product = api.Product,
                            Sizes = api.Sizes
                                .Select(s => new SizeProbe { Size = s.Size, State = s.Visibility })
                                .ToList()
                        };
                    }

                    apiReason = api?.Reason ?? "sin-datos";
                }
                catch (Exception ex)
                {
                    apiReason = ex.Message;
                }

                // 2) Fallback: leer las tallas del DOM
                await page.WaitForAsync(MonitorScripts.ReadyScript(), 20000, CancellationToken.None);
                await Task.Delay(800, CancellationToken.None);

                var value = await page.EvaluateAsync(
                    MonitorScripts.ListSizesScript(), 45000, CancellationToken.None);
                var json = value?.GetString();
                if (string.IsNullOrEmpty(json))
                    return new SizeProbeResult { Reason = "respuesta vacía (api: " + apiReason + ")" };

                var dom = JsonSerializer.Deserialize<SizeProbeResult>(json, JsonOpts);
                if (dom is not null && !dom.Ok && apiReason is not null)
                    dom.Reason = (dom.Reason ?? "sin-tallas") + " | api: " + apiReason;

                return dom ?? new SizeProbeResult { Reason = "respuesta no válida" };
            }
            finally
            {
                try
                {
                    await page.DisposeAsync();
                }
                catch
                {
                    // ignored
                }
            }
        }
        catch (Exception ex)
        {
            return new SizeProbeResult { Reason = ex.Message };
        }
    }

    private static CheckPayload? Parse(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<CheckPayload>(json, JsonOpts);
        }
        catch
        {
            return null;
        }
    }
}
