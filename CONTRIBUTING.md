# Contribuir

Gracias por querer mejorar Inditex Stock Alarm.

## Ideas bienvenidas

- Soporte para más webs (con una URL de prueba y las tallas esperadas).
- Mejoras de detección (nuevos selectores, tallas compuestas, calzado).
- Traducciones.
- Correcciones de la interfaz o accesibilidad.

## Antes de abrir un PR

1. Ejecuta los tests:

   ```bat
   dotnet test tests/InditexStockAlarm.Tests/InditexStockAlarm.Tests.csproj -c Release
   ```

2. Comprueba que compila:

   ```bat
   dotnet build -c Release
   ```

3. Si añades soporte para una web, indica en el PR:
   - URL de ejemplo.
   - Tallas que aparecen.
   - Si funciona por API o por DOM.

## Reglas

- Un cambio por PR, con un mensaje claro.
- No subas `config.json`, perfiles de Edge ni logs.
- Mantén el aviso de copyright (licencia MIT).

## Atribución

Si reutilizas el proyecto, conserva `LICENSE` y, si puedes, enlaza el repositorio original. No es obligatorio, pero se agradece.
