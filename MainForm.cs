using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace InditexStockAlarm;

public sealed class MainForm : Form
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private const string HeadlessArgs =
        "--headless=new --mute-audio --user-agent=\"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36\"";

    private readonly AppConfig _config;
    private readonly List<MonitorItem> _items = new();
    private readonly Dictionary<string, ItemRow> _rows = new();
    private readonly AlarmPlayer _alarm = new();
    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _alertReset = new() { Interval = 46000 };
    private readonly System.Windows.Forms.Timer _focusBrave = new() { Interval = 1600 };
    private readonly string _logDir;

    private EdgeCdp? _edge;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private int _intervalSec = 15;
    private int _checks;
    private int _failStreak;
    private DateTime _lastCheckUtc = DateTime.MinValue;
    private AlertForm? _alertForm;
    private bool _alertTestMode;
    private long _muteUntilTicks;
    private bool _soundEnabled;
    private bool _allowFast;

    private Label _pill = null!;
    private Label _foot = null!;
    private Button _btnToggle = null!;
    private Button _btnSilence = null!;
    private Button _btnSound = null!;
    private Button _btnLock = null!;
    private NumericUpDown _num = null!;
    private RichTextBox _log = null!;
    private FlowLayoutPanel _itemsPanel = null!;
    private Label _itemsEmpty = null!;

    public MainForm()
    {
        _config = AppConfig.Load(AppPaths.DataDir);
        _allowFast = _config.AllowFastInterval;
        _intervalSec = Math.Clamp(_config.IntervalSeconds, _allowFast ? 5 : 15, 120);
        _soundEnabled = _config.SoundEnabled;
        _alarm.Enabled = _soundEnabled;

        foreach (var itemConfig in _config.Items)
        {
            if (ItemUrl.TryParse(itemConfig.Url, out _, out _))
                _items.Add(new MonitorItem(itemConfig));
        }

        _logDir = Path.Combine(AppPaths.DataDir, "logs");
        Directory.CreateDirectory(_logDir);

        BuildUi();
        _tray = BuildTray();

        _alertReset.Tick += (_, _) =>
        {
            _alertReset.Stop();
            if (_cts is not null) SetPill("● VIGILANDO", Palette.Green, Palette.GreenBg);
            _btnSilence.Enabled = false;
        };

        _focusBrave.Tick += (_, _) =>
        {
            _focusBrave.Stop();
            NativeMethods.FocusWindow("Brave");
        };

        FormClosing += OnFormClosing;
        Shown += OnShown;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeMethods.DarkTitleBar(Handle);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == FormWindowState.Minimized)
        {
            Hide();
            _tray.ShowBalloonTip(2500, "Inditex Stock Alarm", "Sigue vigilando en segundo plano.", ToolTipIcon.Info);
        }
    }

    private async void OnShown(object? sender, EventArgs e)
    {
        await Task.Delay(250);
        Log("Aplicación lista.", LogKind.Info);
        RefreshItems();

        if (_items.Count == 0)
        {
            Log("Primera vez: añade tu primer producto (URL + talla).", LogKind.Info);
            AddItem();
        }

        UpdateFooter();
        await StartMonitorAsync();
    }

    private async void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        try
        {
            await StopMonitorAsync();
        }
        catch
        {
            // ignored
        }

        _alarm.Silence();
        _tray.Visible = false;
        _tray.Dispose();
        NativeMethods.KeepAwake(false);
        _edge?.KillBrowser();
    }

    // ---------------------------------------------------------------- UI

    private void BuildUi()
    {
        SuspendLayout();
        Text = "Inditex · Alarma de stock";
        ClientSize = new Size(1100, 790);
        MinimumSize = new Size(960, 680);
        BackColor = Palette.Bg;
        ForeColor = Palette.Text;
        Font = new Font("Segoe UI", 9.75f);
        StartPosition = FormStartPosition.CenterScreen;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = Palette.Bg
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 56));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildControls(), 0, 1);
        root.Controls.Add(BuildItems(), 0, 2);
        root.Controls.Add(BuildLogPanel(), 0, 3);
        root.Controls.Add(BuildFooter(), 0, 4);
        Controls.Add(root);
        ResumeLayout();
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = Palette.Bg,
            Padding = new Padding(20, 16, 20, 6)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 310));

        var left = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Bg };
        left.Controls.Add(new Label
        {
            Text = "Inditex · Alarma de stock",
            Font = new Font("Segoe UI Semibold", 15f),
            ForeColor = Palette.Text,
            AutoSize = true,
            Location = new Point(0, 2)
        });
        left.Controls.Add(new Label
        {
            Text = "Pega cualquier ficha de Inditex, elige talla y listo: alarma + pestaña en Brave",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Palette.Muted,
            AutoSize = true,
            Location = new Point(2, 36)
        });
        header.Controls.Add(left, 0, 0);

        _pill = new Label
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 8, 0, 8),
            Text = "● DETENIDO",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI Semibold", 11f),
            ForeColor = Palette.Muted,
            BackColor = Palette.Chip
        };
        header.Controls.Add(_pill, 1, 0);
        return header;
    }

    private Control BuildControls()
    {
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Palette.Bg,
            Padding = new Padding(18, 10, 18, 8),
            WrapContents = false
        };

        _btnToggle = MakeButton("▶  INICIAR", Palette.Green, Color.FromArgb(8, 28, 17), 180);
        _btnToggle.Click += async (_, _) =>
        {
            if (_cts is null) await StartMonitorAsync();
            else await StopMonitorAsync();
        };

        _num = new NumericUpDown
        {
            Minimum = _allowFast ? 5 : 15,
            Maximum = 120,
            Value = Math.Clamp(_intervalSec, _allowFast ? 5 : 15, 120),
            Width = 96,
            Height = 30,
            Margin = new Padding(12, 8, 4, 0),
            BackColor = Palette.Card,
            ForeColor = Palette.Text,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 10f)
        };
        _num.ValueChanged += (_, _) =>
        {
            _intervalSec = (int)_num.Value;
            _config.IntervalSeconds = _intervalSec;
            _config.Save(AppPaths.DataDir);
            UpdateFooter();
        };

        var lblSeg = new Label
        {
            Text = "seg",
            AutoSize = true,
            ForeColor = Palette.Muted,
            Margin = new Padding(0, 16, 0, 0)
        };

        _btnLock = MakeButton(_allowFast ? "🔓 mín. 5 s" : "🔒 mín. 15 s", Palette.Chip, Palette.Text, 128);
        _btnLock.Margin = new Padding(8, 0, 10, 0);
        _btnLock.ForeColor = _allowFast ? Palette.Amber : Palette.Muted;
        _btnLock.Click += (_, _) => ToggleFast();

        _btnSound = MakeButton("", Palette.Chip, Palette.Text, 150);
        _btnSound.Margin = new Padding(14, 0, 10, 0);
        _btnSound.Click += (_, _) =>
        {
            _soundEnabled = !_soundEnabled;
            ApplySoundState();
            _config.SoundEnabled = _soundEnabled;
            _config.Save(AppPaths.DataDir);
            if (!_soundEnabled) _alarm.Silence();
            Log(_soundEnabled ? "Sonido de alarma ACTIVADO." : "Sonido de alarma SILENCIADO.", LogKind.Info);
        };
        ApplySoundState();

        var btnTest = MakeButton("Probar aviso", Palette.Chip, Palette.Text, 128);
        btnTest.Click += (_, _) => TestAlert();

        var btnBrave = MakeButton("Abrir en Brave", Palette.Chip, Palette.Text, 142);
        btnBrave.Click += (_, _) => OpenAvailableInBrave();

        _btnSilence = MakeButton("Silenciar", Palette.Red, Color.White, 105);
        _btnSilence.Enabled = false;
        _btnSilence.Click += (_, _) =>
        {
            _alarm.Silence();
            _btnSilence.Enabled = false;
        };

        var hint = new Label
        {
            Text = "Mín. 15 s · PC sin suspensión",
            AutoSize = true,
            ForeColor = Palette.Muted,
            Font = new Font("Segoe UI", 8.5f),
            Margin = new Padding(10, 16, 0, 0)
        };

        bar.Controls.AddRange(new Control[] { _btnToggle, _num, lblSeg, _btnLock, _btnSound, btnTest, btnBrave, _btnSilence, hint });
        return bar;
    }

    private void ApplySoundState()
    {
        _alarm.Enabled = _soundEnabled;
        _btnSound.Text = _soundEnabled ? "Sonido: ON" : "Sonido: OFF";
        _btnSound.BackColor = _soundEnabled ? Palette.GreenBg : Palette.Chip;
        _btnSound.ForeColor = _soundEnabled ? Palette.Green : Palette.Muted;
    }

    private void ToggleFast()
    {
        if (!_allowFast)
        {
            using var dialog = new FastIntervalDialog();
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            _allowFast = true;
            _num.Minimum = 5;
            _config.AllowFastInterval = true;
            _btnLock.Text = "🔓 mín. 5 s";
            _btnLock.ForeColor = Palette.Amber;
            Log("¡Mínimo desbloqueado! Ya puedes bajar de 15 s. Bajo tu responsabilidad.", LogKind.Warn);
        }
        else
        {
            _allowFast = false;
            _num.Minimum = 15;
            if (_num.Value < 15) _num.Value = 15;
            _config.AllowFastInterval = false;
            _btnLock.Text = "🔒 mín. 15 s";
            _btnLock.ForeColor = Palette.Muted;
            Log("Mínimo bloqueado de nuevo en 15 s.", LogKind.Info);
        }

        _config.IntervalSeconds = _intervalSec;
        _config.Save(AppPaths.DataDir);
        UpdateFooter();
    }

    private Control BuildItems()
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Palette.Bg,
            Padding = new Padding(18, 2, 18, 6)
        };

        var head = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = Palette.Bg };
        head.Controls.Add(new Label
        {
            Text = "PRODUCTOS VIGILADOS",
            Font = new Font("Segoe UI Semibold", 8.5f),
            ForeColor = Palette.Muted,
            AutoSize = true,
            Location = new Point(0, 7)
        });

        var add = MakeButton("＋ Añadir producto", Palette.Chip, Palette.Text, 170);
        add.Height = 26;
        add.Font = new Font("Segoe UI Semibold", 9f);
        add.Click += (_, _) => AddItem();
        head.Controls.Add(add);
        head.Resize += (_, _) => add.Location = new Point(head.ClientSize.Width - add.Width, 0);

        _itemsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Palette.Bg,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true
        };
        _itemsPanel.Resize += (_, _) => ReflowRows();

        _itemsEmpty = new Label
        {
            Text = "Todavía no hay productos. Pulsa «＋ Añadir producto» y pega la URL de la ficha.",
            ForeColor = Palette.Muted,
            Font = new Font("Segoe UI", 9.5f),
            AutoSize = true,
            Margin = new Padding(2, 14, 0, 0)
        };
        _itemsPanel.Controls.Add(_itemsEmpty);

        outer.Controls.Add(_itemsPanel);
        outer.Controls.Add(head);
        return outer;
    }

    private Control BuildLogPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Palette.Bg,
            Padding = new Padding(18, 0, 18, 10)
        };

        _log = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = Palette.LogBg,
            ForeColor = Palette.Text,
            Font = new Font("Consolas", 9f),
            WordWrap = false,
            DetectUrls = false,
            ScrollBars = RichTextBoxScrollBars.Vertical
        };

        var head = new Panel { Dock = DockStyle.Top, Height = 24, BackColor = Palette.Bg };
        head.Controls.Add(new Label
        {
            Text = "REGISTRO EN VIVO",
            Font = new Font("Segoe UI Semibold", 8.5f),
            ForeColor = Palette.Muted,
            AutoSize = true,
            Location = new Point(0, 5)
        });

        var link = new LinkLabel
        {
            Text = "abrir carpeta de logs",
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5f),
            LinkColor = Palette.Blue,
            ActiveLinkColor = Palette.Blue,
            Location = new Point(200, 3)
        };
        link.LinkClicked += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(_logDir) { UseShellExecute = true });
            }
            catch
            {
                // ignored
            }
        };
        head.Controls.Add(link);
        head.Resize += (_, _) => link.Location = new Point(head.ClientSize.Width - link.Width, 3);

        panel.Controls.Add(_log);
        panel.Controls.Add(head);
        return panel;
    }

    private Control BuildFooter()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(19, 22, 29) };
        _foot = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Listo.",
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(18, 0, 18, 0),
            ForeColor = Palette.Muted,
            Font = new Font("Segoe UI", 8.5f)
        };
        panel.Controls.Add(_foot);
        return panel;
    }

    private NotifyIcon BuildTray()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Mostrar ventana", null, (_, _) => ShowFromTray());
        menu.Items.Add("Iniciar / Detener", null, async (_, _) =>
        {
            if (_cts is null) await StartMonitorAsync();
            else await StopMonitorAsync();
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Salir", null, (_, _) => Close());

        var tray = new NotifyIcon
        {
            Icon = SystemIcons.Information,
            Text = "Inditex · Alarma de stock",
            Visible = true,
            ContextMenuStrip = menu
        };
        tray.DoubleClick += (_, _) => ShowFromTray();
        return tray;
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        BringToFront();
    }

    private static Button MakeButton(string text, Color back, Color fore, int width)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 40,
            BackColor = back,
            ForeColor = fore,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 9.75f),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 10, 0),
            TabStop = false
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    // ---------------------------------------------------------------- items

    private void AddItem()
    {
        using var dialog = new AddItemDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Result is null) return;

        var cfg = dialog.Result;
        if (_items.Any(i => string.Equals(i.Config.Url, cfg.Url, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(this, "Ese producto ya está en la lista.", "Duplicado",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var item = new MonitorItem(cfg);
        _items.Add(item);
        _config.Items.Add(cfg);
        _config.Save(AppPaths.DataDir);
        RefreshItems();
        Log($"Producto añadido: {item.DisplayName} · tallas {string.Join(",", item.Sizes)}", LogKind.Ok);
    }

    private async void RemoveItem(MonitorItem item)
    {
        _items.Remove(item);
        _config.Items.RemoveAll(c => string.Equals(c.Url, item.Config.Url, StringComparison.OrdinalIgnoreCase));
        _config.Save(AppPaths.DataDir);

        var page = item.Page;
        item.Page = null;
        if (page is not null)
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

        RefreshItems();
        Log($"Producto quitado: {item.DisplayName}", LogKind.Info);
    }

    private void RefreshItems()
    {
        _itemsPanel.SuspendLayout();

        foreach (var control in _itemsPanel.Controls.Cast<Control>().ToList())
        {
            if (control == _itemsEmpty) continue;
            control.Dispose();
        }

        _rows.Clear();
        _itemsEmpty.Visible = _items.Count == 0;
        _itemsPanel.Controls.Add(_itemsEmpty);

        foreach (var item in _items)
            _itemsPanel.Controls.Add(BuildRow(item));

        _itemsPanel.ResumeLayout();
        ReflowRows();
        UpdateFooter();
    }

    private Control BuildRow(MonitorItem item)
    {
        var panel = new Panel
        {
            Width = 900,
            Height = 68,
            BackColor = Palette.Card,
            Margin = new Padding(0, 0, 0, 8)
        };
        panel.Paint += (_, e) =>
        {
            using var pen = new Pen(Palette.CardBorder);
            var r = panel.ClientRectangle;
            e.Graphics.DrawRectangle(pen, 0, 0, r.Width - 1, r.Height - 1);
        };

        var name = new Label
        {
            Text = item.DisplayName,
            Font = new Font("Segoe UI Semibold", 10.5f),
            ForeColor = Palette.Text,
            AutoSize = false,
            AutoEllipsis = true,
            Location = new Point(14, 8),
            Size = new Size(500, 20)
        };

        var sub = new Label
        {
            Text = "tallas " + string.Join(",", item.Sizes),
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Palette.Muted,
            AutoSize = false,
            AutoEllipsis = true,
            Location = new Point(15, 34),
            Size = new Size(500, 18)
        };

        var status = new Label
        {
            Text = item.StatusText,
            Font = new Font("Segoe UI Semibold", 10f),
            ForeColor = Palette.Muted,
            AutoSize = false,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleRight,
            Location = new Point(400, 23),
            Size = new Size(300, 22)
        };

        var remove = new Button
        {
            Text = "✕",
            Size = new Size(28, 28),
            BackColor = Palette.Card,
            ForeColor = Palette.Muted,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            TabStop = false
        };
        remove.FlatAppearance.BorderSize = 0;
        remove.Click += (_, _) => RemoveItem(item);

        panel.Controls.AddRange(new Control[] { name, sub, status, remove });
        panel.Resize += (_, _) =>
        {
            var width = panel.ClientSize.Width;
            name.Width = Math.Max(150, width - 60);
            sub.Width = Math.Max(150, width - 120);
            status.Bounds = new Rectangle(300, 23, Math.Max(120, width - 300 - 52), 22);
            remove.Location = new Point(width - 36, 20);
        };

        _rows[item.Config.Url] = new ItemRow(name, sub, status, panel, remove);
        return panel;
    }

    private void ReflowRows()
    {
        if (_itemsPanel is null) return;
        var width = Math.Max(520, _itemsPanel.ClientSize.Width - 26);
        foreach (Control control in _itemsPanel.Controls)
        {
            if (control == _itemsEmpty) continue;
            control.Width = width;
        }
    }

    private void UpdateRow(MonitorItem item)
    {
        if (!_rows.TryGetValue(item.Config.Url, out var row)) return;

        void Apply()
        {
            row.Name.Text = item.DisplayName;
            row.Sub.Text = item.LastError is null
                ? item.Subtitle
                : "error: " + Truncate(item.LastError, 110);
            row.Status.Text = item.LastError is null ? item.StatusText : "ERROR";
            row.Status.ForeColor = item.LastError is not null
                ? Palette.Amber
                : item.StatusColor;
        }

        if (InvokeRequired) BeginInvoke(new Action(Apply));
        else Apply();
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";

    // ---------------------------------------------------------------- monitor

    private async Task StartMonitorAsync()
    {
        if (_cts is not null) return;

        if (_items.Count == 0)
        {
            Log("Añade al menos un producto antes de iniciar.", LogKind.Warn);
            SetPill("● SIN PRODUCTOS", Palette.Amber, Palette.AmberBg);
            return;
        }

        _btnToggle.Enabled = false;
        _num.Enabled = false;
        SetPill("● CONECTANDO…", Palette.Amber, Palette.AmberBg);
        Log("Iniciando monitor…", LogKind.Info);

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        try
        {
            var edgePath = EdgeCdp.FindEdge()
                           ?? throw new InvalidOperationException("No encuentro msedge.exe en este PC.");

            _edge ??= new EdgeCdp(edgePath, Path.Combine(AppPaths.DataDir, "edge-profile"));
            await _edge.StartAsync(_items[0].Config.Url, ct, HeadlessArgs);
            Log("Edge conectado en segundo plano.", LogKind.Ok);

            NativeMethods.KeepAwake(true);
            _loop = LoopAsync(ct);

            _btnToggle.Text = "■  DETENER";
            _btnToggle.BackColor = Palette.Red;
            _btnToggle.ForeColor = Color.White;
            SetPill("● VIGILANDO", Palette.Green, Palette.GreenBg);
            Log($"Vigilando {_items.Count} producto(s) cada {_intervalSec} s. Sonido: {(_soundEnabled ? "sí" : "no")}.", LogKind.Info);
        }
        catch (Exception ex)
        {
            Log("ERROR al iniciar: " + ex.Message, LogKind.Error);
            SetPill("● ERROR", Palette.Red, Palette.RedBg);
            _cts?.Dispose();
            _cts = null;
        }
        finally
        {
            _btnToggle.Enabled = true;
            _num.Enabled = _cts is null;
        }
    }

    private async Task StopMonitorAsync()
    {
        var cts = _cts;
        if (cts is null) return;

        _cts = null;
        cts.Cancel();

        try
        {
            if (_loop is not null) await _loop;
        }
        catch
        {
            // ignored
        }

        _loop = null;

        foreach (var item in _items)
        {
            var page = item.Page;
            item.Page = null;
            if (page is null) continue;
            try
            {
                await page.DisposeAsync();
            }
            catch
            {
                // ignored
            }
        }

        _alarm.Silence();
        NativeMethods.KeepAwake(false);
        cts.Dispose();

        _btnToggle.Text = "▶  INICIAR";
        _btnToggle.BackColor = Palette.Green;
        _btnToggle.ForeColor = Color.FromArgb(8, 28, 17);
        _num.Enabled = true;
        _btnSilence.Enabled = false;
        SetPill("● DETENIDO", Palette.Muted, Palette.Chip);
        Log("Monitor detenido.", LogKind.Info);
        UpdateFooter();
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var started = DateTime.UtcNow;
            try
            {
                var items = _items.ToList();
                if (items.Count == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(_intervalSec), ct);
                    continue;
                }

                foreach (var item in items)
                {
                    if (item.Page is not null && item.Page.IsOpen) continue;
                    item.Page = await _edge!.NewTabAsync(item.Config.Url, ct);
                    await item.Page.EnableFastModeAsync(ct);
                    await item.Page.WaitForAsync(MonitorScripts.ReadyScript(), 30000, ct);
                    item.FreshlyLoaded = true;
                }

                var tasks = items.Select(item => CheckItemAsync(item, ct));
                var results = await Task.WhenAll(tasks);

                _checks++;
                _lastCheckUtc = DateTime.UtcNow;
                await ApplyResultsAsync(results, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _failStreak++;
                Log($"Fallo de comprobación ({_failStreak}): {ex.Message}", LogKind.Error);
                if (_failStreak == 20)
                    Log("⚠ 20 fallos seguidos: revisa que Edge siga abierto y sin bloqueos.", LogKind.Warn);
            }

            var elapsed = (DateTime.UtcNow - started).TotalSeconds;
            var wait = Math.Max(1, _intervalSec - elapsed);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(wait), ct);
            }
            catch
            {
                break;
            }
        }
    }

    private async Task<(MonitorItem Item, CheckPayload? Payload, string? Error)> CheckItemAsync(
        MonitorItem item, CancellationToken ct)
    {
        try
        {
            if (item.Page is null || !item.Page.IsOpen)
                return (item, null, "pestaña no disponible");

            if (!item.FreshlyLoaded)
            {
                double before = 0;
                try
                {
                    var origin = await item.Page.EvaluateAsync("performance.timeOrigin", 8000, ct);
                    if (origin is { ValueKind: JsonValueKind.Number }) before = origin.Value.GetDouble();
                }
                catch
                {
                    // si no se puede leer, seguimos con la recarga normal
                }

                await item.Page.ReloadAsync(ct);

                var deadline = DateTime.UtcNow.AddSeconds(30);
                while (before > 0 && DateTime.UtcNow < deadline)
                {
                    try
                    {
                        var origin = await item.Page.EvaluateAsync("performance.timeOrigin", 5000, ct);
                        if (origin is { ValueKind: JsonValueKind.Number } && Math.Abs(origin.Value.GetDouble() - before) > 1) break;
                    }
                    catch
                    {
                        // el contexto se destruye al recargar: reintentamos
                    }

                    await Task.Delay(300, ct);
                }
            }

            item.FreshlyLoaded = false;

            var ready = await item.Page.WaitForAsync(MonitorScripts.ReadyScript(), 30000, ct);
            if (!ready)
                return (item, null, "la página no cargó (posible bloqueo)");

            await Task.Delay(700, ct);

            var value = await item.Page.EvaluateAsync(MonitorScripts.SizeScript(item.Sizes), 20000, ct);
            var json = value?.GetString();
            if (string.IsNullOrEmpty(json))
                return (item, null, "respuesta vacía");

            var payload = JsonSerializer.Deserialize<CheckPayload>(json, JsonOpts);
            return payload is null ? (item, null, "respuesta no válida") : (item, payload, null);
        }
        catch (Exception ex)
        {
            return (item, null, ex.Message);
        }
    }

    private async Task ApplyResultsAsync(
        IEnumerable<(MonitorItem Item, CheckPayload? Payload, string? Error)> results,
        CancellationToken ct)
    {
        var parts = new List<string>();
        var alerts = new List<(MonitorItem Item, bool Transition)>();

        foreach (var (item, payload, error) in results)
        {
            if (payload is null || !payload.Ok)
            {
                item.LastError = error ?? payload?.Reason ?? "error";
                item.StatusText = "error";
                UpdateRow(item);
                parts.Add($"{item.DisplayName}: error");
                continue;
            }

            item.LastError = null;
            if (!string.IsNullOrWhiteSpace(payload.Product)) item.ProductName = payload.Product!;
            if (!string.IsNullOrWhiteSpace(payload.Color))
                item.ColorName = payload.Color!;
            else if (!string.IsNullOrWhiteSpace(item.ColorId))
                item.ColorName = "colorId " + item.ColorId;

            var wasAvailable = item.Available;
            item.Available = payload.Sizes.Any(s => s.Available);
            item.StatusText = string.Join(" · ",
                payload.Sizes.Select(s => $"{s.Size}: {(s.Available ? (s.RunningOut ? "¡POCAS!" : "DISPONIBLE") : s.Human)}"));
            item.StatusColor = payload.Sizes.Any(s => s.Available)
                ? (payload.Sizes.Any(s => s.RunningOut) ? Palette.Amber : Palette.Green)
                : Palette.Muted;

            item.PriceCents = payload.Sizes.Select(s => s.Price).FirstOrDefault(p => p is not null)
                              ?? item.PriceCents;

            item.Subtitle = string.Join(" · ",
                new[]
                {
                    string.IsNullOrWhiteSpace(item.ColorName) ? null : item.ColorName,
                    "tallas " + string.Join(",", item.Sizes),
                    string.IsNullOrWhiteSpace(item.PriceText) ? null : item.PriceText
                }.Where(x => x is not null));

            UpdateRow(item);
            parts.Add($"{item.DisplayName}: {item.StatusText}");

            var nowTicks = DateTime.UtcNow.Ticks;
            var transition = item.Available && !wasAvailable;
            var repeat = item.Available && wasAvailable &&
                         nowTicks - item.LastAlertTicks > TimeSpan.FromMinutes(2).Ticks &&
                         nowTicks > _muteUntilTicks;

            if (transition || repeat)
            {
                item.LastAlertTicks = nowTicks;
                alerts.Add((item, transition));
            }
        }

        foreach (var (item, transition) in alerts)
            await OnStockAsync(item, transition);

        Log($"#{_checks:0000} · {string.Join(" · ", parts)}", LogKind.Check);
        UpdateFooter();
    }

    private async Task OnStockAsync(MonitorItem item, bool transition)
    {
        var detail = string.Join(" · ",
            new[]
            {
                string.IsNullOrWhiteSpace(item.ColorName) ? null : item.ColorName,
                "tallas " + string.Join(",", item.Sizes)
            }.Where(x => x is not null));

        var sizesAvailable = item.StatusText;
        Log($"🚨 STOCK · {item.DisplayName} · {sizesAvailable} {(string.IsNullOrEmpty(item.PriceText) ? "" : "· " + item.PriceText)}", LogKind.Alert);

        ShowAlert(new AlertItem(item.DisplayName, detail + " · " + sizesAvailable, item.PriceText, item.Config.Url), test: false);
        _alarm.Alarm($"¡Atención! Hay stock en {item.DisplayName}. Pulsa el botón grande cuando lo veas.");

        void Ui()
        {
            SetPill("● ¡HAY STOCK!", Color.White, Palette.Red);
            _btnSilence.Enabled = _soundEnabled;
            _alertReset.Stop();
            _alertReset.Start();
            NativeMethods.Flash(Handle);
            _tray.ShowBalloonTip(15000, "¡HAY STOCK!", $"{item.DisplayName}\nPulsa el botón grande del aviso.",
                ToolTipIcon.Warning);
        }

        if (InvokeRequired) BeginInvoke(new Action(Ui));
        else Ui();

        if (transition && _config.OpenInBrave)
            OpenInBrave(item.Config.Url);

        await Task.CompletedTask;
    }

    private void ShowAlert(AlertItem item, bool test)
    {
        void Ui()
        {
            if (_alertForm is null || _alertForm.IsDisposed)
            {
                _alertTestMode = test;
                var form = new AlertForm();
                form.Confirmed += OnAlertConfirmed;
                form.SilenceRequested += (_, _) =>
                {
                    _alarm.Silence();
                    _btnSilence.Enabled = false;
                };
                form.FormClosed += (_, _) =>
                {
                    if (_alertForm is null) return;
                    _alertForm = null;
                    _alertTestMode = false;
                    _alarm.Silence();
                    _btnSilence.Enabled = false;
                };
                _alertForm = form;
            }

            _alertForm.AddItem(item);
            if (!_alertForm.Visible) _alertForm.Show();
            if (_alertForm.WindowState == FormWindowState.Minimized)
                _alertForm.WindowState = FormWindowState.Normal;
            _alertForm.Activate();
            _alertForm.BringToFront();
        }

        if (InvokeRequired) BeginInvoke(new Action(Ui));
        else Ui();
    }

    private void OnAlertConfirmed(object? sender, EventArgs e)
    {
        var test = _alertTestMode;
        _alertTestMode = false;
        _alertForm = null;

        _alarm.Silence();
        _btnSilence.Enabled = false;
        _muteUntilTicks = DateTime.UtcNow.AddMinutes(5).Ticks;

        if (test)
        {
            Log("Prueba de aviso confirmada.", LogKind.Info);
            return;
        }

        _focusBrave.Stop();
        _focusBrave.Start();
        Log("Aviso confirmado: pestaña en Brave lista. ¡Suerte!", LogKind.Ok);
    }

    private void TestAlert()
    {
        var sample = _items.FirstOrDefault();
        var detail = sample is null ? "Azul lavado · tallas 44" : sample.Subtitle;
        var item = new AlertItem("PRUEBA · " + (sample?.DisplayName ?? "Jeans barrel"), detail, "17,99 €",
            sample?.Config.Url ?? "https://www.bershka.com/");
        ShowAlert(item, test: true);
        if (_soundEnabled)
            _alarm.Alarm("Prueba de aviso. Así sonará. Pulsa el botón grande para parar la sirena.");
        else
            Log("Prueba de aviso (sonido desactivado).", LogKind.Info);
        _btnSilence.Enabled = _soundEnabled;
    }

    private void OpenAvailableInBrave()
    {
        var candidates = _items.Where(i => i.Available).ToList();
        if (candidates.Count == 0)
        {
            Log("Ahora mismo no hay stock: abro las fichas para tenerlas a mano.", LogKind.Info);
            candidates = _items.ToList();
        }

        foreach (var item in candidates) OpenInBrave(item.Config.Url);
    }

    private void OpenInBrave(string url)
    {
        try
        {
            var brave = FindBrave();
            if (brave is not null)
                Process.Start(new ProcessStartInfo(brave, $"\"{url}\"") { UseShellExecute = false });
            else
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

            Log("Pestaña abierta en Brave: " + url, LogKind.Ok);
            _focusBrave.Stop();
            _focusBrave.Start();
        }
        catch (Exception ex)
        {
            Log("No pude abrir Brave: " + ex.Message, LogKind.Warn);
        }
    }

    internal static string? FindBrave()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "BraveSoftware", "Brave-Browser", "Application", "brave.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "BraveSoftware", "Brave-Browser", "Application", "brave.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BraveSoftware", "Brave-Browser", "Application", "brave.exe")
        };

        var found = candidates.FirstOrDefault(File.Exists);
        if (found is not null) return found;

        try
        {
            foreach (var root in new[] { Microsoft.Win32.Registry.LocalMachine, Microsoft.Win32.Registry.CurrentUser })
            {
                using var key = root.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\brave.exe");
                if (key?.GetValue(null) is string path && !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    return path;
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }

    // ---------------------------------------------------------------- helpers

    private void SetPill(string text, Color fore, Color back)
    {
        void Apply()
        {
            _pill.Text = text;
            _pill.ForeColor = fore;
            _pill.BackColor = back;
        }

        if (InvokeRequired) BeginInvoke(new Action(Apply));
        else Apply();
    }

    private void UpdateFooter()
    {
        var last = _lastCheckUtc == DateTime.MinValue ? "—" : _lastCheckUtc.ToLocalTime().ToString("HH:mm:ss");
        var text = $"PC sin suspensión · última comprobación {last} · cada {_intervalSec} s (mín. {(_allowFast ? 5 : 15)} s) · " +
                   $"{_items.Count} producto(s) · comprobaciones {_checks} · fallos {_failStreak} · " +
                   $"sonido {(_soundEnabled ? "on" : "off")} · Brave {(FindBrave() is null ? "no" : "ok")} · Edge {(_edge is null ? "—" : "ok")}";

        void Apply() => _foot.Text = text;
        if (InvokeRequired) BeginInvoke(new Action(Apply));
        else Apply();
    }

    private void Log(string message, LogKind kind)
    {
        var line = $"{DateTime.Now:HH:mm:ss}  {message}";
        try
        {
            File.AppendAllText(
                Path.Combine(_logDir, $"monitor-{DateTime.Now:yyyyMMdd}.log"),
                line + Environment.NewLine,
                new UTF8Encoding(true));
        }
        catch
        {
            // ignored
        }

        void Apply()
        {
            if (IsDisposed || _log.IsDisposed) return;

            if (_log.TextLength > 0) _log.AppendText(Environment.NewLine);
            _log.SelectionStart = _log.TextLength;
            _log.SelectionLength = 0;
            _log.SelectionColor = kind switch
            {
                LogKind.Ok => Palette.Green,
                LogKind.Warn => Palette.Amber,
                LogKind.Error => Palette.Red,
                LogKind.Alert => Color.FromArgb(255, 120, 150),
                LogKind.Check => Color.FromArgb(150, 160, 185),
                _ => Palette.Text
            };
            _log.AppendText(line);
            _log.SelectionStart = _log.TextLength;
            _log.ScrollToCaret();

            if (_log.Lines.Length > 2600)
            {
                var index = _log.GetFirstCharIndexFromLine(600);
                if (index > 0)
                {
                    _log.Select(0, index);
                    _log.SelectedText = string.Empty;
                }
            }
        }

        if (InvokeRequired) BeginInvoke(new Action(Apply));
        else Apply();
    }

    private sealed record ItemRow(Label Name, Label Sub, Label Status, Panel Panel, Button Remove);

    private enum LogKind
    {
        Info,
        Ok,
        Warn,
        Error,
        Alert,
        Check
    }

    private static class Palette
    {
        public static readonly Color Bg = Color.FromArgb(15, 17, 22);
        public static readonly Color Card = Color.FromArgb(23, 26, 33);
        public static readonly Color CardBorder = Color.FromArgb(38, 43, 56);
        public static readonly Color Text = Color.FromArgb(232, 235, 242);
        public static readonly Color Muted = Color.FromArgb(138, 147, 168);
        public static readonly Color Green = Color.FromArgb(64, 214, 128);
        public static readonly Color GreenBg = Color.FromArgb(17, 43, 29);
        public static readonly Color Red = Color.FromArgb(255, 77, 109);
        public static readonly Color RedBg = Color.FromArgb(61, 17, 28);
        public static readonly Color Amber = Color.FromArgb(246, 173, 60);
        public static readonly Color AmberBg = Color.FromArgb(52, 36, 12);
        public static readonly Color Chip = Color.FromArgb(30, 34, 44);
        public static readonly Color LogBg = Color.FromArgb(11, 13, 17);
        public static readonly Color Blue = Color.FromArgb(96, 165, 250);
    }
}
