namespace BershkaMonitor;

public sealed class AddItemDialog : Form
{
    private readonly TextBox _url;
    private readonly TextBox _sizes;
    private readonly TextBox _name;

    public ItemConfig? Result { get; private set; }

    public AddItemDialog()
    {
        Text = "Añadir producto";
        ClientSize = new Size(700, 360);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(20, 23, 30);
        ForeColor = Color.FromArgb(232, 235, 242);
        Font = new Font("Segoe UI", 9.75f);

        var title = new Label
        {
            Text = "Pega la URL de la ficha de Bershka",
            Font = new Font("Segoe UI Semibold", 13f),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(24, 18)
        };

        var hint = new Label
        {
            Text = "Ejemplo: https://www.bershka.com/es/jeans-barrel-c0p229733871.html?colorId=428\n" +
                   "El color se toma del parámetro colorId de la URL.",
            Font = new Font("Segoe UI", 8.75f),
            ForeColor = Color.FromArgb(138, 147, 168),
            AutoSize = true,
            Location = new Point(26, 46)
        };

        _url = new TextBox
        {
            Location = new Point(26, 94),
            Width = 648,
            BackColor = Color.FromArgb(30, 34, 44),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 9.5f)
        };

        _sizes = new TextBox
        {
            Location = new Point(26, 156),
            Width = 190,
            BackColor = Color.FromArgb(30, 34, 44),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 10f),
            Text = "44"
        };

        _name = new TextBox
        {
            Location = new Point(236, 156),
            Width = 438,
            BackColor = Color.FromArgb(30, 34, 44),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9.5f),
            PlaceholderText = "Nombre (opcional, se deduce de la URL)"
        };

        var sizesHint = new Label
        {
            Text = "Talla(s): puedes poner varias separadas por comas → 42,44,46",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(138, 147, 168),
            AutoSize = true,
            Location = new Point(26, 184)
        };

        var ok = new Button
        {
            Text = "Añadir y vigilar",
            Location = new Point(458, 288),
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
            Location = new Point(340, 288),
            Size = new Size(108, 44),
            BackColor = Color.FromArgb(30, 34, 44),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10f),
            Cursor = Cursors.Hand
        };
        cancel.FlatAppearance.BorderSize = 0;
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;

        Controls.AddRange(new Control[] { title, hint, _url, _sizes, _name, sizesHint, ok, cancel });
        AcceptButton = ok;
        CancelButton = cancel;
        ActiveControl = _url;
    }

    private void TryAccept()
    {
        var url = _url.Text.Trim();
        if (!ItemUrl.TryParse(url, out _, out var error))
        {
            MessageBox.Show(this, error, "URL no válida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _url.Focus();
            return;
        }

        var sizes = ItemUrl.ParseSizes(_sizes.Text);
        if (sizes.Length == 0)
        {
            MessageBox.Show(this, "Escribe al menos una talla (por ejemplo 44).", "Falta la talla",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _sizes.Focus();
            return;
        }

        Result = new ItemConfig
        {
            Url = url,
            Sizes = string.Join(",", sizes),
            Name = _name.Text.Trim()
        };
        DialogResult = DialogResult.OK;
    }
}
