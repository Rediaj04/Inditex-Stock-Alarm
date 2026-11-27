using System.Diagnostics;

namespace InditexStockAlarm;

public sealed class UpdateDialog : Form
{
    public UpdateDialog(string version, string url)
    {
        Text = Loc.T("Nueva versión disponible", "New version available");
        ClientSize = new Size(560, 260);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(20, 23, 30);
        ForeColor = Color.FromArgb(232, 235, 242);
        Font = new Font("Segoe UI", 9.75f);

        Controls.Add(new Label
        {
            Text = Loc.T($"Nueva versión v{version} disponible", $"New version v{version} available"),
            Font = new Font("Segoe UI Semibold", 14f),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(24, 22)
        });

        Controls.Add(new Label
        {
            Text = "Hay una actualización en GitHub. No es obligatorio actualizar:\n" +
                   "puedes seguir usando esta versión si te va bien.",
            ForeColor = Color.FromArgb(138, 147, 168),
            AutoSize = false,
            Size = new Size(512, 60),
            Location = new Point(26, 60)
        });

        var open = new Button
        {
            Text = "Abrir descarga",
            Location = new Point(300, 180),
            Size = new Size(232, 44),
            BackColor = Color.FromArgb(64, 214, 128),
            ForeColor = Color.FromArgb(8, 28, 17),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 10f),
            Cursor = Cursors.Hand
        };
        open.FlatAppearance.BorderSize = 0;
        open.Click += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
                // si falla, al menos cerramos
            }

            Close();
        };

        var later = new Button
        {
            Text = "Ahora no",
            Location = new Point(190, 180),
            Size = new Size(100, 44),
            BackColor = Color.FromArgb(30, 34, 44),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        later.FlatAppearance.BorderSize = 0;
        later.Click += (_, _) => Close();

        Controls.Add(open);
        Controls.Add(later);
        AcceptButton = open;
        CancelButton = later;
    }
}
