namespace BershkaMonitor;

public sealed class SizeResult
{
    public string Size { get; set; } = "";
    public string Visibility { get; set; } = "";
    public int? Price { get; set; }

    public bool Available => Visibility is "SHOW" or "RUNNING_OUT";
    public bool RunningOut => Visibility == "RUNNING_OUT";

    public string Human => Visibility switch
    {
        "SHOW" => "DISPONIBLE",
        "RUNNING_OUT" => "¡POCAS!",
        "SOLD_OUT" => "agotada",
        "NO_EXISTE" => "no existe",
        _ => Visibility.ToLowerInvariant()
    };
}

public sealed class CheckPayload
{
    public bool Ok { get; set; }
    public string? Product { get; set; }
    public string? Color { get; set; }
    public string? Reason { get; set; }
    public List<SizeResult> Sizes { get; set; } = new();
}

public sealed class MonitorItem
{
    public MonitorItem(ItemConfig config)
    {
        Config = config;
        ItemUrl.TryParse(config.Url, out var colorId, out _);
        ColorId = colorId;
        Sizes = ItemUrl.ParseSizes(config.Sizes);
        if (Sizes.Length == 0) Sizes = new[] { "44" };
    }

    public ItemConfig Config { get; }
    public string ColorId { get; }
    public string[] Sizes { get; }

    public CdpPage? Page { get; set; }
    public bool FreshlyLoaded { get; set; }
    public string ProductName { get; set; } = "";
    public string ColorName { get; set; } = "";
    public bool Available { get; set; }
    public string StatusText { get; set; } = "—";
    public Color StatusColor { get; set; } = Color.FromArgb(138, 147, 168);
    public string SizesSummary { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public int? PriceCents { get; set; }
    public long LastAlertTicks { get; set; }
    public string? LastError { get; set; }

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(Config.Name)
            ? Config.Name
            : !string.IsNullOrWhiteSpace(ProductName)
                ? ProductName
                : ItemUrl.GuessName(Config.Url);

    public string PriceText => PriceCents is int c ? (c / 100.0).ToString("0.00") + " €" : "";
}
