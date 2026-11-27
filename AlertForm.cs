namespace InditexStockAlarm;

public sealed record AlertItem(string Name, string Detail, string Price, string Url);

public sealed class AlertForm : Form
{
    private readonly FlowLayoutPanel _list;
    private readonly List<string> _urls = new();

    public event EventHandler? Confirmed;
    public event EventHandler? SilenceRequested;
    public event EventHandler? Bought;

    public IReadOnlyList<string> Urls => _urls;

    public AlertForm()
    {
        Text = "¡STOCK! · Alerta de talla";
        ClientSize = new Size(900, 500);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        TopMost = true;
        BackColor = Color.FromArgb(64, 12, 26);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10.5f);

        Controls.Add(new Label
        {
            Text = "¡TALLA 44 DISPONIBLE!",
            Font = new Font("Segoe UI Black", 30f),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(40, 24)
        });

        _list = new FlowLayoutPanel
        {
            Location = new Point(44, 92),
            Size = new Size(812, 214),
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent,
            AutoScroll = true
        };

        var ok = new Button
        {
            Text = "OK · ¡VOY A COMPRAR!",
            Font = new Font("Segoe UI Black", 22f),
            Size = new Size(812, 104),
            Location = new Point(44, 318),
            BackColor = Color.White,
            ForeColor = Color.FromArgb(130, 10, 34),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        ok.FlatAppearance.BorderSize = 0;
        ok.Click += (_, _) =>
        {
            Confirmed?.Invoke(this, EventArgs.Empty);
            Close();
        };

        Controls.Add(_list);
        Controls.Add(ok);
        Controls.Add(new Label
        {
            Text = "La sirena no parará hasta que pulses el botón.",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(255, 190, 205),
            AutoSize = true,
            Location = new Point(46, 436)
        });

        var silence = new LinkLabel
        {
            Text = "silenciar sirena (el aviso sigue en pantalla)",
            Font = new Font("Segoe UI", 9f),
            LinkColor = Color.FromArgb(255, 190, 205),
            ActiveLinkColor = Color.White,
            AutoSize = true,
            Location = new Point(46, 458)
        };
        silence.LinkClicked += (_, _) => SilenceRequested?.Invoke(this, EventArgs.Empty);
        Controls.Add(silence);

        var bought = new LinkLabel
        {
            Text = "ya he comprado · pausar este producto",
            Font = new Font("Segoe UI", 9f),
            LinkColor = Color.FromArgb(160, 235, 190),
            ActiveLinkColor = Color.White,
            AutoSize = true,
            Location = new Point(430, 458)
        };
        bought.LinkClicked += (_, _) =>
        {
            Bought?.Invoke(this, EventArgs.Empty);
            Close();
        };
        Controls.Add(bought);
    }

    public void AddItem(AlertItem item)
    {
        if (_urls.Contains(item.Url)) return;
        _urls.Add(item.Url);

        var price = string.IsNullOrEmpty(item.Price) ? "" : " · " + item.Price;
        var card = new Panel
        {
            Width = 788,
            Height = 64,
            BackColor = Color.FromArgb(96, 22, 38),
            Margin = new Padding(0, 0, 0, 8)
        };

        card.Controls.Add(new Label
        {
            Text = item.Name,
            Font = new Font("Segoe UI Semibold", 13f),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(14, 8)
        });
        card.Controls.Add(new Label
        {
            Text = item.Detail + price,
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(255, 210, 220),
            AutoSize = true,
            Location = new Point(15, 36)
        });

        _list.Controls.Add(card);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Activate();
        BringToFront();
        NativeMethods.Flash(Handle);
    }
}
