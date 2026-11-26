using System.Diagnostics;

namespace InditexStockAlarm;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Any(a => a.Equals("--selftest", StringComparison.OrdinalIgnoreCase)))
        {
            return SelfTest.RunAsync(args).GetAwaiter().GetResult();
        }

        if (args.Any(a => a.Equals("--soundtest", StringComparison.OrdinalIgnoreCase)))
        {
            var player = new AlarmPlayer();
            player.Alarm("Prueba de alarma. Talla cuarenta y cuatro disponible en Inditex. Corre a comprar.");
            Thread.Sleep(6000);
            player.Silence();
            return 0;
        }

        if (args.Any(a => a.Equals("--alerttest", StringComparison.OrdinalIgnoreCase)))
        {
            ApplicationConfiguration.Initialize();

            var sample = new AlertItem(
                "PRUEBA · Jeans barrel",
                "Azul lavado · tallas 44",
                "17,99 €",
                "https://www.bershka.com/es/jeans-barrel-c0p229733871.html?colorId=428");

            var form = new AlertForm();
            form.AddItem(sample);

            var timer = new System.Windows.Forms.Timer { Interval = 7000 };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                try
                {
                    using var bmp = new Bitmap(form.Width, form.Height);
                    form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
                    bmp.Save(Path.Combine(AppContext.BaseDirectory, "alert-preview.png"));
                }
                catch
                {
                    // ignored
                }

                form.Close();
            };
            form.Shown += (_, _) => timer.Start();

            Application.Run(form);
            return 0;
        }

        if (args.Any(a => a.Equals("--uipreview", StringComparison.OrdinalIgnoreCase)))
        {
            ApplicationConfiguration.Initialize();

            var main = new MainForm();
            main.Shown += async (_, _) =>
            {
                await Task.Delay(14000);
                try
                {
                    using var bmp = new Bitmap(main.Width, main.Height);
                    main.DrawToBitmap(bmp, new Rectangle(0, 0, main.Width, main.Height));
                    bmp.Save(Path.Combine(AppContext.BaseDirectory, "ui-preview.png"));
                }
                catch
                {
                    // ignored
                }

                main.Close();
            };

            Application.Run(main);
            return 0;
        }

        ApplicationConfiguration.Initialize();

        using var mutex = new Mutex(true, "InditexStockAlarm_SingleInstance", out var isNew);
        if (!isNew)
        {
            NativeMethods.FocusWindow("Inditex · Alarma de stock");
            return 0;
        }

        Application.Run(new MainForm());
        return 0;
    }
}
