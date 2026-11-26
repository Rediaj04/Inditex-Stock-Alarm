using System.Text;

namespace BershkaMonitor;

public sealed class AlarmPlayer
{
    private readonly string _wavPath;
    private static object? _voice;

    public bool Enabled { get; set; } = true;

    public AlarmPlayer()
    {
        _wavPath = Path.Combine(Path.GetTempPath(), "bershka-monitor-siren.wav");
        try
        {
            if (!File.Exists(_wavPath) || new FileInfo(_wavPath).Length < 1000)
                File.WriteAllBytes(_wavPath, BuildSiren());
        }
        catch
        {
            // sin sonido si falla
        }
    }

    public void Alarm(string message)
    {
        if (!Enabled) return;
        NativeMethods.PlayLoop(_wavPath);
        Speak(message);
    }

    public void Silence() => NativeMethods.StopSound();

    public void TestSound()
    {
        if (!Enabled) return;
        NativeMethods.PlayLoop(_wavPath);
        Speak("Prueba de alarma. Así sonará cuando aparezca la talla cuarenta y cuatro.");

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
}
