namespace InditexStockAlarm;

public sealed class EditItemDialog : Form
{
    private readonly TextBox _sizes;
    private readonly TextBox _name;

    public string[] SelectedSizes { get; private set; } = Array.Empty<string>();
    public string ItemName => _name.Text.Trim();

    public EditItemDialog(MonitorItem item)
    {
        Text = "Editar producto";
        ClientSize = new Size(620, 300);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(20, 23, 30);
        ForeColor = Color.FromArgb(232, 235, 242);
        Font = new Font("Segoe UI", 9.75f);

        Controls.Add(new Label
        {
            Text = "Editar producto",
            Font = new Font("Segoe UI Semibold", 14f),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(24, 18)
        });

        Controls.Add(new Label
        {
            Text = item.Config.Url,
            ForeColor = Color.FromArgb(138, 147, 168),
            AutoSize = false,
            AutoEllipsis = true,
            Location = new Point(26, 52),
            Size = new Size(568, 18),
            Font = new Font("Consolas", 8.5f)
        });

        Controls.Add(new Label
        {
            Text = "Tallas (separadas por comas)",
            ForeColor = Color.FromArgb(232, 235, 242),
            AutoSize = true,
            Location = new Point(26, 88)
        });

        _sizes = new TextBox
        {
            Location = new Point(26, 112),
            Width = 220,
            BackColor = Color.FromArgb(30, 34, 44),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 10f),
            Text = string.Join(",", item.Sizes)
        };

        Controls.Add(new Label
        {
            Text = "Nombre (opcional)",
            ForeColor = Color.FromArgb(232, 235, 242),
            AutoSize = true,
            Location = new Point(266, 88)
        });

        _name = new TextBox
        {
            Location = new Point(266, 112),
            Width = 328,
            BackColor = Color.FromArgb(30, 34, 44),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Text = item.Config.Name
        };

        Controls.Add(_sizes);
        Controls.Add(_name);
        Controls.Add(new Label
        {
            Text = "Ejemplos: 42,44,46 · M,L,XL · Única",
            ForeColor = Color.FromArgb(138, 147, 168),
            Font = new Font("Segoe UI", 8.5f),
            AutoSize = true,
            Location = new Point(26, 146)
        });

        var ok = new Button
        {
            Text = "Guardar",
            Location = new Point(454, 230),
            Size = new Size(140, 42),
            BackColor = Color.FromArgb(64, 214, 128),
            ForeColor = Color.FromArgb(8, 28, 17),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 10f),
            Cursor = Cursors.Hand
        };
        ok.FlatAppearance.BorderSize = 0;
        ok.Click += (_, _) => TryAccept();

        var cancel = new Button
        {
            Text = "Cancelar",
            Location = new Point(344, 230),
            Size = new Size(100, 42),
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
        ActiveControl = _sizes;
        Loc.Apply(this);
    }

    private void TryAccept()
    {
        var sizes = ItemUrl.ParseSizes(_sizes.Text);
        if (sizes.Length == 0)
        {
            MessageBox.Show(this, "Escribe al menos una talla.", "Falta la talla",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SelectedSizes = sizes;
        DialogResult = DialogResult.OK;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Ui.CenterOnOwner(this);
    }
}
