namespace InditexStockAlarm;

internal static class Loc
{
    public static bool English { get; set; }

    private static readonly Dictionary<string, string> Map = new(StringComparer.Ordinal)
    {
        // Main window
        ["Inditex · Alarma de stock"] = "Inditex · Stock Alarm",
        ["Pega cualquier ficha de Inditex, elige talla y listo: alarma + pestaña en Brave"] = "Paste any Inditex product, pick a size and go: alarm + Brave tab",
        ["▶  INICIAR"] = "▶  START",
        ["■  DETENER"] = "■  STOP",
        ["seg"] = "sec",
        ["Probar"] = "Test",
        ["Ajustes"] = "Settings",
        ["Abrir web"] = "Open web",
        ["Silenciar"] = "Silence",
        ["Quitar snooze"] = "Cancel snooze",
        ["Sonido: ON"] = "Sound: ON",
        ["Sonido: OFF"] = "Sound: OFF",
        ["🔒 mín. 15 s"] = "🔒 min. 15 s",
        ["🔓 mín. 5 s"] = "🔓 min. 5 s",
        ["PRODUCTOS VIGILADOS"] = "WATCHED PRODUCTS",
        ["＋ Añadir producto"] = "＋ Add product",
        ["Todavía no hay productos. Pulsa «＋ Añadir producto» y pega la URL de la ficha."] = "No products yet. Press «＋ Add product» and paste the product URL.",
        ["REGISTRO EN VIVO"] = "LIVE LOG",
        ["limpiar"] = "clear",
        ["abrir carpeta de logs"] = "open logs folder",

        // Pills
        ["● DETENIDO"] = "● STOPPED",
        ["● CONECTANDO…"] = "● CONNECTING…",
        ["● VIGILANDO"] = "● WATCHING",
        ["● ERROR"] = "● ERROR",
        ["● SNOOZE 30 MIN"] = "● SNOOZE 30 MIN",
        ["● FUERA DE HORARIO"] = "● OUTSIDE SCHEDULE",
        ["● SIN PRODUCTOS"] = "● NO PRODUCTS",
        ["● ¡HAY STOCK!"] = "● IN STOCK!",

        // Row and menu
        ["PAUSADO"] = "PAUSED",
        ["agotada"] = "sold out",
        ["disponible"] = "available",
        ["pocas unidades"] = "low stock",
        ["no existe"] = "not available",
        ["ERROR"] = "ERROR",
        ["Comprobar ahora"] = "Check now",
        ["Editar tallas / nombre…"] = "Edit sizes / name…",
        ["Pausar"] = "Pause",
        ["Reanudar"] = "Resume",
        ["Eliminar"] = "Delete",

        // Tray
        ["Mostrar ventana"] = "Show window",
        ["Iniciar / Detener"] = "Start / Stop",
        ["Salir"] = "Exit",
        ["Sigue vigilando en segundo plano."] = "Still watching in the background.",
        ["Vigilando en segundo plano. Doble clic en la bandeja para abrir."] = "Watching in the background. Double-click the tray icon to open.",

        // Settings
        ["SONIDO DE LA ALARMA"] = "ALARM SOUND",
        ["Sirena + voz"] = "Siren + voice",
        ["Solo sirena"] = "Siren only",
        ["Solo voz"] = "Voice only",
        ["Solo pitido"] = "Beep only",
        ["Archivo personalizado"] = "Custom file",
        ["Elegir…"] = "Choose…",
        ["Sin archivo: elige un WAV, MP3 o WMA."] = "No file: choose a WAV, MP3 or WMA.",
        ["AVISO AL MÓVIL (NTFY.SH)"] = "MOBILE ALERT (NTFY.SH)",
        ["Probar push"] = "Test push",
        ["HORARIO DE VIGILANCIA"] = "WATCH SCHEDULE",
        ["Vigilar solo dentro del horario"] = "Watch only within schedule",
        ["desde"] = "from",
        ["hasta"] = "to",
        ["24:00 = fin del día"] = "24:00 = end of day",
        ["Modo suave: mínimo 45 s entre comprobaciones (menos riesgo de bloqueo)"] = "Gentle mode: at least 45 s between checks (less risk of blocking)",
        ["APARIENCIA"] = "APPEARANCE",
        ["Oscuro"] = "Dark",
        ["Claro"] = "Light",
        ["El cambio de tema se aplica al reiniciar la app."] = "Theme changes apply on restart.",
        ["ESCALA DE LA INTERFAZ"] = "UI SCALE",
        ["Se aplica al reiniciar la app."] = "Applies on restart.",
        ["IDIOMA / LANGUAGE"] = "LANGUAGE",
        ["PANTALLA DE LA ALERTA"] = "ALERT SCREEN",
        ["Predeterminada"] = "Default",
        ["Iniciar con Windows"] = "Start with Windows",
        ["Arrancar minimizada"] = "Start minimised",
        ["Guardar"] = "Save",
        ["Cancelar"] = "Cancel",

        // Add product
        ["Añadir producto"] = "Add product",
        ["Buscar tallas"] = "Search sizes",
        ["Tallas detectadas (marca las que quieras vigilar):"] = "Detected sizes (tick the ones to watch):",
        ["marcar todas"] = "select all",
        ["desmarcar"] = "clear",
        ["Tallas a mano (si no detecta o quieres otras):"] = "Sizes manually (if not detected or you want others):",
        ["Añadir y vigilar"] = "Add and watch",
        ["Nombre (opcional)"] = "Name (optional)",
        ["Puedes poner varias separadas por comas: 42,44,46 · M,L,XL · o «Única»"] = "You can use several comma-separated sizes: 42,44,46 · M,L,XL · or “One size”",

        // Edit
        ["Editar producto"] = "Edit product",
        ["Tallas (separadas por comas)"] = "Sizes (comma-separated)",
        ["Ejemplos: 42,44,46 · M,L,XL · Única"] = "Examples: 42,44,46 · M,L,XL · One size",

        // Alert
        ["¡TALLA DISPONIBLE!"] = "SIZE IN STOCK!",
        ["La sirena no parará hasta que pulses OK o cierres el aviso."] = "The siren will not stop until you press OK or close the alert.",
        ["silenciar sirena (el aviso sigue en pantalla)"] = "silence siren (alert stays on screen)",
        ["Ya he comprado · pausar"] = "Already bought · pause",
        ["Abrir ficha"] = "Open product",
        ["OK · ¡VOY A COMPRAR!"] = "OK · I'M GOING TO BUY!",
        ["¡STOCK! · Alerta de talla"] = "STOCK! · Size alert",

        // Update
        ["Nueva versión disponible"] = "New version available",
        ["Abrir descarga"] = "Open download",
        ["Ahora no"] = "Not now",
        ["Hay una actualización en GitHub. No es obligatorio actualizar:\npuedes seguir usando esta versión si te va bien."] = "There is an update on GitHub. Updating is optional:\nyou can keep using this version if it works for you.",

        // Fast interval
        ["⚠  Bajar de 15 segundos"] = "⚠  Below 15 seconds",
        ["Aviso: bajar del mínimo recomendado"] = "Warning: below the recommended minimum",
        ["Entiendo el riesgo: desbloquear"] = "I understand the risk: unlock"
    };

    public static string T(string text) =>
        English && Map.TryGetValue(text, out var translated) ? translated : text;

    public static string T(string spanish, string english) => English ? english : spanish;

    public static void Apply(Control root)
    {
        if (!English) return;

        if (!string.IsNullOrEmpty(root.Text))
            root.Text = T(root.Text);

        foreach (Control control in root.Controls)
        {
            if (!string.IsNullOrEmpty(control.Text))
                control.Text = T(control.Text);

            if (control is ComboBox combo)
            {
                for (var i = 0; i < combo.Items.Count; i++)
                {
                    if (combo.Items[i] is string item)
                        combo.Items[i] = T(item);
                }
            }
            else if (control is ListBox list)
            {
                for (var i = 0; i < list.Items.Count; i++)
                {
                    if (list.Items[i] is string item)
                        list.Items[i] = T(item);
                }
            }
            else if (control is ToolStrip strip)
            {
                ApplyToolStrip(strip);
            }

            Apply(control);
        }
    }

    private static void ApplyToolStrip(ToolStrip strip)
    {
        foreach (ToolStripItem item in strip.Items)
        {
            if (!string.IsNullOrEmpty(item.Text))
                item.Text = T(item.Text);

            if (item is ToolStripDropDownItem drop && drop.HasDropDownItems)
                ApplyToolStrip(drop.DropDown);
        }
    }
}
