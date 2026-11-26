namespace InditexStockAlarm;

internal static class AppPaths
{
    private static readonly Lazy<string> LazyDataDir = new(Resolve, isThreadSafe: true);

    public static string DataDir => LazyDataDir.Value;

    private static string Resolve()
    {
        var exeDir = AppContext.BaseDirectory;

        if (IsWritable(exeDir))
            return exeDir;

        var local = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "InditexStockAlarm");

        try
        {
            Directory.CreateDirectory(local);
        }
        catch
        {
            // último recurso: carpeta temporal del usuario
            local = Path.Combine(Path.GetTempPath(), "InditexStockAlarm");
            Directory.CreateDirectory(local);
        }

        return local;
    }

    private static bool IsWritable(string dir)
    {
        try
        {
            var probe = Path.Combine(dir, ".bsk-write-test-" + Guid.NewGuid().ToString("N") + ".tmp");
            File.WriteAllText(probe, "x");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
