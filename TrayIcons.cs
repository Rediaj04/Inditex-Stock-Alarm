using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace InditexStockAlarm;

internal static class TrayIcons
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public static Icon Create(Color color)
    {
        var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var background = new SolidBrush(color);
            g.FillEllipse(background, 1, 1, 30, 30);

            using var white = new SolidBrush(Color.White);
            g.FillEllipse(white, 10, 7, 12, 12);
            g.FillRectangle(white, 12, 13, 8, 8);
            g.FillEllipse(white, 9, 19, 14, 5);
            g.FillEllipse(white, 14, 24, 4, 4);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
            bitmap.Dispose();
        }
    }
}
