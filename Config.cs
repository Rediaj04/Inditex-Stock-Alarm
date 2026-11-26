using System.Text.Json;
using System.Text.RegularExpressions;

namespace BershkaMonitor;

public sealed class AppConfig
{
    public int IntervalSeconds { get; set; } = 15;
    public bool SoundEnabled { get; set; } = true;
    public bool OpenInBrave { get; set; } = true;
    public List<ItemConfig> Items { get; set; } = new();

    private static readonly JsonSerializerOptions WriteOpts = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions ReadOpts = new() { PropertyNameCaseInsensitive = true };

    public static string PathFor(string dir) => Path.Combine(dir, "config.json");

    public static AppConfig Load(string dir)
    {
        try
        {
            var path = PathFor(dir);
            if (File.Exists(path))
            {
                var cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), ReadOpts);
                if (cfg is not null) return cfg;
            }
        }
        catch
        {
            // configuración corrupta: se usa la de por defecto
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
    private static readonly Regex ProductRx = new(@"-c0p(\d+)\.html", RegexOptions.IgnoreCase);

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

        if (!uri.Host.Contains("bershka.com", StringComparison.OrdinalIgnoreCase))
        {
            error = "De momento solo funciona con URLs de bershka.com.";
            return false;
        }

        if (!ProductRx.IsMatch(uri.AbsolutePath))
        {
            error = "No encuentro el producto en esa URL. Copia la URL completa de la ficha.";
            return false;
        }

        foreach (var part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && kv[0].Equals("colorId", StringComparison.OrdinalIgnoreCase))
                colorId = Uri.UnescapeDataString(kv[1]);
        }

        return true;
    }

    public static string[] ParseSizes(string? text) =>
        (text ?? "")
            .Split(new[] { ',', ';', ' ', '/' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length is > 0 and <= 4)
            .Distinct()
            .ToArray();

    public static string GuessName(string url)
    {
        try
        {
            var uri = new Uri(url);
            var slug = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "Producto";
            var dash = slug.IndexOf("-c0p", StringComparison.OrdinalIgnoreCase);
            if (dash > 0) slug = slug[..dash];
            var words = slug.Replace('-', ' ').Trim();
            return words.Length == 0 ? "Producto" : char.ToUpperInvariant(words[0]) + words[1..];
        }
        catch
        {
            return "Producto";
        }
    }
}
