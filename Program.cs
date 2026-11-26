using System.Diagnostics;

namespace InditexStockAlarm;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        var probeArg = args.FirstOrDefault(a => a.StartsWith("--probe=", StringComparison.OrdinalIgnoreCase));
        if (probeArg is not null)
        {
            var edgePath = EdgeCdp.FindEdge();
            if (edgePath is null)
            {
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "probe.txt"), "no edge");
                return 1;
            }

            var edge = new EdgeCdp(edgePath, Path.Combine(Path.GetTempPath(), "bsk-probe-profile"), 9555);
            var result = SizeProbeService.ProbeAsync(edge, probeArg[8..]).GetAwaiter().GetResult();
            File.WriteAllText(
                Path.Combine(AppContext.BaseDirectory, "probe.txt"),
                System.Text.Json.JsonSerializer.Serialize(result,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            edge.KillBrowser();
            return 0;
        }

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

        if (args.Any(a => a.Equals("--addpreview", StringComparison.OrdinalIgnoreCase)))
        {
            ApplicationConfiguration.Initialize();

            var dialog = new AddItemDialog();
            var timer = new System.Windows.Forms.Timer { Interval = 3500 };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                try
                {
                    using var bmp = new Bitmap(dialog.Width, dialog.Height);
                    dialog.DrawToBitmap(bmp, new Rectangle(0, 0, dialog.Width, dialog.Height));
                    bmp.Save(Path.Combine(AppContext.BaseDirectory, "add-preview.png"));
                }
                catch
                {
                    // ignored
                }
                dialog.Close();
            };
            dialog.Shown += (_, _) => timer.Start();

            Application.Run(dialog);
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

            var widthArg = args.FirstOrDefault(a => a.StartsWith("--w=", StringComparison.OrdinalIgnoreCase));
            var heightArg = args.FirstOrDefault(a => a.StartsWith("--h=", StringComparison.OrdinalIgnoreCase));
            if (widthArg is not null && int.TryParse(widthArg[4..], out var previewWidth))
                main.ClientSize = new Size(previewWidth, main.ClientSize.Height);
            if (heightArg is not null && int.TryParse(heightArg[4..], out var previewHeight))
                main.ClientSize = new Size(main.ClientSize.Width, previewHeight);

            main.Shown += async (_, _) =>
            {
                await Task.Delay(14000);
                try
                {
                    File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "ui-layout.txt"), main.DumpLayout());
                }
                catch
                {
                    // ignored
                }
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
