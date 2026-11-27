using System.Text;

namespace InditexStockAlarm;

public sealed class AlarmPlayer
{
    private readonly string _sirenPath;
    private readonly string _beepPath;
    private static object? _voice;

    public bool Enabled { get; set; } = true;

    /// <summary>both | siren | voice | beep</summary>
    public string Mode { get; set; } = "both";

    public AlarmPlayer()
    {
        var temp = Path.GetTempPath();
        _sirenPath = Path.Combine(temp, "inditex-stock-siren.wav");
        _beepPath = Path.Combine(temp, "inditex-stock-beep.wav");

        try
        {
            if (!File.Exists(_sirenPath) || new FileInfo(_sirenPath).Length < 1000)
                File.WriteAllBytes(_sirenPath, BuildSiren());

            if (!File.Exists(_beepPath) || new FileInfo(_beepPath).Length < 1000)
                File.WriteAllBytes(_beepPath, BuildBeep());
        }
        catch
        {
            // sin sonido si falla
        }
    }

    public void Alarm(string message)
    {
        if (!Enabled) return;

        switch (Mode)
        {
            case "siren":
                NativeMethods.PlayLoop(_sirenPath);
                break;
            case "voice":
                Speak(message);
                break;
            case "beep":
                NativeMethods.PlayLoop(_beepPath);
                break;
            default:
                NativeMethods.PlayLoop(_sirenPath);
                Speak(message);
                break;
        }
    }

    public void Silence() => NativeMethods.StopSound();

    public void TestSound()
    {
        if (!Enabled) return;

        switch (Mode)
        {
            case "voice":
                Speak("Prueba de aviso. Así sonará la alarma.");
                return;
            case "beep":
                NativeMethods.PlayLoop(_beepPath);
                break;
            case "siren":
                NativeMethods.PlayLoop(_sirenPath);
                break;
            default:
                NativeMethods.PlayLoop(_sirenPath);
                Speak("Prueba de aviso. Así sonará la alarma.");
                break;
        }

        var timer = new System.Windows.Forms.Timer { Interval = 4000 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            timer.Dispose();
            Silence();
        };
        timer.Start();
    }

    public static void Speak(string text)
    {
        try
        {
            _voice ??= CreateVoice();
            if (_voice is null) return;
            dynamic voice = _voice;
            voice.Volume = 100;
            voice.Rate = 1;
            voice.Speak(text, 1);
        }
        catch
        {
            // sin voz si no hay SAPI
        }
    }

    private static object? CreateVoice()
    {
        try
        {
            var type = Type.GetTypeFromProgID("SAPI.SpVoice");
            return type is null ? null : Activator.CreateInstance(type);
        }
        catch
        {
            return null;
        }
    }

    private static byte[] BuildSiren()
    {
        const int rate = 44100;
        const double seconds = 6.0;
        var samples = (int)(rate * seconds);

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        var dataSize = samples * 2;

        WriteHeader(bw, rate, dataSize);

        var phase = 0.0;
        for (var i = 0; i < samples; i++)
        {
            var t = (double)i / rate;
            var frequency = Math.Sin(2 * Math.PI * 1.1 * t) >= 0 ? 1450.0 : 850.0;
            phase += 2 * Math.PI * frequency / rate;
            var value = Math.Sign(Math.Sin(phase)) * 0.55;
            bw.Write((short)(value * short.MaxValue));
        }

        return ms.ToArray();
    }

    private static byte[] BuildBeep()
    {
        const int rate = 44100;
        const double seconds = 1.2;
        var samples = (int)(rate * seconds);

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        var dataSize = samples * 2;

        WriteHeader(bw, rate, dataSize);

        for (var i = 0; i < samples; i++)
        {
            var t = (double)i / rate;
            var cycle = t % 0.4;
            var on = cycle < 0.22;
            var value = on ? Math.Sin(2 * Math.PI * 1000 * t) * 0.5 : 0.0;
            bw.Write((short)(value * short.MaxValue));
        }

        return ms.ToArray();
    }

    private static void WriteHeader(BinaryWriter bw, int rate, int dataSize)
    {
        bw.Write(Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + dataSize);
        bw.Write(Encoding.ASCII.GetBytes("WAVE"));
        bw.Write(Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);
        bw.Write((short)1);
        bw.Write((short)1);
        bw.Write(rate);
        bw.Write(rate * 2);
        bw.Write((short)2);
        bw.Write((short)16);
        bw.Write(Encoding.ASCII.GetBytes("data"));
        bw.Write(dataSize);
    }
}
