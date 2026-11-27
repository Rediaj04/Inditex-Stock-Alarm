# Seguridad

## Qué hace la app

- Se ejecuta localmente en Windows.
- Lanza una instancia de Microsoft Edge en modo headless con un perfil propio (cookies aisladas).
- Consulta las fichas de Inditex y guarda `config.json`, `logs/` y su perfil junto al ejecutable (o en `%LOCALAPPDATA%\InditexStockAlarm`).
- Opcionalmente envía un push a `ntfy.sh` si configuras un tema.

## Qué NO hace

- No pide ni guarda contraseñas ni datos de pago.
- No compra, no añade a la cesta y no rellena formularios.
- No envía datos a servidores propios.

## Reportar un problema

Abre un issue describiendo:
- Versión (aparece en el pie de la app).
- Qué esperabas y qué pasó.
- Fragmento del log (sin datos personales).

Si el problema es sensible, no publiques tokens ni rutas personales.
