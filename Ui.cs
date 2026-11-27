namespace InditexStockAlarm;

internal static class Ui
{
    public static void CenterOnOwner(Form form)
    {
        var screen = form.Owner is not null ? Screen.FromControl(form.Owner) : Screen.FromControl(form);
        var area = screen.WorkingArea;

        var width = Math.Min(form.Width, Math.Max(400, area.Width - 10));
        var height = Math.Min(form.Height, Math.Max(300, area.Height - 10));

        form.Location = new Point(
            area.Left + Math.Max(0, (area.Width - width) / 2),
            area.Top + Math.Max(0, (area.Height - height) / 2));
    }
}
