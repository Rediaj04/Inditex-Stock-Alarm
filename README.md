# Bershka Monitor · alerta de stock

Aplicación de escritorio para Windows que vigila la disponibilidad de las tallas que elijas en **cualquier ficha de Bershka** y te avisa con alarma sonora, ventana emergente y una pestaña abierta en Brave.

> Proyecto personal, sin relación con Inditex/Bershka. Úsalo de forma responsable y respetando los términos de la web. No compra nada por ti.

![Ventana principal](docs/app.png)

## Descargar y ejecutar (sin instalar nada)

1. Ve a **Releases** y descarga `BershkaMonitor.exe` (build portable, ya lleva todo dentro).
2. Ponlo en una carpeta normal (Escritorio, Documentos, un USB…). No necesita instalador ni administrador.
3. Doble clic. La primera vez te pide añadir un producto.
4. Pega la URL de la ficha de Bershka, escribe la talla y dale a **INICIAR**.

Requisitos: **Windows 10/11 de 64 bits** y **Microsoft Edge** (viene preinstalado en Windows). Brave es opcional: si no está, el aviso se abre en tu navegador por defecto.

> El ejecutable no está firmado, así que SmartScreen puede avisar la primera vez: *Más información → Ejecutar de todas formas*.

## Dónde guarda los datos

La app es portable: guarda `config.json`, `logs/` y el perfil interno de Edge **junto al .exe** si la carpeta es escribible. Si no lo es (por ejemplo en `Program Files`), usa `%LOCALAPPDATA%\BershkaMonitor`.

## Qué hace

- Añades un producto pegando su URL de Bershka y escribiendo la talla (o varias: `42,44,46`).
- Consulta el stock cada 15 s (mínimo configurable hasta 120 s) en un **Edge oculto en segundo plano**, así que no te abre ventanas de Edge.
- Cuando la talla pasa a disponible:
  - **Ventana emergente grande y siempre encima** con un botón `OK · ¡VOY A COMPRAR!`.
  - **Sirena + voz** que no para hasta que pulsas OK (o silencias). Se puede desactivar con `Sonido: ON/OFF`.
  - Abre la **pestaña en Brave** con el producto y color exactos.
  - Notificación en la bandeja y parpadeo de la ventana.
- Si la talla sigue disponible, repite el aviso cada 2 minutos (con 5 min de silencio tras pulsar OK).
- Impide que el PC entre en suspensión mientras vigila.
- Registro en vivo y en `logs\monitor-AAAAMMDD.log`.

![Ventana de aviso](docs/alerta.png)

## Uso

1. Pulsa **＋ Añadir producto**, pega la URL de la ficha y escribe la talla.
   - El color se toma del parámetro `colorId` de la URL.
   - Puedes poner varias tallas separadas por comas.
2. Pulsa **INICIAR** (arranca solo al abrir).
3. Deja la app abierta o minimizada en la bandeja.

> La talla no se puede preseleccionar por URL en Bershka: la pestaña se abre en la ficha y color correctos, y tú tocas la talla y “Añadir a la cesta”.

## Configuración

`config.json`:

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

## Compilar

Con el SDK de .NET 10:

```bat
build-release.cmd
```

Genera `release\BershkaMonitor.exe` (un solo archivo autocontenido). Para una build rápida que use el runtime instalado:

```bat
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

El repositorio incluye un workflow de GitHub Actions (`.github/workflows/build.yml`) que compila la versión portable en cada push y crea un Release al subir un tag `v*`.

## Cómo detecta el stock

Carga la ficha en un Edge headless y lee los botones de talla de la página:

- botón deshabilitado → agotada
- clase `size-button--low-stock` → quedan pocas unidades
- botón activo → disponible

Funciona con cualquier producto de Bershka sin depender de IDs internos. Se recarga la ficha en cada comprobación (con imágenes y fuentes bloqueadas para que sea ligero).

## Límites conocidos

- Solo Bershka (el parseo de URL y los selectores son suyos). Si cambian el diseño de la ficha, habrá que ajustar los selectores.
- Con 15 s de intervalo, un reestock muy corto puede escaparse.
- Si cierras la app o el PC se apaga/suspende, deja de vigilar.
- La web puede bloquear peticiones si se abusa de la frecuencia; el log muestra los fallos.
- No automatiza la compra.

## Licencia

MIT. Ver [LICENSE](LICENSE).
