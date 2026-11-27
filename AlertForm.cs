using System.Net.Http;

namespace InditexStockAlarm;

public sealed record AlertItem(string Name, string Detail, string Price, string Url, string? ImageUrl);

public sealed class AlertForm : Form
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    private readonly FlowLayoutPanel _list;
    private readonly List<string> _urls = new();
    private readonly PictureBox _image;
    private readonly Label _price;
    private bool _firstItem = true;

    public event EventHandler? Confirmed;
    public event EventHandler? SilenceRequested;
    public event EventHandler? Bought;
    public event EventHandler? OpenRequested;

    public IReadOnlyList<string> Urls => _urls;

    public AlertForm()
    {
        Text = "¡STOCK! · Alerta de talla";
        ClientSize = new Size(980, 620);
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
            Text = "¡TALLA DISPONIBLE!",
            Font = new Font("Segoe UI Black", 30f),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(34, 18)
        });

        _image = new PictureBox
        {
            Location = new Point(34, 88),
            Size = new Size(180, 180),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(96, 22, 38)
        };
        Controls.Add(_image);

        _price = new Label
        {
            Text = "",
            Font = new Font("Segoe UI Black", 22f),
            ForeColor = Color.FromArgb(255, 220, 180),
            AutoSize = true,
            Location = new Point(240, 330)
        };
        Controls.Add(_price);

        _list = new FlowLayoutPanel
        {
            Location = new Point(240, 88),
            Size = new Size(700, 230),
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent,
            AutoScroll = true
        };
        Controls.Add(_list);

        Controls.Add(new Label
        {
            Text = "La sirena no parará hasta que pulses OK o cierres el aviso.",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(255, 190, 205),
            AutoSize = true,
            Location = new Point(34, 500)
        });

        var bought = new Button
        {
            Text = "Ya he comprado · pausar",
            Location = new Point(34, 540),
            Size = new Size(220, 46),
            BackColor = Color.FromArgb(70, 20, 36),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 9.5f),
            Cursor = Cursors.Hand
        };
        bought.FlatAppearance.BorderSize = 1;
        bought.FlatAppearance.BorderColor = Color.FromArgb(140, 70, 90);
        bought.Click += (_, _) =>
        {
            Bought?.Invoke(this, EventArgs.Empty);
            Close();
        };

        var open = new Button
        {
            Text = "Abrir ficha",
            Location = new Point(270, 540),
            Size = new Size(170, 46),
            BackColor = Color.FromArgb(70, 20, 36),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 10f),
            Cursor = Cursors.Hand
        };
        open.FlatAppearance.BorderSize = 1;
        open.FlatAppearance.BorderColor = Color.FromArgb(140, 70, 90);
        open.Click += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);

        var ok = new Button
        {
            Text = "OK · ¡VOY A COMPRAR!",
            Font = new Font("Segoe UI Black", 20f),
            Size = new Size(476, 54),
            Location = new Point(456, 536),
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

        Controls.Add(bought);
        Controls.Add(open);
        Controls.Add(ok);

        var silence = new LinkLabel
        {
            Text = "silenciar sirena (el aviso sigue en pantalla)",
            Font = new Font("Segoe UI", 9f),
            LinkColor = Color.FromArgb(255, 190, 205),
            ActiveLinkColor = Color.White,
            AutoSize = true,
            Location = new Point(34, 592)
        };
        silence.LinkClicked += (_, _) => SilenceRequested?.Invoke(this, EventArgs.Empty);
        Controls.Add(silence);

        AcceptButton = ok;
    }

    public void AddItem(AlertItem item)
    {
        if (_urls.Contains(item.Url)) return;
        _urls.Add(item.Url);

        if (_firstItem)
        {
            _firstItem = false;
            if (!string.IsNullOrWhiteSpace(item.Price))
                _price.Text = item.Price;
            if (!string.IsNullOrWhiteSpace(item.ImageUrl))
                LoadImageAsync(item.ImageUrl!);
        }

        var price = string.IsNullOrEmpty(item.Price) ? "" : " · " + item.Price;
        var card = new Panel
        {
            Width = 676,
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

    private async void LoadImageAsync(string url)
    {
        try
        {
            var bytes = await Http.GetByteArrayAsync(url);
            using var stream = new MemoryStream(bytes);
            var image = Image.FromStream(stream);
            if (!_image.IsDisposed) _image.Image = image;
        }
        catch
        {
            // sin imagen
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Activate();
        BringToFront();
        NativeMethods.Flash(Handle);
    }
}
