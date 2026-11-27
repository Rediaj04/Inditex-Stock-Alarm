using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace InditexStockAlarm;

public sealed class MainForm : Form
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private const string HeadlessArgs = EdgeCdp.HeadlessArgs;

    private readonly AppConfig _config;
    private readonly List<MonitorItem> _items = new();
    private readonly Dictionary<string, RowGroup> _rows = new();
    private readonly AlarmPlayer _alarm = new();
    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _alertReset = new() { Interval = 46000 };
    private readonly System.Windows.Forms.Timer _focusBrave = new() { Interval = 1600 };
    private readonly System.Windows.Forms.Timer _snoozeTick = new() { Interval = 1000 };
    private long _snoozeUntilTicks;
    private Icon? _iconNormal;
    private Icon? _iconAlert;
    private string? _lastAlertUrl;
    private float _uiScale = 1f;
    private static readonly HttpClient ThumbHttp = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly Dictionary<string, Image> _thumbCache = new();
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
    private bool _schedulePaused;
    private bool _moveBraveToScreen;
    private string? _updateTag;
    private bool _updatePrompted;

    private Label _pill = null!;
    private Label _foot = null!;
    private Panel _content = null!;
    private Button _btnToggle = null!;
    private Button _btnSilence = null!;
    private Button _btnSound = null!;
    private Button _btnLock = null!;
    private Button _btnSnooze = null!;
    private NumericUpDown _num = null!;
    private RichTextBox _log = null!;
    private TableLayoutPanel _root = null!;
    private FlowLayoutPanel _itemsPanel = null!;
    private Label _itemsEmpty = null!;

    public MainForm()
    {
        _config = AppConfig.Load(AppPaths.DataDir);
        _uiScale = Math.Clamp(_config.UiScale, 100, 150) / 100f;
        Loc.English = string.Equals(_config.Language, "en", StringComparison.OrdinalIgnoreCase);
        _allowFast = _config.AllowFastInterval;
        _intervalSec = Math.Clamp(_config.IntervalSeconds, _allowFast ? 5 : 15, 120);
        _soundEnabled = _config.SoundEnabled;
        _alarm.Enabled = _soundEnabled;
        _alarm.Mode = _config.SoundMode;
        _alarm.CustomSoundPath = _config.CustomSoundPath;
        Palette.Apply(_config.Theme);

        foreach (var itemConfig in _config.Items)
        {
            if (ItemUrl.TryParse(itemConfig.Url, out _, out _))
                _items.Add(new MonitorItem(itemConfig));
        }

        _logDir = Path.Combine(AppPaths.DataDir, "logs");
        Directory.CreateDirectory(_logDir);

        BuildUi();
        _tray = BuildTray();
        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? Icon;
        }
        catch
        {
            // sin icono propio si falla
        }

        _alertReset.Tick += (_, _) =>
        {
            _alertReset.Stop();
            if (_cts is not null) SetPill("● VIGILANDO", Palette.Green, Palette.GreenBg);
            _btnSilence.Enabled = false;
            SetTrayAlert(false);
        };

        _focusBrave.Tick += (_, _) =>
        {
            _focusBrave.Stop();

            if (_moveBraveToScreen && _config.AlertScreen >= 0)
            {
                var screens = Screen.AllScreens;
                var screen = _config.AlertScreen < screens.Length ? screens[_config.AlertScreen] : Screen.PrimaryScreen;
                var hWnd = NativeMethods.FindWindowByTitle("Brave");

                if (hWnd != IntPtr.Zero && screen is not null)
                    NativeMethods.MoveWindow(hWnd, screen.WorkingArea);

                _moveBraveToScreen = false;
            }

            NativeMethods.FocusWindow("Brave");
        };

        _snoozeTick.Tick += (_, _) =>
        {
            if (_snoozeUntilTicks <= DateTime.UtcNow.Ticks)
            {
                EndSnooze();
                return;
            }

            var remaining = TimeSpan.FromTicks(_snoozeUntilTicks - DateTime.UtcNow.Ticks);
            _btnSnooze.Text = Loc.T(
                $"Quitar snooze · {remaining:mm\\:ss}",
                $"Cancel snooze · {remaining:mm\\:ss}");
        };

        FormClosing += OnFormClosing;
        Shown += OnShown;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeMethods.DarkTitleBar(Handle);
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
        _ = CheckUpdatesAsync();

        if (_config.StartMinimized)
        {
            Hide();
            _tray.ShowBalloonTip(2500, "Inditex Stock Alarm",
                "Vigilando en segundo plano. Doble clic en la bandeja para abrir.", ToolTipIcon.Info);
        }
    }

    private async Task CheckUpdatesAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("InditexStockAlarm");
            var json = await http.GetStringAsync("https://api.github.com/repos/Rediaj04/Inditex-Stock-Alarm/releases/latest");
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("tag_name", out var tagEl)) return;
            var tag = tagEl.GetString()?.TrimStart('v', 'V');
            var url = doc.RootElement.TryGetProperty("html_url", out var u) ? u.GetString() : null;
            var current = typeof(MainForm).Assembly.GetName().Version;

            if (string.IsNullOrWhiteSpace(tag) || current is null) return;
            if (!Version.TryParse(tag, out var latest) || latest <= current) return;

            _updateTag = tag;
            Log($"Nueva versión disponible: v{tag}" + (string.IsNullOrEmpty(url) ? "" : $" → {url}"), LogKind.Warn);
            UpdateFooter();

            if (!_updatePrompted && !string.IsNullOrEmpty(url))
            {
                _updatePrompted = true;
                BeginInvoke(new Action(() =>
                {
                    using var dialog = new UpdateDialog(tag!, url!);
                    dialog.ShowDialog(this);
                }));
            }
        }
        catch
        {
            // sin conexión o API caída: no molestamos
        }
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
        ClientSize = new Size(1120, 800);
        MinimumSize = new Size(760, 600);
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
        _root = root;
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 200));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildControls(), 0, 1);
        root.Controls.Add(BuildItems(), 0, 2);
        root.Controls.Add(BuildLogPanel(), 0, 3);
        root.Controls.Add(BuildFooter(), 0, 4);

        _content = new Panel { BackColor = Palette.Bg };
        _content.Controls.Add(root);
        Controls.Add(_content);
        PositionContent();
        ResumeLayout();
        Loc.Apply(this);
    }

    private void PositionContent()
    {
        if (_content is null) return;

        var width = Math.Min(Px(1120), Math.Max(Px(600), ClientSize.Width - Px(32)));
        _content.Bounds = new Rectangle((ClientSize.Width - width) / 2, 0, width, ClientSize.Height);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        PositionContent();

        if (WindowState == FormWindowState.Minimized)
        {
            Hide();
            _tray.ShowBalloonTip(2500, "Inditex Stock Alarm", Loc.T("Sigue vigilando en segundo plano.", "Still watching in the background."), ToolTipIcon.Info);
        }
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Palette.Bg,
            Padding = new Padding(20, 14, 20, 4)
        };
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));

        var left = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Bg };
        left.Controls.Add(new Label
        {
            Text = "Inditex · Alarma de stock",
            Font = new Font("Segoe UI Semibold", 14.5f),
            ForeColor = Palette.Text,
            AutoSize = true,
            Location = new Point(0, 2)
        });

        var subtitle = new Label
        {
            Text = "Pega cualquier ficha de Inditex, elige talla y listo: alarma + pestaña en Brave",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Palette.Muted,
            AutoSize = false,
            AutoEllipsis = true,
            Location = new Point(2, 32),
            Size = new Size(420, 18)
        };
        left.Controls.Add(subtitle);
        left.Resize += (_, _) => subtitle.Width = Math.Max(120, left.ClientSize.Width - 6);

        header.Controls.Add(left, 0, 0);

        _pill = new Label
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 10, 0, 10),
            Text = "● DETENIDO",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI Semibold", 9.5f),
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
            Dock = DockStyle.Top,
            BackColor = Palette.Bg,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(20, 8, 20, 8),
            WrapContents = true
        };

        _btnToggle = MakeButton("▶  INICIAR", Palette.Green, Color.FromArgb(8, 28, 17), 142);
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
            Width = 118,
            TextAlign = HorizontalAlignment.Center,
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

        _btnLock = MakeButton(_allowFast ? "🔓 mín. 5 s" : "🔒 mín. 15 s", Palette.Chip, Palette.Text, 104);
        _btnLock.Margin = new Padding(8, 0, 10, 0);
        _btnLock.ForeColor = _allowFast ? Palette.Amber : Palette.Muted;
        _btnLock.Click += (_, _) => ToggleFast();

        _btnSound = MakeButton("", Palette.Chip, Palette.Text, 124);
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

        var btnTest = MakeButton("Probar", Palette.Chip, Palette.Text, 84);
        btnTest.Click += (_, _) => TestAlert();

        var btnBrave = MakeButton("Abrir web", Palette.Chip, Palette.Text, 96);
        btnBrave.Click += (_, _) => OpenAvailableInBrave();

        var btnSettings = MakeButton("Ajustes", Palette.Chip, Palette.Text, 84);
        btnSettings.Click += (_, _) => OpenSettings();

        _btnSnooze = MakeButton("Snooze", Palette.Chip, Palette.Text, 84);
        _btnSnooze.Click += (_, _) => Snooze();

        _btnSilence = MakeButton("Silenciar", Palette.Chip, Palette.Red, 84);
        _btnSilence.Enabled = false;
        _btnSilence.Click += (_, _) =>
        {
            _alarm.Silence();
            _btnSilence.Enabled = false;
        };

        bar.Controls.AddRange(new Control[] { _btnToggle, _num, lblSeg, _btnLock, _btnSound, btnTest, btnBrave, btnSettings, _btnSnooze, _btnSilence });
        return bar;
    }

    private void ApplySoundState()
    {
        _alarm.Enabled = _soundEnabled;
        _btnSound.Text = Loc.T(_soundEnabled ? "Sonido: ON" : "Sonido: OFF", _soundEnabled ? "Sound: ON" : "Sound: OFF");
        _btnSound.BackColor = Palette.Chip;
        _btnSound.ForeColor = _soundEnabled ? Palette.Green : Palette.Muted;
    }

    private static string StartupShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Startup), "InditexStockAlarm.lnk");

    private void SetStartup(bool enabled)
    {
        try
        {
            var path = StartupShortcutPath;

            if (enabled)
            {
                var type = Type.GetTypeFromProgID("WScript.Shell");
                if (type is null) throw new InvalidOperationException("WScript.Shell no está disponible.");

                dynamic shell = Activator.CreateInstance(type)!;
                dynamic lnk = shell.CreateShortcut(path);
                lnk.TargetPath = Application.ExecutablePath;
                lnk.WorkingDirectory = AppPaths.DataDir;
                lnk.IconLocation = Application.ExecutablePath + ",0";
                lnk.Description = "Inditex Stock Alarm";
                lnk.Save();
                Log("Arranque con Windows ACTIVADO.", LogKind.Ok);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
                Log("Arranque con Windows desactivado.", LogKind.Info);
            }
        }
        catch (Exception ex)
        {
            Log("No pude cambiar el arranque con Windows: " + ex.Message, LogKind.Warn);
        }
    }

    private int Px(int value) => (int)Math.Round(value * _uiScale);

    private int EffectiveInterval => _config.GentleMode ? Math.Max(_intervalSec, 45) : _intervalSec;

    private bool InSchedule()
    {
        if (!_config.ScheduleEnabled) return true;

        var start = TimeSpan.TryParse(_config.ScheduleStart, out var s) ? s : TimeSpan.Zero;
        var end = TimeSpan.TryParse(_config.ScheduleEnd, out var e) ? e : TimeSpan.FromHours(24);
        if (start == end) return true;

        var now = DateTime.Now.TimeOfDay;
        return end < start ? now >= start || now < end : now >= start && now < end;
    }

    private async void OpenSettings()
    {
        using var dialog = new SettingsDialog(_config, File.Exists(StartupShortcutPath));
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        _config.SoundMode = dialog.SoundMode;
        _config.NtfyTopic = dialog.NtfyTopic;
        _config.ScheduleEnabled = dialog.ScheduleEnabled;
        _config.ScheduleStart = dialog.ScheduleStart;
        _config.ScheduleEnd = dialog.ScheduleEnd;
        _config.GentleMode = dialog.GentleMode;
        _config.Theme = dialog.Theme;
        _config.AlertScreen = dialog.AlertScreen;
        _config.StartMinimized = dialog.StartMinimized;
        _config.CustomSoundPath = dialog.CustomSoundPath;
        _config.Language = dialog.Language;
        _config.UiScale = dialog.UiScale;
        _config.Save(AppPaths.DataDir);

        _alarm.Mode = _config.SoundMode;
        _alarm.CustomSoundPath = _config.CustomSoundPath;

        if (dialog.StartWithWindows != File.Exists(StartupShortcutPath))
            SetStartup(dialog.StartWithWindows);

        Log("Ajustes guardados.", LogKind.Ok);
        UpdateFooter();

        if (dialog.ThemeChanged)
            MessageBox.Show(this, "El tema se aplicará la próxima vez que abras la app.", "Tema",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ToggleFast()
    {        if (!_allowFast)
        {
            using var dialog = new FastIntervalDialog();
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            _allowFast = true;
            _num.Minimum = 5;
            _config.AllowFastInterval = true;
            _btnLock.Text = Loc.T("🔓 mín. 5 s", "🔓 min. 5 s");
            _btnLock.ForeColor = Palette.Amber;
            Log("¡Mínimo desbloqueado! Ya puedes bajar de 15 s. Bajo tu responsabilidad.", LogKind.Warn);
        }
        else
        {
            _allowFast = false;
            _num.Minimum = 15;
            if (_num.Value < 15) _num.Value = 15;
            _config.AllowFastInterval = false;
            _btnLock.Text = Loc.T("🔒 mín. 15 s", "🔒 min. 15 s");
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

        var head = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Palette.Bg };
        head.Controls.Add(new Label
        {
            Text = "PRODUCTOS VIGILADOS",
            Font = new Font("Segoe UI Semibold", 8.5f),
            ForeColor = Palette.Muted,
            AutoSize = true,
            Location = new Point(0, 13)
        });

        var add = MakeButton("＋ Añadir producto", Palette.Card, Palette.Text, 170);
        add.Height = 28;
        add.Font = new Font("Segoe UI Semibold", 9f);
        add.FlatAppearance.BorderSize = 1;
        add.FlatAppearance.BorderColor = Palette.CardBorder;
        add.Click += (_, _) => AddItem();
        head.Controls.Add(add);

        head.Resize += (_, _) => add.Location = new Point(head.ClientSize.Width - add.Width, 6);

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
            WordWrap = true,
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

        var clear = new LinkLabel
        {
            Text = "limpiar",
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5f),
            LinkColor = Palette.Muted,
            ActiveLinkColor = Palette.Text,
            Location = new Point(200, 3)
        };
        clear.LinkClicked += (_, _) => _log.Clear();
        head.Controls.Add(clear);

        head.Resize += (_, _) =>
        {
            link.Location = new Point(head.ClientSize.Width - link.Width, 3);
            clear.Location = new Point(link.Left - clear.Width - 16, 3);
        };

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
            AutoEllipsis = true,
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
        menu.Items.Add(Loc.T("Mostrar ventana", "Show window"), null, (_, _) => ShowFromTray());
        menu.Items.Add(Loc.T("Iniciar / Detener", "Start / Stop"), null, async (_, _) =>
        {
            if (_cts is null) await StartMonitorAsync();
            else await StopMonitorAsync();
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Loc.T("Salir", "Exit"), null, (_, _) => Close());

        _iconNormal = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Information;
        _iconAlert = SystemIcons.Warning;

        var tray = new NotifyIcon
        {
            Icon = _iconNormal,
            Text = "Inditex · Alarma de stock",
            Visible = true,
            ContextMenuStrip = menu
        };
        tray.DoubleClick += (_, _) => ShowFromTray();
        tray.BalloonTipClicked += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_lastAlertUrl))
                OpenInBrave(_lastAlertUrl!, moveToAlertScreen: true);
        };
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
            Height = 36,
            BackColor = back,
            ForeColor = fore,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 9.25f),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 8, 0),
            TabStop = true,
            AccessibleRole = AccessibleRole.PushButton,
            AccessibleName = text
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    // ---------------------------------------------------------------- items

    private void AddItem()
    {
        using var dialog = new AddItemDialog(ProbeSizesAsync);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Results.Count == 0) return;

        var added = 0;
        var skipped = 0;

        foreach (var cfg in dialog.Results)
        {
            if (_items.Any(i => string.Equals(i.Config.Url, cfg.Url, StringComparison.OrdinalIgnoreCase)))
            {
                skipped++;
                continue;
            }

            var item = new MonitorItem(cfg);
            _items.Add(item);
            _config.Items.Add(cfg);
            added++;
            Log($"Producto añadido: {item.DisplayName} · tallas {string.Join(",", item.Sizes)}", LogKind.Ok);
        }

        if (added == 0)
        {
            MessageBox.Show(this, skipped > 0 ? "Ese producto ya está en la lista." : "No se añadió ninguno.",
                "Sin cambios", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _config.Save(AppPaths.DataDir);
        RefreshItems();
        UpdateFooter();
    }

    internal async Task<SizeProbeResult> ProbeSizesAsync(string url)
    {
        var edgePath = EdgeCdp.FindBrowser()
                       ?? throw new InvalidOperationException("No encuentro Edge, Chrome ni Brave en este PC.");

        _edge ??= new EdgeCdp(edgePath, Path.Combine(AppPaths.DataDir, "edge-profile"));
        return await SizeProbeService.ProbeAsync(_edge, url);
    }

    private void ShowGroupMenu(List<MonitorItem> members, Control anchor)
    {
        var allPaused = members.All(m => m.Paused);
        var menu = new ContextMenuStrip();
        menu.Items.Add(Loc.T("Comprobar ahora", "Check now"), null, (_, _) =>
        {
            foreach (var member in members) CheckNow(member);
        });
        menu.Items.Add(Loc.T("Editar tallas / nombre…", "Edit sizes / name…"), null, (_, _) => EditMembers(members));
        menu.Items.Add(allPaused ? Loc.T("Reanudar", "Resume") : Loc.T("Pausar", "Pause"), null, (_, _) => ToggleMembers(members));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Loc.T("Eliminar", "Delete"), null, async (_, _) => await RemoveMembersAsync(members));
        menu.Show(anchor, new Point(0, anchor.Height));
    }

    private void ToggleMembers(List<MonitorItem> members)
    {
        var pause = !members.All(m => m.Paused);
        foreach (var member in members)
        {
            member.Paused = pause;
            if (pause)
            {
                member.Available = false;
                member.StatusText = "PAUSADO";
                member.StatusColor = Palette.Muted;
            }
        }

        UpdateRow(members[0]);
        Log(pause ? $"Pausado: {members[0].DisplayName}" : $"Reanudado: {members[0].DisplayName}", LogKind.Info);
        UpdateFooter();
    }

    private async Task EnsurePageAsync(MonitorItem item, CancellationToken ct)
    {
        if (_edge is null)
        {
            var edgePath = EdgeCdp.FindBrowser()
                           ?? throw new InvalidOperationException("No encuentro Edge, Chrome ni Brave en este PC.");
            _edge = new EdgeCdp(edgePath, Path.Combine(AppPaths.DataDir, "edge-profile"));
            await _edge.StartAsync(item.Config.Url, ct, HeadlessArgs);
        }

        if (item.Page is not null && item.Page.IsOpen) return;

        item.Page = await _edge.NewTabAsync(item.Config.Url, ct);
        await item.Page.EnableFastModeAsync(ct);
        await item.Page.WaitForAsync("document.readyState === 'complete'", 30000, ct);
        await Task.Delay(800, ct);
        item.FreshlyLoaded = true;
    }

    private async void CheckNow(MonitorItem item)
    {
        try
        {
            Log($"Comprobando ahora: {item.DisplayName}…", LogKind.Info);
            var ct = _cts?.Token ?? CancellationToken.None;
            await EnsurePageAsync(item, ct);
            var result = await CheckItemAsync(item, ct);
            await ApplyResultsAsync(new[] { result }, ct);
        }
        catch (Exception ex)
        {
            Log("No pude comprobar ahora: " + ex.Message, LogKind.Error);
        }
    }

    private void EditMembers(List<MonitorItem> members)
    {
        using var dialog = new EditItemDialog(members[0]);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        foreach (var member in members)
        {
            member.Config.Name = dialog.ItemName;
            member.Config.Sizes = string.Join(",", dialog.SelectedSizes);
            member.Sizes = dialog.SelectedSizes;
        }

        _config.Save(AppPaths.DataDir);
        RefreshItems();
        Log($"Editado: {members[0].DisplayName} · tallas {string.Join(",", members[0].Sizes)}", LogKind.Ok);
    }

    private void Snooze()
    {
        if (_snoozeUntilTicks > DateTime.UtcNow.Ticks)
        {
            _snoozeUntilTicks = 0;
            _snoozeTick.Stop();
            _btnSnooze.Text = Loc.T("Snooze 30m", "Snooze 30m");
            if (_cts is not null) SetPill("● VIGILANDO", Palette.Green, Palette.GreenBg);
            Log("Snooze cancelado: vuelven los avisos.", LogKind.Info);
            SetTrayAlert(false);
            return;
        }

        _snoozeUntilTicks = DateTime.UtcNow.AddMinutes(30).Ticks;
        _alarm.Silence();
        _btnSilence.Enabled = false;
        _snoozeTick.Stop();
        _snoozeTick.Start();
        _btnSnooze.Text = Loc.T("Quitar snooze · 30:00", "Cancel snooze · 30:00");
        if (_cts is not null) SetPill("● SNOOZE 30 MIN", Palette.Amber, Palette.AmberBg);
        Log($"Avisos silenciados hasta las {DateTime.Now.AddMinutes(30):HH:mm} (la vigilancia sigue).", LogKind.Warn);
    }

    private void EndSnooze()
    {
        _snoozeTick.Stop();
        _snoozeUntilTicks = 0;
        _btnSnooze.Text = Loc.T("Snooze 30m", "Snooze 30m");
        if (_cts is not null) SetPill("● VIGILANDO", Palette.Green, Palette.GreenBg);
        SetTrayAlert(false);
        Log("Snooze terminado: vuelven los avisos.", LogKind.Info);
    }

    private void SetTrayAlert(bool alert)
    {
        try
        {
            _tray.Icon = alert ? _iconAlert : _iconNormal;
        }
        catch
        {
            // ignored
        }
    }

    private void PauseUrls(IReadOnlyList<string> urls)
    {
        foreach (var item in _items)
        {
            if (item.Paused) continue;
            if (!urls.Any(u => string.Equals(u, item.Config.Url, StringComparison.OrdinalIgnoreCase))) continue;

            item.Paused = true;
            item.Available = false;
            item.StatusText = "PAUSADO";
            item.StatusColor = Palette.Muted;
            UpdateRow(item);
            Log($"Pausado tras la compra: {item.DisplayName}", LogKind.Ok);
        }

        UpdateFooter();
    }

    private async Task RemoveMembersAsync(List<MonitorItem> members)
    {
        foreach (var member in members.ToList())
        {
            _items.Remove(member);
            _config.Items.RemoveAll(c => string.Equals(c.Url, member.Config.Url, StringComparison.OrdinalIgnoreCase));

            var page = member.Page;
            member.Page = null;
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

        _config.Save(AppPaths.DataDir);
        RefreshItems();
        Log($"Producto quitado: {members[0].DisplayName}", LogKind.Info);
    }

    private static string GroupKeyOf(MonitorItem item)
    {
        try
        {
            return new Uri(item.Config.Url).AbsolutePath.ToLowerInvariant();
        }
        catch
        {
            return item.Config.Url;
        }
    }

    private static string GroupStatus(List<MonitorItem> members, out Color color)
    {
        if (members.All(m => m.Paused))
        {
            color = Palette.Muted;
            return Loc.T("PAUSADO", "PAUSED");
        }

        var available = members.Where(m => m.Available).ToList();
        if (available.Count > 0)
        {
            color = available.Any(m => m.StatusText.Contains("POCAS")) ? Palette.Amber : Palette.Green;
            if (available.Count == 1)
            {
                var name = available[0].ColorName;
                return string.IsNullOrWhiteSpace(name)
                    ? Loc.T("DISPONIBLE", "IN STOCK")
                    : Loc.T("DISPONIBLE", "IN STOCK") + " · " + name;
            }

            return Loc.T($"DISPONIBLE · {available.Count} colores", $"IN STOCK · {available.Count} colours");
        }

        if (members.Any(m => m.LastError is not null))
        {
            color = Palette.Amber;
            return "ERROR";
        }

        color = Palette.Muted;
        return members.Any(m => string.IsNullOrEmpty(m.LastError))
            ? Loc.T("agotada", "sold out")
            : "—";
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

        foreach (var group in _items.GroupBy(GroupKeyOf))
        {
            var members = group.ToList();
            _itemsPanel.Controls.Add(BuildRow(members));
            UpdateRow(members[0]);
        }

        _itemsPanel.ResumeLayout();
        ReflowRows();
        UpdateItemsHeight();
        UpdateFooter();
    }

    private void UpdateItemsHeight()
    {
        if (_root is null) return;

        var groups = _items.Select(GroupKeyOf).Distinct().Count();
        var height = Math.Min(Px(260), Math.Max(Px(120), Px(64) + groups * Px(84)));
        _root.RowStyles[2].Height = height;
    }

    private Control BuildRow(List<MonitorItem> members)
    {
        var item = members[0];
        var nameFont = new Font("Segoe UI Semibold", 10.5f * _uiScale);
        var subFont = new Font("Segoe UI", 8.5f * _uiScale);
        var statusFont = new Font("Segoe UI Semibold", 10f * _uiScale);

        var nameHeight = nameFont.Height + 2;
        var subHeight = subFont.Height + 2;
        var statusHeight = statusFont.Height + 8;
        var top = Px(14);
        var rowHeight = Math.Max(Px(76), top + nameHeight + 4 + subHeight + 10);

        var panel = new Panel
        {
            Width = Math.Max(320, _itemsPanel.ClientSize.Width - 26 - SystemInformation.VerticalScrollBarWidth),
            Height = rowHeight,
            BackColor = Palette.Card,
            Margin = new Padding(0, 0, 0, Px(8))
        };
        panel.Paint += (_, e) =>
        {
            using var pen = new Pen(Palette.CardBorder);
            var r = panel.ClientRectangle;
            e.Graphics.DrawRectangle(pen, 0, 0, r.Width - 1, r.Height - 1);
        };

        var thumb = new PictureBox
        {
            Size = new Size(Px(44), Px(44)),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Palette.Chip,
            Location = new Point(Px(14), (rowHeight - Px(44)) / 2)
        };

        var name = new Label
        {
            Text = item.DisplayName,
            Font = nameFont,
            ForeColor = Palette.Text,
            AutoSize = false,
            AutoEllipsis = true,
            Location = new Point(Px(70), top),
            Size = new Size(300, nameHeight)
        };

        var sub = new Label
        {
            Text = string.IsNullOrWhiteSpace(item.ColorName) ? "" : item.ColorName,
            Font = subFont,
            ForeColor = Palette.Muted,
            AutoSize = false,
            AutoEllipsis = true,
            Location = new Point(Px(71), top + nameHeight + 4),
            Size = new Size(300, subHeight)
        };

        var mid = new Label
        {
            Text = Loc.T("tallas ", "sizes ") + string.Join(",", item.Sizes)
                   + (string.IsNullOrWhiteSpace(item.PriceText) ? "" : " · " + item.PriceText),
            Font = subFont,
            ForeColor = Palette.Muted,
            AutoSize = false,
            AutoEllipsis = true,
            Location = new Point(Px(300), top + nameHeight + 4),
            Size = new Size(200, subHeight)
        };

        var status = new Label
        {
            Text = item.StatusText,
            Font = statusFont,
            ForeColor = Palette.Muted,
            AutoSize = false,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleRight,
            Location = new Point(Px(320), (rowHeight - statusHeight) / 2),
            Size = new Size(300, statusHeight)
        };

        var menu = new Button
        {
            Text = "⋯",
            Size = new Size(Px(28), Px(28)),
            BackColor = Palette.Card,
            ForeColor = Palette.Muted,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            TabStop = true
        };
        menu.FlatAppearance.BorderSize = 0;
        menu.Click += (_, _) => ShowGroupMenu(members, menu);

        panel.Controls.AddRange(new Control[] { thumb, name, sub, mid, status, menu });

        void LayoutRow()
        {
            var width = panel.ClientSize.Width;
            menu.Location = new Point(Math.Max(Px(10), width - menu.Width - Px(10)), Math.Max(0, (panel.Height - menu.Height) / 2));
            thumb.Location = new Point(Px(14), Math.Max(0, (panel.Height - thumb.Height) / 2));

            var menuX = Math.Max(Px(10), width - menu.Width - Px(10));
            var statusX = Math.Max(Px(180), Math.Min(Math.Max(Px(300), width - Px(310)), menuX - Px(120)));
            var midX = Math.Max(Px(240), (int)(width * 0.46));
            var midW = statusX - midX - Px(10);
            mid.Visible = midW >= Px(80);
            mid.Location = new Point(midX, top + nameHeight + 4);
            mid.Width = Math.Max(Px(60), midW);
            name.Width = Math.Max(Px(80), (mid.Visible ? midX : statusX) - Px(80));
            sub.Width = Math.Max(Px(80), (mid.Visible ? midX : statusX) - Px(84));
            status.Bounds = new Rectangle(
                statusX,
                Math.Max(0, (panel.Height - statusHeight) / 2),
                Math.Max(Px(80), menuX - statusX - Px(10)),
                statusHeight);
        }

        panel.Resize += (_, _) => LayoutRow();
        LayoutRow();

        if (!string.IsNullOrWhiteSpace(item.ImageUrl))
            LoadThumbnailAsync(thumb, item.ImageUrl!);

        _rows[GroupKeyOf(item)] = new RowGroup
        {
            Key = GroupKeyOf(item),
            Members = members,
            Ui = new ItemRow(name, sub, mid, status, panel, menu, thumb)
        };
        return panel;
    }

    internal string DumpLayout()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"panelClient={_itemsPanel.ClientSize.Width}x{_itemsPanel.ClientSize.Height}");
        sb.AppendLine($"hScroll visible={_itemsPanel.HorizontalScroll.Visible} max={_itemsPanel.HorizontalScroll.Maximum} large={_itemsPanel.HorizontalScroll.LargeChange}");
        sb.AppendLine($"vScroll visible={_itemsPanel.VerticalScroll.Visible} max={_itemsPanel.VerticalScroll.Maximum}");
        foreach (Control control in _itemsPanel.Controls)
            sb.AppendLine($"{control.GetType().Name} w={control.Width} left={control.Left} margin={control.Margin.Left},{control.Margin.Right}");
        return sb.ToString();
    }

    private async void LoadThumbnailAsync(PictureBox box, string url)
    {
        try
        {
            if (_thumbCache.TryGetValue(url, out var cached))
            {
                if (!box.IsDisposed) box.Image = cached;
                return;
            }

            var bytes = await ThumbHttp.GetByteArrayAsync(url);
            using var stream = new MemoryStream(bytes);
            var image = Image.FromStream(stream);
            _thumbCache[url] = image;

            if (!box.IsDisposed) box.Image = image;
        }
        catch
        {
            // sin miniatura
        }
    }

    private void ReflowRows()
    {
        if (_itemsPanel is null) return;
        var width = Math.Max(320, _itemsPanel.ClientSize.Width);
        foreach (Control control in _itemsPanel.Controls)
        {
            if (control == _itemsEmpty) continue;
            control.Width = width;
        }

        if (_itemsPanel.HorizontalScroll.Maximum > _itemsPanel.ClientSize.Width)
        {
            // WinForms conserva el ancho de scroll antiguo al reducir filas: lo reiniciamos
            _itemsPanel.AutoScroll = false;
            _itemsPanel.AutoScroll = true;
            _itemsPanel.PerformLayout();
        }
    }

    private void UpdateRow(MonitorItem item)
    {
        if (!_rows.TryGetValue(GroupKeyOf(item), out var group)) return;

        var members = group.Members;
        var first = members[0];
        var row = group.Ui;

        void Apply()
        {
            var colorSummary = string.Join(" · ",
                members.Select(m => m.ColorName).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct());
            var price = members.Select(m => m.PriceText)
                .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)) ?? "";

            row.Name.Text = first.DisplayName;
            row.Sub.Text = colorSummary;
            row.Mid.Text = Loc.T("tallas ", "sizes ") + string.Join(",", first.Sizes)
                           + (price.Length > 0 ? " · " + price : "");
            row.Status.Text = GroupStatus(members, out var statusColor);
            row.Status.ForeColor = statusColor;

            var image = members.Select(m => m.ImageUrl)
                .FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));
            if (!string.IsNullOrWhiteSpace(image) && row.Thumb.Image is null)
                LoadThumbnailAsync(row.Thumb, image!);
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
            var edgePath = EdgeCdp.FindBrowser()
                           ?? throw new InvalidOperationException("No encuentro Edge, Chrome ni Brave en este PC.");

            _edge ??= new EdgeCdp(edgePath, Path.Combine(AppPaths.DataDir, "edge-profile"));
            await _edge.StartAsync(_items[0].Config.Url, ct, HeadlessArgs);
            Log($"Navegador conectado en segundo plano ({_edge?.Name ?? "navegador"}).", LogKind.Ok);

            NativeMethods.KeepAwake(true);
            _loop = LoopAsync(ct);

            _btnToggle.Text = Loc.T("■  DETENER", "■  STOP");
            _btnToggle.BackColor = Palette.Red;
            _btnToggle.ForeColor = Color.White;
            SetPill("● VIGILANDO", Palette.Green, Palette.GreenBg);
            Log($"Vigilando {_items.Count} producto(s) cada {EffectiveInterval} s{(_config.GentleMode ? " (modo suave)" : "")}. Sonido: {(_soundEnabled ? _config.SoundMode : "no")}.", LogKind.Info);
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

    private async Task RecoverBrowserAsync(CancellationToken ct)
    {
        try
        {
            Log("Watchdog: reiniciando el navegador y las pestañas…", LogKind.Warn);

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

            _edge?.KillBrowser();
            _edge = null;

            var edgePath = EdgeCdp.FindBrowser();
            var first = _items.FirstOrDefault(i => !i.Paused) ?? _items.FirstOrDefault();
            if (edgePath is null || first is null) return;

            _edge = new EdgeCdp(edgePath, Path.Combine(AppPaths.DataDir, "edge-profile"));
            await _edge.StartAsync(first.Config.Url, ct, HeadlessArgs);
            Log("Watchdog: navegador reiniciado.", LogKind.Ok);
        }
        catch (Exception ex)
        {
            Log("Watchdog: no pude reiniciar el navegador: " + ex.Message, LogKind.Error);
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

        _btnToggle.Text = Loc.T("▶  INICIAR", "▶  START");
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
            if (!InSchedule())
            {
                if (!_schedulePaused)
                {
                    _schedulePaused = true;
                    SetPill("● FUERA DE HORARIO", Palette.Muted, Palette.Chip);
                    Log("Fuera del horario configurado: vigilancia en pausa.", LogKind.Info);
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), ct);
                }
                catch
                {
                    break;
                }

                continue;
            }

            if (_schedulePaused)
            {
                _schedulePaused = false;
                if (_cts is not null) SetPill("● VIGILANDO", Palette.Green, Palette.GreenBg);
                Log("Dentro del horario: reanudando vigilancia.", LogKind.Info);
            }

            var started = DateTime.UtcNow;
            try
            {
                var items = _items.Where(i => !i.Paused).ToList();
                if (items.Count == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(EffectiveInterval), ct);
                    continue;
                }

                foreach (var item in items)
                {
                    if (item.Page is not null && item.Page.IsOpen) continue;
                    item.Page = await _edge!.NewTabAsync(item.Config.Url, ct);
                    await item.Page.EnableFastModeAsync(ct);
                    await item.Page.WaitForAsync("document.readyState === 'complete'", 30000, ct);
                    await Task.Delay(1000, ct);
                    item.FreshlyLoaded = true;
                    Log($"Pestaña lista: {item.DisplayName}", LogKind.Info);
                }

                var tasks = items.Select(item => CheckItemAsync(item, ct));
                var results = await Task.WhenAll(tasks);

                _checks++;
                _lastCheckUtc = DateTime.UtcNow;
                await ApplyResultsAsync(results, ct);

                if (_failStreak == 3)
                    await RecoverBrowserAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _failStreak++;
                Log($"Fallo de comprobación ({_failStreak}): {ex.Message}", LogKind.Error);
                if (_failStreak == 3)
                    await RecoverBrowserAsync(ct);
                if (_failStreak == 20)
                    Log("⚠ 20 fallos seguidos: revisa que el navegador siga abierto y sin bloqueos.", LogKind.Warn);
            }

            var elapsed = (DateTime.UtcNow - started).TotalSeconds;
            var wait = Math.Max(1, EffectiveInterval - elapsed);

            if (_failStreak > 0)
            {
                var backoff = Math.Min(180, EffectiveInterval * Math.Pow(1.6, Math.Min(_failStreak, 5)));
                if (backoff > wait)
                {
                    wait = backoff;
                    if (_failStreak is 1 or 3 or 5)
                        Log($"Backoff activo: siguiente intento en {Math.Round(wait)} s ({_failStreak} fallo(s) seguidos).", LogKind.Warn);
                }
            }
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

            // 1) Modo API: consulta directa sin recargar la ficha
            if (item.ApiStoreId is not null && item.ApiProductId is not null)
            {
                try
                {
                    var pollScript = MonitorScripts.PollApiScript(
                        item.ApiStoreId, item.ApiProductId, item.ApiColorId ?? "", item.Sizes);
                    var pollValue = await item.Page.EvaluateAsync(pollScript, 20000, ct);
                    var pollPayload = ParsePayload(pollValue?.GetString());
                    if (pollPayload is { Ok: true }) return (item, pollPayload, null);
                }
                catch
                {
                    // se reintenta con resolución completa
                }

                item.ApiStoreId = null;
                item.ApiProductId = null;
                item.ApiColorId = null;
            }

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

            // 2) Resolver la API de la ficha (funciona aunque la web no pinte las tallas)
            await item.Page.WaitForAsync("document.readyState === 'complete'", 20000, ct);
            await Task.Delay(600, ct);

            string? apiReason = null;
            CheckPayload? resolved = null;

            for (var attempt = 0; attempt < 2; attempt++)
            {
                if (attempt > 0)
                    await Task.Delay(1500, ct);

                try
                {
                    var resolveValue = await item.Page.EvaluateAsync(
                        MonitorScripts.ResolveApiScript(item.ColorId), 30000, ct);
                    resolved = ParsePayload(resolveValue?.GetString());

                    if (resolved is { Ok: true })
                        break;

                    apiReason = resolved?.Reason ?? "sin-respuesta";
                }
                catch (Exception ex)
                {
                    apiReason = ex.Message;
                }
            }

            if (resolved is { Ok: true })
            {
                item.ApiStoreId = resolved.StoreId;
                item.ApiProductId = resolved.ProductId;
                item.ApiColorId = resolved.ColorId;

                if (string.IsNullOrWhiteSpace(item.ImageUrl) && !string.IsNullOrWhiteSpace(resolved.Image))
                    item.ImageUrl = resolved.Image;

                if (item.Sizes.Length > 0)
                {
                    var map = resolved.Sizes
                        .GroupBy(s => s.Size, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                    resolved.Sizes = item.Sizes
                        .Select(w => map.TryGetValue(w, out var found)
                            ? found
                            : new SizeResult { Size = w, Visibility = "NO_EXISTE" })
                        .ToList();
                }

                return (item, resolved, null);
            }

            // 3) Fallback: leer tallas del DOM
            var ready = await item.Page.WaitForAsync(MonitorScripts.ReadyScript(), 20000, ct);
            if (!ready)
                return (item, null, $"la página no cargó (api: {apiReason})");

            await Task.Delay(600, ct);

            var value = await item.Page.EvaluateAsync(MonitorScripts.SizeScript(item.Sizes), 25000, ct);
            var json = value?.GetString();
            if (string.IsNullOrEmpty(json))
                return (item, null, "respuesta vacía");

            var payload = ParsePayload(json);
            return payload is null ? (item, null, "respuesta no válida") : (item, payload, null);
        }
        catch (Exception ex)
        {
            return (item, null, ex.Message);
        }
    }

    private static CheckPayload? ParsePayload(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<CheckPayload>(json, JsonOpts);
        }
        catch
        {
            return null;
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
                parts.Add($"{item.DisplayName}: error ({item.LastError})");
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
                    Loc.T("tallas ", "sizes ") + string.Join(",", item.Sizes),
                    string.IsNullOrWhiteSpace(item.PriceText) ? null : item.PriceText
                }.Where(x => x is not null));

            UpdateRow(item);
            parts.Add($"{item.DisplayName}: {item.StatusText}");

            if (wasAvailable != item.Available)
                AppendHistory(item);

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

        var okCount = results.Count(r => r.Payload is { Ok: true });
        _failStreak = okCount > 0 ? 0 : _failStreak + 1;

        Log($"#{_checks:0000} · {string.Join(" · ", parts)}", LogKind.Check);
        UpdateFooter();
    }

    private async Task OnStockAsync(MonitorItem item, bool transition)
    {
        if (DateTime.UtcNow.Ticks < _snoozeUntilTicks)
        {
            var until = new DateTime(_snoozeUntilTicks, DateTimeKind.Utc).ToLocalTime();
            Log($"Aviso omitido: snooze activo hasta {until:HH:mm} ({item.DisplayName})", LogKind.Check);
            return;
        }

        var detail = string.Join(" · ",
            new[]
            {
                string.IsNullOrWhiteSpace(item.ColorName) ? null : item.ColorName,
                Loc.T("tallas ", "sizes ") + string.Join(",", item.Sizes)
            }.Where(x => x is not null));

        var sizesAvailable = item.StatusText;
        Log($"🚨 STOCK · {item.DisplayName} · {sizesAvailable} {(string.IsNullOrEmpty(item.PriceText) ? "" : "· " + item.PriceText)}", LogKind.Alert);

        ShowAlert(new AlertItem(item.DisplayName, detail + " · " + sizesAvailable, item.PriceText, item.Config.Url, item.ImageUrl), test: false);
        _alarm.Alarm($"¡Atención! Hay stock en {item.DisplayName}. Pulsa el botón grande cuando lo veas.");
        SendPush(item);

        void Ui()
        {
            SetPill("● ¡HAY STOCK!", Color.White, Palette.Red);
            _btnSilence.Enabled = _soundEnabled;
            _alertReset.Stop();
            _alertReset.Start();
            NativeMethods.Flash(Handle);
            _lastAlertUrl = item.Config.Url;
            SetTrayAlert(true);
            _tray.ShowBalloonTip(15000,
                Loc.T("¡HAY STOCK!", "IN STOCK!"),
                $"{item.DisplayName}\n" + Loc.T("Haz clic aquí para abrir la ficha.", "Click here to open the product."),
                ToolTipIcon.Warning);
        }

        if (InvokeRequired) BeginInvoke(new Action(Ui));
        else Ui();

        if (transition && _config.OpenInBrave)
            OpenInBrave(item.Config.Url, moveToAlertScreen: true);

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
                form.Bought += (_, _) =>
                {
                    PauseUrls(form.Urls);
                    _alarm.Silence();
                };
                form.OpenRequested += (_, _) =>
                {
                    foreach (var url in form.Urls.Distinct(StringComparer.OrdinalIgnoreCase))
                        OpenInBrave(url, moveToAlertScreen: true);

                    _alarm.Silence();
                    _btnSilence.Enabled = false;
                };
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
            if (!_alertForm.Visible)
            {
                PositionAlert(_alertForm);
                _alertForm.Show();
            }
            if (_alertForm.WindowState == FormWindowState.Minimized)
                _alertForm.WindowState = FormWindowState.Normal;
            _alertForm.Activate();
            _alertForm.BringToFront();
        }

        if (InvokeRequired) BeginInvoke(new Action(Ui));
        else Ui();
    }

    private async Task ClearCacheAsync()
    {
        var wasRunning = _cts is not null;
        if (wasRunning) await StopMonitorAsync();

        try { _edge?.KillBrowser(); } catch { }
        _edge = null;

        var path = Path.Combine(AppPaths.DataDir, "edge-profile");
        var deleted = false;

        for (var attempt = 0; attempt < 6; attempt++)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
                deleted = !Directory.Exists(path);
                if (deleted) break;
            }
            catch
            {
                // archivos en uso: reintentamos
            }

            await Task.Delay(500);
        }

        Log(deleted ? "Caché del navegador borrada." : "No pude borrar toda la caché (archivos en uso).",
            deleted ? LogKind.Ok : LogKind.Warn);

        if (wasRunning) await StartMonitorAsync();
    }

    private void PositionAlert(Form form)
    {
        var screens = Screen.AllScreens;
        var index = _config.AlertScreen;
        var screen = index >= 0 && index < screens.Length ? screens[index] : Screen.PrimaryScreen;
        if (screen is null) return;

        var area = screen.WorkingArea;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(
            area.Left + Math.Max(0, (area.Width - form.Width) / 2),
            area.Top + Math.Max(0, (area.Height - form.Height) / 2));
    }

    private void OnAlertConfirmed(object? sender, EventArgs e)
    {
        var test = _alertTestMode;
        _alertTestMode = false;
        _alertForm = null;

        _alarm.Silence();
        _btnSilence.Enabled = false;
        _muteUntilTicks = DateTime.UtcNow.AddMinutes(5).Ticks;
        SetTrayAlert(false);

        if (test)
        {
            Log("Prueba de aviso confirmada.", LogKind.Info);
            return;
        }

        var urls = (sender as AlertForm)?.Urls ?? Array.Empty<string>();
        if (urls.Count > 0)
        {
            foreach (var url in urls.Distinct(StringComparer.OrdinalIgnoreCase))
                OpenInBrave(url, moveToAlertScreen: true);

            Log("Aviso confirmado: abriendo la ficha en Brave. ¡Suerte!", LogKind.Ok);
        }
        else
        {
            _focusBrave.Stop();
            _focusBrave.Start();
            Log("Aviso confirmado. ¡Suerte!", LogKind.Ok);
        }
    }

    private void TestAlert()
    {
        var sample = _items.FirstOrDefault();
        var detail = sample is null ? "Azul lavado · tallas 44" : sample.Subtitle;
        var item = new AlertItem("PRUEBA · " + (sample?.DisplayName ?? "Jeans barrel"), detail, "17,99 €",
            sample?.Config.Url ?? "https://www.bershka.com/", sample?.ImageUrl);
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

    private void SendPush(MonitorItem item)
    {
        var topic = (_config.NtfyTopic ?? "").Trim();
        if (topic.Length == 0) return;

        _ = Task.Run(async () =>
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                using var request = new HttpRequestMessage(
                    HttpMethod.Post, "https://ntfy.sh/" + Uri.EscapeDataString(topic));

                request.Headers.TryAddWithoutValidation("Title", "Stock: " + item.DisplayName);
                request.Headers.TryAddWithoutValidation("Priority", "urgent");
                request.Headers.TryAddWithoutValidation("Tags", "rotating_light");
                request.Content = new StringContent(
                    $"{item.StatusText}\n{item.PriceText}\n{item.Config.Url}".Trim());

                await http.SendAsync(request);
                Log("Aviso push enviado a ntfy.", LogKind.Info);
            }
            catch (Exception ex)
            {
                Log("No pude enviar el aviso push: " + ex.Message, LogKind.Warn);
            }
        });
    }

    private void OpenInBrave(string url, bool moveToAlertScreen = false)
    {
        try
        {
            var brave = FindBrave();
            if (brave is not null)
                Process.Start(new ProcessStartInfo(brave, $"\"{url}\"") { UseShellExecute = false });
            else
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

            Log("Pestaña abierta en Brave: " + url, LogKind.Ok);
            _moveBraveToScreen = moveToAlertScreen;
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
        text = Loc.T(text);
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
        var version = typeof(MainForm).Assembly.GetName().Version?.ToString(3) ?? "?";
        var update = _updateTag is null ? "" : $" · NUEVA v{_updateTag}";
        var text = $"v{version}{update} · PC sin suspensión · última comprobación {last} · cada {EffectiveInterval} s (mín. {(_allowFast ? 5 : 15)} s) · " +
                   $"{_items.Count} producto(s) · comprobaciones {_checks} · fallos {_failStreak} · " +
                   $"sonido {(_soundEnabled ? "on" : "off")} · Brave {(FindBrave() is null ? "no" : "ok")} · navegador {(_edge is null ? "—" : (_edge.Name))}";

        void Apply() => _foot.Text = text;
        if (InvokeRequired) BeginInvoke(new Action(Apply));
        else Apply();

        var available = _items.Count(i => i.Available);
        var tip = Loc.T($"Inditex · {_items.Count} producto(s) · {available} disponible(s)", $"Inditex · {_items.Count} product(s) · {available} available");
        if (tip.Length > 63) tip = tip[..63];
        try
        {
            _tray.Text = tip;
        }
        catch
        {
            // ignored
        }
    }

    private void AppendHistory(MonitorItem item)
    {
        try
        {
            Directory.CreateDirectory(_logDir);
            var path = Path.Combine(_logDir, "historial.csv");
            var utf8 = new UTF8Encoding(true);

            if (!File.Exists(path))
                File.AppendAllText(path,
                    "fecha;hora;producto;tallas;estado;color;url" + Environment.NewLine, utf8);

            var line = string.Join(";", new[]
            {
                DateTime.Now.ToString("yyyy-MM-dd"),
                DateTime.Now.ToString("HH:mm:ss"),
                Clean(item.DisplayName),
                Clean(string.Join("/", item.Sizes)),
                item.Available ? "DISPONIBLE" : "AGOTADA",
                Clean(item.ColorName),
                Clean(item.Config.Url)
            });

            File.AppendAllText(path, line + Environment.NewLine, utf8);
            Log($"Cambio de stock: {item.DisplayName} · {(item.Available ? "DISPONIBLE" : "agotada")}",
                item.Available ? LogKind.Ok : LogKind.Info);
        }
        catch
        {
            // el historial es opcional
        }
    }

    private static string Clean(string? text) =>
        (text ?? "").Replace(';', ',').Replace('\r', ' ').Replace('\n', ' ');

    private void Log(string message, LogKind kind)
    {        var line = $"{DateTime.Now:HH:mm:ss}  {message}";
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
                LogKind.Alert => Palette.Red,
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

    private sealed record ItemRow(Label Name, Label Sub, Label Mid, Label Status, Panel Panel, Button Menu, PictureBox Thumb);

    private sealed class RowGroup
    {
        public required string Key { get; init; }
        public required List<MonitorItem> Members { get; init; }
        public required ItemRow Ui { get; init; }
    }

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
        public static Color Bg = Color.FromArgb(15, 17, 22);
        public static Color Card = Color.FromArgb(23, 26, 33);
        public static Color CardBorder = Color.FromArgb(38, 43, 56);
        public static Color Text = Color.FromArgb(232, 235, 242);
        public static Color Muted = Color.FromArgb(138, 147, 168);
        public static Color Green = Color.FromArgb(64, 214, 128);
        public static Color GreenBg = Color.FromArgb(17, 43, 29);
        public static Color Red = Color.FromArgb(255, 77, 109);
        public static Color RedBg = Color.FromArgb(61, 17, 28);
        public static Color Amber = Color.FromArgb(246, 173, 60);
        public static Color AmberBg = Color.FromArgb(52, 36, 12);
        public static Color Chip = Color.FromArgb(30, 34, 44);
        public static Color LogBg = Color.FromArgb(11, 13, 17);
        public static Color Blue = Color.FromArgb(96, 165, 250);

        public static void Apply(string? theme)
        {
            var light = string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase);

            if (light)
            {
                Bg = Color.FromArgb(245, 247, 251);
                Card = Color.White;
                CardBorder = Color.FromArgb(214, 220, 230);
                Text = Color.FromArgb(26, 29, 36);
                Muted = Color.FromArgb(95, 104, 120);
                Green = Color.FromArgb(16, 150, 80);
                GreenBg = Color.FromArgb(220, 245, 230);
                Red = Color.FromArgb(200, 40, 65);
                RedBg = Color.FromArgb(252, 228, 232);
                Amber = Color.FromArgb(170, 110, 10);
                AmberBg = Color.FromArgb(252, 240, 210);
                Chip = Color.FromArgb(233, 237, 244);
                LogBg = Color.White;
                Blue = Color.FromArgb(30, 110, 220);
            }
            else
            {
                Bg = Color.FromArgb(15, 17, 22);
                Card = Color.FromArgb(23, 26, 33);
                CardBorder = Color.FromArgb(38, 43, 56);
                Text = Color.FromArgb(232, 235, 242);
                Muted = Color.FromArgb(138, 147, 168);
                Green = Color.FromArgb(64, 214, 128);
                GreenBg = Color.FromArgb(17, 43, 29);
                Red = Color.FromArgb(255, 77, 109);
                RedBg = Color.FromArgb(61, 17, 28);
                Amber = Color.FromArgb(246, 173, 60);
                AmberBg = Color.FromArgb(52, 36, 12);
                Chip = Color.FromArgb(30, 34, 44);
                LogBg = Color.FromArgb(11, 13, 17);
                Blue = Color.FromArgb(96, 165, 250);
            }
        }
    }
}
