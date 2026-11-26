using System.Text;

namespace BershkaMonitor;

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

            const string url = "https://www.bershka.com/es/jeans-barrel-c0p229733871.html?colorId=428";
            var extra = headless
                ? "--headless=new --mute-audio --user-agent=\"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36\""
                : "--window-position=-2400,-2400 --window-size=1000,800";

            await cdp.StartAsync(url, CancellationToken.None, extra);
            sb.AppendLine("edge=ok");

            var page = await cdp.GetMonitorPageAsync(url, CancellationToken.None);
            sb.AppendLine("page=ok");

            var ready = await page.WaitForAsync(MonitorScripts.ReadyScript(), 40000, CancellationToken.None);
            sb.AppendLine("ready=" + ready);

            await Task.Delay(5000);

            for (var i = 1; i <= 2; i++)
            {
                var value = await page.EvaluateAsync(
                    MonitorScripts.SizeScript(new[] { "44" }), 40000, CancellationToken.None);
                sb.AppendLine($"fetch{i}=" + (value?.GetString() ?? "<null>"));
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
