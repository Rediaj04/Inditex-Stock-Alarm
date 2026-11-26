using System.Runtime.InteropServices;
using System.Text;

namespace InditexStockAlarm;

internal static class NativeMethods
{
    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint esFlags);

    private const uint EsContinuous = 0x80000000;
    private const uint EsSystemRequired = 0x00000001;

    public static void KeepAwake(bool on)
    {
        try
        {
            SetThreadExecutionState(on ? EsContinuous | EsSystemRequired : EsContinuous);
        }
        catch
        {
            // ignored
        }
    }

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern bool PlaySound(string? sound, IntPtr module, uint flags);

    private const uint SndAsync = 0x0001;
    private const uint SndLoop = 0x0008;
    private const uint SndFilename = 0x00020000;
    private const uint SndPurge = 0x0040;

    public static void PlayLoop(string path)
    {
        try
        {
            PlaySound(path, IntPtr.Zero, SndFilename | SndAsync | SndLoop);
        }
        catch
        {
            // ignored
        }
    }

    public static void StopSound()
    {
        try
        {
            PlaySound(null, IntPtr.Zero, SndPurge);
        }
        catch
        {
            // ignored
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern void SwitchToThisWindow(IntPtr hWnd, bool altTab);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr param);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr param);

    public static IntPtr FindWindowByTitle(string contains)
    {
        var found = IntPtr.Zero;
        try
        {
            EnumWindows((hWnd, _) =>
            {
                if (!IsWindowVisible(hWnd)) return true;
                var sb = new StringBuilder(512);
                GetWindowText(hWnd, sb, sb.Capacity);
                var title = sb.ToString();
                if (title.Length > 0 && title.Contains(contains, StringComparison.OrdinalIgnoreCase))
                {
                    found = hWnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
        }
        catch
        {
            // ignored
        }
        return found;
    }

    public static void FocusWindow(string titleContains)
    {
        var hWnd = FindWindowByTitle(titleContains);
        if (hWnd == IntPtr.Zero) return;
        try
        {
            ShowWindow(hWnd, 9); // SW_RESTORE
            SetForegroundWindow(hWnd);
            SwitchToThisWindow(hWnd, true);
        }
        catch
        {
            // ignored
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FlashInfo
    {
        public uint Size;
        public IntPtr Window;
        public uint Flags;
        public uint Count;
        public uint Timeout;
    }

    [DllImport("user32.dll")]
    private static extern bool FlashWindowEx(ref FlashInfo info);

    public static void Flash(IntPtr hWnd)
    {
        try
        {
            var f = new FlashInfo
            {
                Size = (uint)Marshal.SizeOf<FlashInfo>(),
                Window = hWnd,
                Flags = 0x00000003 | 0x0000000C, // FLASHW_ALL | FLASHW_TIMERNOFG
                Count = 10,
                Timeout = 0
            };
            FlashWindowEx(ref f);
        }
        catch
        {
            // ignored
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hWnd, int attribute, ref int value, int size);

    public static void DarkTitleBar(IntPtr hWnd)
    {
        try
        {
            var on = 1;
            DwmSetWindowAttribute(hWnd, 20, ref on, sizeof(int));
        }
        catch
        {
            // ignored
        }
    }
}
