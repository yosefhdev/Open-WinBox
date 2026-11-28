# Guía para agentes: widgets de escritorio en Windows (estilo OpenWinBox)

Objetivo: crear desde cero una app de Windows 11 con widgets que viven en el escritorio, arrancan rápido y se sienten nativos. Referencia real: este repo (`src/DeskBox`).

## 1. Stack obligatorio

| Pieza | Qué usar | Por qué |
|---|---|---|
| Lenguaje | C# en .NET 10 (`net10.0-windows10.0.22621.0`) | Acceso directo a WinRT y Win32 |
| UI | WinUI 3 / Windows App SDK + XAML | Mica/Acrylic, aspecto nativo |
| Ventanas | `WinUIEx` | Ventanas sin marco, posición y capas fijadas al escritorio |
| MVVM | `CommunityToolkit.Mvvm` | `ObservableObject`, `SetProperty`, comandos |
| Controles/animación | `CommunityToolkit.WinUI.*` | Ajustes, sliders, animaciones |
| Bandeja | `H.NotifyIcon.WinUI` | Icono y menú en la bandeja del sistema |
| Compilación | Native AOT (`PublishAot=true`) | Arranque rápido, poca RAM |

Reglas para que AOT funcione: nada de reflexión dinámica. Usa `[GeneratedComInterface]` / `LibraryImport` para COM y P/Invoke, y source generators para JSON (`JsonSerializerContext`).

## 2. Arquitectura de un widget

Cada widget tiene cuatro piezas:

1. **Servicio** (`Services/XxxService.cs`): habla con Windows (WinRT, COM, Win32). Sin UI. Expone eventos (`XxxChanged`) y métodos `TryXxxAsync` que devuelven `bool` y nunca lanzan excepciones.
2. **ViewModel** (`ViewModels/XxxWidgetViewModel.cs`): estado observable para la vista. Se suscribe a los eventos del servicio y los reenvía al hilo de UI con `DispatcherQueue.TryEnqueue`.
3. **Vista** (`Controls/WidgetContents/XxxWidgetContent.xaml` + `.xaml.cs`): solo layout, bindings y gestos de puntero.
4. **Adaptador** (`XxxWidgetContentAdapter.cs`): conecta la vista con el contenedor genérico de widgets (la ventana, el título, el tamaño y la persistencia son comunes).

El contenedor común lo ponen todos los widgets: ventana con marco propio, barra de título con icono, arrastre y cambio de tamaño, y guardado en `WidgetConfig`. Un widget nuevo solo aporta su contenido.

Convenciones:
- Los widgets grandes se reparten en clases `partial` por área (`.Playback.cs`, `.MediaInfo.cs`, `.Lifecycle.cs`).
- `Dispose()` desuscribe todos los eventos y suelta las imágenes (las `BitmapImage` retienen memoria nativa de WIC).
- Textos localizados en `Strings/<locale>.json`. Nunca escribas texto fijo en la vista.
- Cada tipo de widget tiene su icono de título (ver `AGENTS.md`, sección de iconos).

## 3. Caso práctico: widget de música (play, progreso, volumen, botones, portada)

Archivos de referencia:
- `Services/MusicSessionService.cs`: sesión multimedia (qué suena, controles).
- `Services/MusicVolumeService.cs`: volumen del sistema y de la app (COM de Core Audio).
- `ViewModels/MusicWidgetViewModel*.cs`: estado, temporizador, portada.
- `Controls/WidgetContents/MusicWidgetContent.xaml(.cs)`: vista y gestos.

### 3.1 Qué se está reproduciendo: SMTC

La API clave es `Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager` (SMTC). Ve lo que reproduce cualquier app que se registre en el panel multimedia de Windows (Spotify, navegadores, etc.).

```csharp
var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
manager.SessionsChanged += ...;        // se abre o cierra una app
manager.CurrentSessionChanged += ...;  // cambia la app activa

var session = manager.GetCurrentSession();
session.PlaybackInfoChanged += ...;    // play/pausa, shuffle, repeat
session.MediaPropertiesChanged += ...; // título, artista, portada
session.TimelinePropertiesChanged += ...; // posición y duración

var props = await session.TryGetMediaPropertiesAsync(); // Title, Artist, Thumbnail
var timeline = session.GetTimelineProperties();         // Position, EndTime, LastUpdatedTime
```

- Al cambiar de sesión, desuscribe los eventos de la anterior antes de suscribir la nueva.
- `SourceAppUserModelId` identifica la app. Úsalo para el icono de origen y para elegir sesión si hay varias.
- Puede pasar que `TryGetMediaPropertiesAsync` falle o devuelva datos vacíos mientras la app cambia de pista. Trátalo como "sin cambios" y reintenta un par de veces con un pequeño retraso (aquí: 4 reintentos).

### 3.2 Botones multimedia

```csharp
await session.TryPlayAsync();  await session.TryPauseAsync();
await session.TrySkipPreviousAsync();  await session.TrySkipNextAsync();
await session.TryChangeShuffleActiveAsync(bool);
await session.TryChangeAutoRepeatModeAsync(MediaPlaybackAutoRepeatMode);
```

- Play/pausa es un solo botón: si `PlaybackStatus == Playing` llama a pausa, si no, a play.
- Después de cada comando: refresca, espera unos 180 ms y vuelve a refrescar. Muchas apps confirman el estado con retraso.
- Habilita cada botón según `session.GetPlaybackInfo().Controls` (`IsNextEnabled`, `IsPlayPauseEnabled`, …).
- Iconos: `Path` vectoriales propios en XAML (`MusicTransportIcon.xaml`), que escalan sin pixelarse.

### 3.3 Barra de progreso

El problema: SMTC no emite la posición de forma continua. Solo da `Position` + `LastUpdatedTime`.

La solución es interpolar en local:
- Guarda `_lastSyncedPosition` y `_lastPositionSyncAt = DateTimeOffset.UtcNow` en cada evento de timeline.
- Un `DispatcherQueueTimer` (500 ms con el widget expandido, 1000 ms en compacto) calcula:
  `pos = min(duración, _lastSyncedPosition + (UtcNow - _lastPositionSyncAt))`
- El temporizador solo corre mientras se reproduce. En pausa se detiene, para no gastar CPU.
- Si la app no da duración (radios, directos), oculta la barra con un retraso de unos 2 s para que no parpadee.

Arrastrar para buscar (seek):
- La barra es un `Grid` propio (pista + relleno + punto), no un `Slider`, para controlar el gesto.
- `PointerPressed` → `BeginSeek()` (`_isSeeking = true`) y captura el puntero. `PointerMoved` → `SeekValue = ratio * duración`.
- Mientras `_isSeeking` es verdadero, el temporizador **no** toca `SeekValue`. Así se evita que la barra "salte" bajo el dedo.
- `PointerReleased` → `CommitSeekAsync()`: llama a `TryChangePlaybackPositionAsync(ticks)` (en unidades de 100 ns), fija la posición en local, espera unos 260 ms y refresca. Si la app rechaza el seek, refresca y vuelve a la posición real.

### 3.4 Volumen (sistema y app): Core Audio por COM

SMTC no controla el volumen. Se usa Core Audio con interfaces COM declaradas a mano (con GUID):

- **Volumen del sistema**: `MMDeviceEnumerator` → `GetDefaultAudioEndpoint` → `Activate(IAudioEndpointVolume)` → `Get/SetMasterVolumeLevelScalar(0..1)`.
- **Volumen de la app que suena**: `IMMDevice.Activate(IAudioSessionManager2)` → `GetSessionEnumerator` → por cada `IAudioSessionControl2` obtén `GetProcessId` y compáralo con el proceso de la sesión SMTC (por AUMID o nombre). En la sesión que coincide, usa `ISimpleAudioVolume.Get/SetMasterVolume`. Si no hay coincidencia exacta pero solo hay una sesión de audio válida (que no sea "sonidos del sistema"), usa esa.
- Libera cada objeto COM (`Marshal.ReleaseComObject` o `Dispose` de un handle propio).
- Sliders: mientras el usuario arrastra, guarda solo el último valor pedido (`_pendingSystemVolume`) y aplícalo en un bucle que vacía esa cola. Así no saturas COM con cada píxel ni se cruzan las llamadas.
- Si no se encuentra la sesión de la app, muestra el slider atenuado y el texto "No disponible".

### 3.5 Portada

```csharp
using var stream = await props.Thumbnail.OpenReadAsync();
var image = new BitmapImage { DecodePixelWidth = 192 }; // limita la memoria
await image.SetSourceAsync(stream);
```

- `DecodePixelWidth` evita decodificar portadas de 3000 px para un recuadro pequeño.
- **Color dominante**: clona el stream, decodifica una miniatura diminuta con `BitmapDecoder` y promedia los píxeles. Con ese color se tiñen el fondo y los acentos. Una sola lectura sirve para la imagen y el color.
- Usa una firma de la portada (título + artista + app) para no recargar la misma imagen. Si el thumbnail aún no está listo, reintenta hasta 3 veces con espera creciente.
- Transición: un contador de versión (`_artworkTransitionVersion`) descarta animaciones viejas si la pista cambia a mitad de un fundido.
- Sin portada: muestra el icono de la app de origen como respaldo.

### 3.6 Robustez (lo que hace que "vaya tan bien")

- **Debounce**: `MediaPropertiesChanged` llega en ráfagas. Espera unos 180 ms y usa un contador de generación para ignorar refrescos obsoletos.
- **Hilos**: los eventos de SMTC llegan en hilos de fondo. Todo cambio de UI pasa por `DispatcherQueue.TryEnqueue`.
- **Nunca lanzar excepciones hacia la UI**: todo `Try*` captura el error, lo registra en el log y devuelve `false`/`null`.
- **Sesión preferida**: si el usuario elige una app concreta, se guarda su id. Si esa app desaparece, se cae a la sesión actual del sistema.

## 4. Checklist para un widget nuevo

1. Servicio con eventos y métodos `Try*Async` seguros.
2. ViewModel con `ObservableObject`, despacho a UI y `Dispose` limpio.
3. Vista XAML con bindings. Los gestos complejos van en el code-behind.
4. Adaptador registrado en el contenedor de widgets.
5. Textos en todos los `Strings/*.json`.
6. Icono de título completo (ver `AGENTS.md`).
7. Probar en AOT: `dotnet test ... -p:Platform=x64`. No uses reflexión.
