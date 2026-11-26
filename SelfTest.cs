using System.Text;

namespace InditexStockAlarm;

internal static class SelfTest
{
    public static async Task<int> RunAsync(string[] args)
    {
        var sb = new StringBuilder();
        EdgeCdp? cdp = null;

        try
        {
            var headless = args.Any(a => a.Equals("--headless", StringComparison.OrdinalIgnoreCase));
            var edge = EdgeCdp.FindEdge() ?? throw new Exception("msedge.exe no encontrado");
            var profile = Path.Combine(Path.GetTempPath(), "bsk-selftest-profile");
            cdp = new EdgeCdp(edge, profile, 9444);

            var url = args.FirstOrDefault(a => a.StartsWith("--url=", StringComparison.OrdinalIgnoreCase))?[6..]
                      ?? "https://www.bershka.com/es/jeans-barrel-c0p229733871.html?colorId=428";
            var size = args.FirstOrDefault(a => a.StartsWith("--size=", StringComparison.OrdinalIgnoreCase))?[7..]
                       ?? "44";

            var extra = headless
                ? "--headless=new --mute-audio --user-agent=\"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36\""
                : "--window-position=-2400,-2400 --window-size=1000,800";

            await cdp.StartAsync(url, CancellationToken.None, extra);
            sb.AppendLine("edge=ok");

            var page = await cdp.GetMonitorPageAsync(url, CancellationToken.None);
            sb.AppendLine("page=ok");

            var ready = await page.WaitForAsync(MonitorScripts.ReadyScript(), 45000, CancellationToken.None);
            sb.AppendLine("ready=" + ready);

            await Task.Delay(4000);

            for (var i = 1; i <= 2; i++)
            {
                var value = await page.EvaluateAsync(
                    MonitorScripts.SizeScript(new[] { size }), 40000, CancellationToken.None);
                sb.AppendLine($"check{i}=" + (value?.GetString() ?? "<null>"));
                await Task.Delay(3000);
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine("ERROR: " + ex);
        }
        finally
        {
            cdp?.KillBrowser();
        }

        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "selftest.txt"), sb.ToString());
        return 0;
    }
}
