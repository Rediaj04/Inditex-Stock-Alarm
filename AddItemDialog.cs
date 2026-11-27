using System.Text.Json;

namespace InditexStockAlarm;

public sealed class AddItemDialog : Form
{
    private readonly Func<string, Task<SizeProbeResult>>? _probe;
    private readonly TextBox _url;
    private readonly TextBox _sizesText;
    private readonly TextBox _name;
    private readonly Button _probeButton;
    private readonly Label _status;
    private readonly CheckedListBox _list;
    private readonly List<SizeProbe> _probed = new();

    public List<ItemConfig> Results { get; } = new();

    public AddItemDialog(Func<string, Task<SizeProbeResult>>? probe = null)
    {
        _probe = probe;

        Text = "Añadir producto";
        ClientSize = new Size(780, 650);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(20, 23, 30);
        ForeColor = Color.FromArgb(232, 235, 242);
        Font = new Font("Segoe UI", 9.75f);

        Controls.Add(new Label
        {
            Text = "Añadir producto",
            Font = new Font("Segoe UI Semibold", 15f),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(24, 18)
        });

        Controls.Add(new Label
        {
            Text = "Pega una URL por línea (varias = varios colores) y pulsa «Buscar tallas».\n" +
                   "Vale cualquier ficha de Inditex (Bershka, Zara, Pull&Bear, Oysho, Stradivarius…).",
            Font = new Font("Segoe UI", 8.75f),
            ForeColor = Color.FromArgb(138, 147, 168),
            AutoSize = true,
            Location = new Point(26, 50)
        });

        _url = new TextBox
        {
            Location = new Point(26, 96),
            Width = 560,
            BackColor = Color.FromArgb(30, 34, 44),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 9.5f),
            Multiline = true,
            Height = 64,
            ScrollBars = ScrollBars.Vertical
        };
        _url.KeyDown += async (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.Enter && _probe is not null)
            {
                e.SuppressKeyPress = true;
                await RunProbeAsync();
            }
        };

        _probeButton = new Button
        {
            Text = "Buscar tallas",
            Location = new Point(596, 94),
            Size = new Size(158, 28),
            BackColor = Color.FromArgb(64, 214, 128),
            ForeColor = Color.FromArgb(8, 28, 17),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 9.5f),
            Cursor = Cursors.Hand
        };
        _probeButton.FlatAppearance.BorderSize = 0;
        _probeButton.Click += async (_, _) => await RunProbeAsync();

        _status = new Label
        {
            Text = _probe is null ? "" : "Pega la URL y pulsa «Buscar tallas».",
            ForeColor = Color.FromArgb(138, 147, 168),
            AutoSize = false,
            AutoEllipsis = true,
            Location = new Point(26, 166),
            Size = new Size(728, 20)
        };

        Controls.Add(_url);
        Controls.Add(_probeButton);
        Controls.Add(_status);

        Controls.Add(new Label
        {
            Text = "Tallas detectadas (marca las que quieras vigilar):",
            Font = new Font("Segoe UI Semibold", 8.75f),
            ForeColor = Color.FromArgb(232, 235, 242),
            AutoSize = true,
            Location = new Point(26, 194)
        });

        _list = new CheckedListBox
        {
            Location = new Point(26, 218),
            Size = new Size(728, 140),
            BackColor = Color.FromArgb(30, 34, 44),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            CheckOnClick = true,
            Font = new Font("Segoe UI", 9.75f),
            IntegralHeight = false
        };
        Controls.Add(_list);

        var all = new LinkLabel
        {
            Text = "marcar todas",
            AutoSize = true,
            LinkColor = Color.FromArgb(96, 165, 250),
            Location = new Point(26, 364)
        };
        all.LinkClicked += (_, _) => SetAllChecked(true);

        var none = new LinkLabel
        {
            Text = "desmarcar",
            AutoSize = true,
            LinkColor = Color.FromArgb(96, 165, 250),
            Location = new Point(120, 364)
        };
        none.LinkClicked += (_, _) => SetAllChecked(false);

        Controls.Add(all);
        Controls.Add(none);

        Controls.Add(new Label
        {
            Text = "Tallas a mano (si no detecta o quieres otras):",
            Font = new Font("Segoe UI Semibold", 8.75f),
            ForeColor = Color.FromArgb(232, 235, 242),
            AutoSize = true,
            Location = new Point(26, 392)
        });

        _sizesText = new TextBox
        {
            Location = new Point(26, 416),
            Width = 300,
            BackColor = Color.FromArgb(30, 34, 44),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 10f),
            Text = "44"
        };

        _name = new TextBox
        {
            Location = new Point(346, 416),
            Width = 408,
            BackColor = Color.FromArgb(30, 34, 44),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9.5f),
            PlaceholderText = "Nombre (opcional)"
        };

        Controls.Add(_sizesText);
        Controls.Add(_name);

        Controls.Add(new Label
        {
            Text = "Puedes poner varias separadas por comas: 42,44,46  ·  M,L,XL  ·  o «Única»",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(138, 147, 168),
            AutoSize = true,
            Location = new Point(26, 444)
        });

        var ok = new Button
        {
            Text = "Añadir y vigilar",
            Location = new Point(536, 576),
            Size = new Size(218, 46),
            BackColor = Color.FromArgb(64, 214, 128),
            ForeColor = Color.FromArgb(8, 28, 17),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 10.5f),
            Cursor = Cursors.Hand
        };
        ok.FlatAppearance.BorderSize = 0;
        ok.Click += (_, _) => TryAccept();

        var cancel = new Button
        {
            Text = "Cancelar",
            Location = new Point(420, 576),
            Size = new Size(106, 46),
            BackColor = Color.FromArgb(30, 34, 44),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10f),
            Cursor = Cursors.Hand
        };
        cancel.FlatAppearance.BorderSize = 0;
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;

        Controls.Add(ok);
        Controls.Add(cancel);
        AcceptButton = ok;
        CancelButton = cancel;
        ActiveControl = _url;
    }

    private void SetAllChecked(bool value)
    {
        for (var i = 0; i < _list.Items.Count; i++)
            _list.SetItemChecked(i, value);
    }

    private string FirstUrl() =>
        _url.Lines.Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? "";

    private async Task RunProbeAsync()
    {
        if (_probe is null) return;

        var url = FirstUrl();
        if (!ItemUrl.TryParse(url, out _, out var error))
        {
            _status.ForeColor = Color.FromArgb(255, 120, 140);
            _status.Text = error;
            _url.Focus();
            return;
        }

        _probeButton.Enabled = false;
        _probeButton.Text = "Buscando…";
        _status.ForeColor = Color.FromArgb(246, 173, 60);
        _status.Text = "Abriendo la ficha y leyendo tallas… (puede tardar 10-20 s)";

        try
        {
            var result = await _probe(url);

            _list.Items.Clear();
            _probed.Clear();

            if (result.Ok && result.Sizes.Count > 0)
            {
                foreach (var size in result.Sizes)
                {
                    _probed.Add(size);
                    var index = _list.Items.Add($"{size.Size}   ·   {size.Human}");
                    _list.SetItemChecked(index, size.State != "SOLD_OUT");
                }

                if (string.IsNullOrWhiteSpace(_name.Text) && !string.IsNullOrWhiteSpace(result.Product))
                    _name.Text = result.Product!.Trim();

                _status.ForeColor = Color.FromArgb(64, 214, 128);
                _status.Text = $"Detectadas {result.Sizes.Count} talla(s). Marca las que quieras vigilar.";
            }
            else
            {
                _status.ForeColor = Color.FromArgb(255, 120, 140);
                _status.Text = result.Reason == "sin-tallas"
                    ? "No pude leer las tallas en esa web. Escríbelas a mano abajo."
                    : "No pude leer las tallas: " + (result.Reason ?? "error");
            }
        }
        finally
        {
            _probeButton.Enabled = true;
            _probeButton.Text = "Buscar tallas";
        }
    }

    private void TryAccept()
    {
        var urls = _url.Lines
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (urls.Count == 0)
        {
            MessageBox.Show(this, "Pega al menos una URL.", "Falta la URL",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _url.Focus();
            return;
        }

        foreach (var url in urls)
        {
            if (!ItemUrl.TryParse(url, out _, out var error))
            {
                MessageBox.Show(this, error + Environment.NewLine + Environment.NewLine + url,
                    "URL no válida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _url.Focus();
                return;
            }
        }

        var chosen = new List<string>();
        for (var i = 0; i < _list.Items.Count && i < _probed.Count; i++)
        {
            if (_list.GetItemChecked(i))
                chosen.Add(_probed[i].Size);
        }

        if (chosen.Count == 0)
            chosen = ItemUrl.ParseSizes(_sizesText.Text).ToList();

        if (chosen.Count == 0)
        {
            MessageBox.Show(this, "Marca alguna talla detectada o escríbela a mano (por ejemplo 44 o M).",
                "Falta la talla", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _sizesText.Focus();
            return;
        }

        var sizes = string.Join(",", chosen.Distinct(StringComparer.OrdinalIgnoreCase));

        Results.Clear();
        foreach (var url in urls)
        {
            Results.Add(new ItemConfig
            {
                Url = url,
                Sizes = sizes,
                Name = _name.Text.Trim()
            });
        }

        DialogResult = DialogResult.OK;
    }
}
