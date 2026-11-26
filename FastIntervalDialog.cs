namespace InditexStockAlarm;

public sealed class FastIntervalDialog : Form
{
    public FastIntervalDialog()
    {
        Text = "Aviso: bajar del mínimo recomendado";
        ClientSize = new Size(660, 400);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(38, 12, 18);
        ForeColor = Color.FromArgb(255, 220, 226);
        Font = new Font("Segoe UI", 9.75f);

        Controls.Add(new Label
        {
            Text = "⚠  Bajar de 15 segundos",
            Font = new Font("Segoe UI Black", 19f),
            ForeColor = Color.FromArgb(255, 77, 109),
            AutoSize = true,
            Location = new Point(28, 22)
        });

        Controls.Add(new Label
        {
            Text =
                "El mínimo de 15 s está puesto para no abusar de la web.\n\n" +
                "Si lo desbloqueas y bajas el intervalo:\n\n" +
                "   •  se multiplican las peticiones a la web\n" +
                "   •  pueden limitar o bloquear temporalmente tu conexión\n" +
                "   •  el monitor puede empezar a fallar y perder el reestock\n" +
                "   •  no hay ninguna garantía de que funcione mejor\n\n" +
                "Es tu responsabilidad. ¿Quieres desbloquearlo igualmente?",
            Font = new Font("Segoe UI", 10f),
            ForeColor = Color.FromArgb(255, 190, 200),
            AutoSize = false,
            Size = new Size(604, 240),
            Location = new Point(30, 74)
        });

        var accept = new Button
        {
            Text = "Entiendo el riesgo: desbloquear",
            Location = new Point(30, 330),
            Size = new Size(360, 46),
            BackColor = Color.FromArgb(255, 77, 109),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 10.5f),
            Cursor = Cursors.Hand
        };
        accept.FlatAppearance.BorderSize = 0;
        accept.Click += (_, _) => DialogResult = DialogResult.OK;

        var cancel = new Button
        {
            Text = "Cancelar",
            Location = new Point(404, 330),
            Size = new Size(226, 46),
            BackColor = Color.FromArgb(60, 24, 34),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10f),
            Cursor = Cursors.Hand
        };
        cancel.FlatAppearance.BorderSize = 0;
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;

        Controls.Add(accept);
        Controls.Add(cancel);
        CancelButton = cancel;
    }
}
