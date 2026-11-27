namespace InditexStockAlarm;

public sealed class SettingsDialog : Form
{
    private static readonly string[] SoundValues = { "both", "siren", "voice", "beep", "custom" };
    private readonly string _originalTheme;

    private readonly ComboBox _sound;
    private readonly TextBox _ntfy;
    private readonly Label _ntfyStatus;
    private readonly CheckBox _schedule;
    private readonly TextBox _start;
    private readonly TextBox _end;
    private readonly CheckBox _gentle;
    private readonly ComboBox _theme;
    private readonly ComboBox _screen;
    private readonly CheckBox _startup;
    private readonly CheckBox _startMinimized;
    private readonly ComboBox _scale;
    private readonly Button _customButton;
    private readonly Label _customLabel;
    private string _customPath = "";

    public string SoundMode => SoundValues[Math.Max(0, _sound.SelectedIndex)];
    public string NtfyTopic => _ntfy.Text.Trim();
    public bool ScheduleEnabled => _schedule.Checked;
    public string ScheduleStart => _start.Text.Trim();
    public string ScheduleEnd => _end.Text.Trim();
    public bool GentleMode => _gentle.Checked;
    public string Theme => _theme.SelectedIndex == 1 ? "light" : "dark";
    public int AlertScreen => _screen.SelectedIndex <= 0 ? -1 : _screen.SelectedIndex - 1;
    public bool ThemeChanged => !string.Equals(Theme, _originalTheme, StringComparison.OrdinalIgnoreCase);
    public bool StartWithWindows => _startup.Checked;
    public bool StartMinimized => _startMinimized.Checked;
    public int UiScale => _scale.SelectedIndex == 2 ? 150 : _scale.SelectedIndex == 1 ? 125 : 100;
    public string CustomSoundPath => _customPath;

    public SettingsDialog(AppConfig config, bool startWithWindows = false)
    {
        _originalTheme = config.Theme;

        Text = "Ajustes";
        ClientSize = new Size(700, 660);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(20, 23, 30);
        ForeColor = Color.FromArgb(232, 235, 242);
        Font = new Font("Segoe UI", 9.75f);

        var y = 18;

        Controls.Add(Header("Sonido de la alarma", ref y));

        _sound = new ComboBox
        {
            Location = new Point(30, y),
            Width = 300,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(30, 34, 44),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _customPath = config.CustomSoundPath;
        _sound.Items.AddRange(new object[] { "Sirena + voz", "Solo sirena", "Solo voz", "Solo pitido", "Archivo personalizado" });
        _sound.SelectedIndex = Array.IndexOf(SoundValues, config.SoundMode);
        if (_sound.SelectedIndex < 0) _sound.SelectedIndex = 0;
        Controls.Add(_sound);

        _customButton = new Button
        {
            Text = "Elegir…",
            Location = new Point(300, y - 1),
            Size = new Size(110, 26),
            BackColor = Color.FromArgb(30, 34, 44),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _customButton.FlatAppearance.BorderSize = 0;
        _customButton.Click += (_, _) => PickCustomSound();
        Controls.Add(_customButton);

        _customLabel = new Label
        {
            Text = "",
            ForeColor = Color.FromArgb(138, 147, 168),
            AutoSize = false,
            AutoEllipsis = true,
            Location = new Point(30, y + 30),
            Size = new Size(620, 18)
        };
        Controls.Add(_customLabel);
        UpdateCustomLabel();

        y += 62;

        Controls.Add(Header("Aviso al móvil (ntfy.sh)", ref y));

        _ntfy = new TextBox
        {
            Location = new Point(30, y),
            Width = 400,
            BackColor = Color.FromArgb(30, 34, 44),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Text = config.NtfyTopic,
            PlaceholderText = "tema secreto, ej. stock-7f3k9x2"
        };

        var test = new Button
        {
            Text = "Probar push",
            Location = new Point(442, y - 1),
            Size = new Size(150, 26),
            BackColor = Color.FromArgb(30, 34, 44),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        test.FlatAppearance.BorderSize = 0;
        test.Click += async (_, _) => await TestPushAsync();

        _ntfyStatus = new Label
        {
            Location = new Point(30, y + 28),
            Size = new Size(620, 18),
            ForeColor = Color.FromArgb(138, 147, 168),
            AutoEllipsis = true
        };

        Controls.Add(_ntfy);
        Controls.Add(test);
        Controls.Add(_ntfyStatus);
        y += 54;

        Controls.Add(Header("Horario de vigilancia", ref y));

        _schedule = new CheckBox
        {
            Text = "Vigilar solo dentro del horario",
            Checked = config.ScheduleEnabled,
            AutoSize = true,
            ForeColor = Color.White,
            Location = new Point(30, y)
        };

        _start = TimeBox(config.ScheduleStart, 350, y);
        _end = TimeBox(config.ScheduleEnd, 480, y);

        Controls.Add(_schedule);
        Controls.Add(_start);
        Controls.Add(_end);
        Controls.Add(new Label
        {
            Text = "desde",
            AutoSize = true,
            ForeColor = Color.FromArgb(138, 147, 168),
            Location = new Point(300, y + 3)
        });
        Controls.Add(new Label
        {
            Text = "hasta",
            AutoSize = true,
            ForeColor = Color.FromArgb(138, 147, 168),
            Location = new Point(432, y + 3)
        });
        Controls.Add(new Label
        {
            Text = "24:00 = fin del día",
            AutoSize = true,
            ForeColor = Color.FromArgb(110, 118, 134),
            Font = new Font("Segoe UI", 8f),
            Location = new Point(560, y + 4)
        });
        y += 40;

        _gentle = new CheckBox
        {
            Text = "Modo suave: mínimo 45 s entre comprobaciones (menos riesgo de bloqueo)",
            Checked = config.GentleMode,
            AutoSize = true,
            ForeColor = Color.White,
            Location = new Point(30, y)
        };
        Controls.Add(_gentle);
        y += 40;

        Controls.Add(Header("Apariencia", ref y));

        _theme = new ComboBox
        {
            Location = new Point(30, y),
            Width = 180,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(30, 34, 44),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _theme.Items.AddRange(new object[] { "Oscuro", "Claro" });
        _theme.SelectedIndex = string.Equals(config.Theme, "light", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        Controls.Add(_theme);
        Controls.Add(new Label
        {
            Text = "El cambio de tema se aplica al reiniciar la app.",
            AutoSize = true,
            ForeColor = Color.FromArgb(138, 147, 168),
            Location = new Point(224, y + 4)
        });

        y += 36;

        Controls.Add(Header("Escala de la interfaz", ref y));

        _scale = new ComboBox
        {
            Location = new Point(30, y),
            Width = 120,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(30, 34, 44),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _scale.Items.AddRange(new object[] { "100 %", "125 %", "150 %" });
        _scale.SelectedIndex = config.UiScale >= 150 ? 2 : config.UiScale >= 125 ? 1 : 0;
        Controls.Add(_scale);
        Controls.Add(new Label
        {
            Text = "Se aplica al reiniciar la app.",
            AutoSize = true,
            ForeColor = Color.FromArgb(138, 147, 168),
            Location = new Point(166, y + 4)
        });

        y += 36;

        Controls.Add(Header("Pantalla de la alerta", ref y));

        _screen = new ComboBox
        {
            Location = new Point(30, y),
            Width = 320,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(30, 34, 44),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _screen.Items.Add("Predeterminada");

        var screens = Screen.AllScreens;
        for (var i = 0; i < screens.Length; i++)
        {
            var s = screens[i];
            _screen.Items.Add($"Pantalla {i + 1}{(s.Primary ? " · principal" : "")} ({s.Bounds.Width}×{s.Bounds.Height})");
        }

        _screen.SelectedIndex = config.AlertScreen >= 0 && config.AlertScreen < screens.Length
            ? config.AlertScreen + 1
            : 0;

        Controls.Add(_screen);
        Controls.Add(new Label
        {
            Text = $"Detectadas {screens.Length} pantalla(s). La ventana saldrá centrada.",
            AutoSize = true,
            ForeColor = Color.FromArgb(138, 147, 168),
            Location = new Point(360, y + 4)
        });

        _startup = new CheckBox
        {
            Text = "Iniciar con Windows",
            Checked = startWithWindows,
            AutoSize = true,
            ForeColor = Color.White,
            Location = new Point(30, y + 38)
        };
        Controls.Add(_startup);

        _startMinimized = new CheckBox
        {
            Text = "Arrancar minimizada",
            Checked = config.StartMinimized,
            AutoSize = true,
            ForeColor = Color.White,
            Location = new Point(250, y + 38)
        };
        Controls.Add(_startMinimized);

        var ok = new Button
        {
            Text = "Guardar",
            Location = new Point(470, 594),
            Size = new Size(200, 44),
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
            Location = new Point(360, 594),
            Size = new Size(100, 44),
            BackColor = Color.FromArgb(30, 34, 44),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        cancel.FlatAppearance.BorderSize = 0;
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;

        Controls.Add(ok);
        Controls.Add(cancel);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private static Label Header(string text, ref int y)
    {
        var label = new Label
        {
            Text = text.ToUpperInvariant(),
            Font = new Font("Segoe UI Semibold", 8.5f),
            ForeColor = Color.FromArgb(138, 147, 168),
            AutoSize = true,
            Location = new Point(30, y)
        };
        y += 26;
        return label;
    }

    private static TextBox TimeBox(string value, int x, int y) => new()
    {
        Text = value,
        Location = new Point(x, y - 2),
        Width = 70,
        BackColor = Color.FromArgb(30, 34, 44),
        ForeColor = Color.White,
        BorderStyle = BorderStyle.FixedSingle,
        Font = new Font("Consolas", 10f)
    };

    private void PickCustomSound()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Elegir sonido de alarma",
            Filter = "Audio (*.wav;*.mp3;*.wma)|*.wav;*.mp3;*.wma|Todos los archivos (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        _customPath = dialog.FileName;
        _sound.SelectedIndex = 4;
        UpdateCustomLabel();
    }

    private void UpdateCustomLabel()
    {
        _customLabel.Text = string.IsNullOrWhiteSpace(_customPath)
            ? "Sin archivo: elige un WAV, MP3 o WMA."
            : Path.GetFileName(_customPath);
    }

    private async Task TestPushAsync()
    {
        var topic = _ntfy.Text.Trim();
        if (topic.Length == 0)
        {
            _ntfyStatus.ForeColor = Color.FromArgb(255, 120, 140);
            _ntfyStatus.Text = "Escribe un tema primero.";
            return;
        }

        _ntfyStatus.ForeColor = Color.FromArgb(246, 173, 60);
        _ntfyStatus.Text = "Enviando…";

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            using var request = new HttpRequestMessage(
                HttpMethod.Post, "https://ntfy.sh/" + Uri.EscapeDataString(topic));
            request.Headers.TryAddWithoutValidation("Title", "Inditex Stock Alarm");
            request.Headers.TryAddWithoutValidation("Priority", "default");
            request.Headers.TryAddWithoutValidation("Tags", "bell");
            request.Content = new StringContent("Prueba de aviso desde Inditex Stock Alarm.");

            var response = await http.SendAsync(request);
            _ntfyStatus.ForeColor = response.IsSuccessStatusCode
                ? Color.FromArgb(64, 214, 128)
                : Color.FromArgb(255, 120, 140);
            _ntfyStatus.Text = response.IsSuccessStatusCode
                ? "Enviado. Mira el móvil."
                : "Error HTTP " + (int)response.StatusCode;
        }
        catch (Exception ex)
        {
            _ntfyStatus.ForeColor = Color.FromArgb(255, 120, 140);
            _ntfyStatus.Text = "Error: " + ex.Message;
        }
    }

    private void TryAccept()
    {
        if (SoundMode == "custom" && (string.IsNullOrWhiteSpace(_customPath) || !File.Exists(_customPath)))
        {
            MessageBox.Show(this, "Elige un archivo de sonido válido o cambia el modo de sonido.",
                "Sonido personalizado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_schedule.Checked &&
            (!TimeSpan.TryParse(_start.Text, out _) || !TimeSpan.TryParse(_end.Text, out _)))
        {
            MessageBox.Show(this, "El horario debe tener formato HH:mm (por ejemplo 08:00 y 24:00).",
                "Horario no válido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult = DialogResult.OK;
    }
}
