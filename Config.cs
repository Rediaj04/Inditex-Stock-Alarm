using System.Text.Json;
using System.Text.RegularExpressions;

namespace InditexStockAlarm;

public sealed class AppConfig
{
    public int IntervalSeconds { get; set; } = 15;
    public bool AllowFastInterval { get; set; }
    public bool SoundEnabled { get; set; } = true;
    public bool OpenInBrave { get; set; } = true;
    public string NtfyTopic { get; set; } = "";
    public string SoundMode { get; set; } = "both";
    public bool GentleMode { get; set; }
    public bool ScheduleEnabled { get; set; }
    public string ScheduleStart { get; set; } = "08:00";
    public string ScheduleEnd { get; set; } = "24:00";
    public string Theme { get; set; } = "dark";
    public List<ItemConfig> Items { get; set; } = new();

    private static readonly JsonSerializerOptions WriteOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private static readonly JsonSerializerOptions ReadOpts = new() { PropertyNameCaseInsensitive = true };

    public static string PathFor(string dir) => Path.Combine(dir, "config.json");

    public static AppConfig Load(string dir)
    {
        var path = PathFor(dir);
        if (!File.Exists(path)) return new AppConfig();

        try
        {
            var cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), ReadOpts);
            if (cfg is not null) return cfg;
        }
        catch
        {
            // Si el JSON está corrupto, guardamos una copia antes de continuar.
            try
            {
                File.Copy(path, path + ".bad", true);
            }
            catch
            {
                // ignored
            }
        }

        return new AppConfig();
    }

    public void Save(string dir)
    {
        try
        {
            File.WriteAllText(PathFor(dir), JsonSerializer.Serialize(this, WriteOpts));
        }
        catch
        {
            // si no se puede guardar, seguimos en memoria
        }
    }
}

public sealed class ItemConfig
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public string Sizes { get; set; } = "44";
}

public static class ItemUrl
{
    private static readonly string[] InditexDomains =
    {
        "bershka.com",
        "zara.com",
        "pullandbear.com",
        "stradivarius.com",
        "oysho.com",
        "massimodutti.com",
        "uterque.com",
        "uterqe.com",
        "zarahome.com"
    };

    private static readonly Regex SlugSuffix = new(@"-(?:c0p|p|l)\d+(?:\.html)?$", RegexOptions.IgnoreCase);

    public static bool TryParse(string? url, out string colorId, out string error)
    {
        colorId = "";
        error = "";

        var text = (url ?? "").Trim();
        if (text.Length == 0)
        {
            error = "Pega la URL del producto.";
            return false;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            error = "La URL no es válida.";
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            error = "La URL debe empezar por https://";
            return false;
        }

        var host = uri.Host.ToLowerInvariant();
        var known = InditexDomains.Any(d =>
            host.Equals(d, StringComparison.Ordinal) ||
            host.EndsWith("." + d, StringComparison.Ordinal));

        if (!known)
        {
            error = "De momento solo se admiten webs de Inditex: Bershka, Zara, Pull&Bear, Stradivarius, Oysho, Massimo Dutti, Uterqüe y Zara Home.";
            return false;
        }

        var color = "";
        var cs = "";

        foreach (var part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;

            if (kv[0].Equals("colorId", StringComparison.OrdinalIgnoreCase))
                color = Uri.UnescapeDataString(kv[1]);
            else if (kv[0].Equals("cS", StringComparison.OrdinalIgnoreCase))
                cs = Uri.UnescapeDataString(kv[1]);
        }

        colorId = color.Length > 0 ? color : cs;
        return true;
    }

    public static string[] ParseSizes(string? text) =>
        (text ?? "")
            .Split(new[] { ',', ';', ' ', '/' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length is > 0 and <= 6)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static string GuessName(string url)
    {
        try
        {
            var uri = new Uri(url);
            var slug = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "Producto";
            slug = SlugSuffix.Replace(slug, "");
            var words = slug.Replace('-', ' ').Trim();
            return words.Length == 0 ? "Producto" : char.ToUpperInvariant(words[0]) + words[1..];
        }
        catch
        {
            return "Producto";
        }
    }
}
