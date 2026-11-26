using System.Text.Json;

namespace InditexStockAlarm;

internal static class SizeProbeService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public static async Task<SizeProbeResult> ProbeAsync(EdgeCdp edge, string url)
    {
        try
        {
            await edge.StartAsync(url, CancellationToken.None, EdgeCdp.HeadlessArgs);

            var page = await edge.NewTabAsync(url, CancellationToken.None);
            try
            {
                await page.EnableFastModeAsync(CancellationToken.None);
                await page.WaitForAsync(MonitorScripts.ReadyScript(), 30000, CancellationToken.None);
                await Task.Delay(1200, CancellationToken.None);

                var value = await page.EvaluateAsync(
                    MonitorScripts.ListSizesScript(), 45000, CancellationToken.None);

                var json = value?.GetString();
                if (string.IsNullOrEmpty(json))
                    return new SizeProbeResult { Reason = "respuesta vacía" };

                return JsonSerializer.Deserialize<SizeProbeResult>(json, JsonOpts)
                       ?? new SizeProbeResult { Reason = "respuesta no válida" };
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
}
