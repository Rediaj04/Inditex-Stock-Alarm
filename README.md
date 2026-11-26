# Bershka Monitor · alerta de stock

Aplicación de escritorio para Windows que vigila la disponibilidad de las tallas que elijas en **cualquier ficha de Bershka** y te avisa con alarma sonora, ventana emergente y una pestaña abierta en Brave.

> Proyecto personal, sin relación con Inditex/Bershka. Úsalo de forma responsable y respetando los términos de la web. No compra nada por ti.

![Ventana principal](docs/app.png)

## Qué hace

- Añades un producto pegando su URL de Bershka y escribiendo la talla (o varias: `42,44,46`).
- Consulta el stock cada 15 s (mínimo configurable hasta 120 s) en un **Edge oculto en segundo plano**, así que no te abre ventanas de Edge.
- Cuando la talla pasa a disponible:
  - **Ventana emergente grande y siempre encima** con un botón `OK · ¡VOY A COMPRAR!`.
  - **Sirena + voz** que no para hasta que pulsas OK (o silencias).
  - Abre la **pestaña en Brave** con el producto y color exactos.
  - Notificación en la bandeja y parpadeo de la ventana.
- Si la talla sigue disponible, repite el aviso cada 2 minutos (con 5 min de silencio tras pulsar OK).
- Impide que el PC entre en suspensión mientras vigila.
- Registro en vivo y en `logs\monitor-AAAAMMDD.log`.

![Ventana de aviso](docs/alerta.png)

## Requisitos

- Windows 10/11.
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download) (o el SDK si vas a compilar).
- Microsoft **Edge** instalado (se usa para consultar el stock en segundo plano).
- **Brave** instalado si quieres que abra la pestaña del aviso (si no está, usa el navegador por defecto).

## Uso

1. Ejecuta `BershkaMonitor.exe`.
2. Pulsa **＋ Añadir producto**, pega la URL de la ficha y escribe la talla.
   - El color se toma del parámetro `colorId` de la URL.
   - Puedes poner varias tallas separadas por comas.
3. Pulsa **INICIAR** (arranca solo al abrir).
4. Deja la app abierta o minimizada en la bandeja. El **sonido** se activa/desactiva con el botón `Sonido: ON/OFF` (se guarda en `config.json`).

> La talla no se puede preseleccionar por URL en Bershka: la pestaña se abre en la ficha y color correctos, y tú tocas la talla y “Añadir a la cesta”.

## Configuración

`config.json` se crea junto al ejecutable:

```json
{
  "intervalSeconds": 15,
  "soundEnabled": true,
  "openInBrave": true,
  "items": [
    {
      "name": "Jeans barrel",
      "url": "https://www.bershka.com/es/jeans-barrel-c0p229733871.html?colorId=428",
      "sizes": "44"
    }
  ]
}
```

- `intervalSeconds`: 15–120.
- `soundEnabled`: sirena y voz.
- `openInBrave`: abrir pestaña automáticamente al detectar stock.
- `items`: lista de productos. También se puede editar a mano y reiniciar.

## Compilar desde el código

```bat
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

El .exe queda en `publish\BershkaMonitor.exe`.

## Cómo detecta el stock

Carga la ficha en un Edge headless y lee los botones de talla de la página:

- botón deshabilitado → agotada
- clase `size-button--low-stock` → quedan pocas unidades
- botón activo → disponible

Funciona con cualquier producto de Bershka sin depender de IDs internos. Se recarga la ficha en cada comprobación (con imágenes y fuentes bloqueadas para que sea ligero).

## Límites conocidos

- Solo Bershka (el parseo de URL y los selectores son suyos).
- Con 15 s de intervalo, un reestock muy corto puede escaparse.
- Si cierras la app o el PC se apaga/suspende, deja de vigilar.
- La web puede bloquear peticiones si se abusa de la frecuencia; el log muestra los fallos.
- No automatiza la compra.

## Licencia

MIT. Ver [LICENSE](LICENSE).
