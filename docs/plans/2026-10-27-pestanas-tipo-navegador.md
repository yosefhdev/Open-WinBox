# PLAN — Widgets de archivos: botón "+", menú "⋯", bloquear, agrupar/separar pestañas

## Contexto

OpenWinBox (WinUI 3 / WinAppSDK 1.6, .NET 8, sin empaquetar). Código en `DeskBox\`.
Un widget de archivos (`WidgetConfig.Kind == "File"`) es una ventana `WidgetWindow`; cada carpeta de
`WidgetConfig.Folders` es una pestaña (`BuildTabs` en `WidgetWindow.xaml.cs`). Datos por carpeta en
`Order/Icons/Titles` (diccionarios con la ruta de la carpeta como clave). `App` mantiene
`_widgets: Dictionary<WidgetConfig, WidgetWindow>`; cambiar las pestañas = cerrar y reabrir la ventana
(patrón ya usado por `App.AddFolder`/`App.RemoveFolder`).

Hoy: botón X (columna 2 del Grid de la barra en `WidgetWindow.xaml`) llama `Hide_Click` → `AppWindow.Hide()`.
"Desactivar widget" (= `App.RemoveWidget`, no borra la carpeta) y "Agrupar otra carpeta…" viven en el menú
de clic derecho de la pestaña (`ShowTitleMenu`). La ventana se mueve con arrastre nativo desde `DragBar`
(`SetTitleBar(DragBar)`), con imán en el subclass proc `OnWindowMessage` (`WidgetWindow.Snap.cs`).

### Decisiones

- **Widget/pestaña nueva vacía**: sin diálogo. Se crea la carpeta
  `%LOCALAPPDATA%\OpenWinBox\widgets\Nuevo widget` (si existe: `FileActions.UniquePath` → "Nuevo widget (2)"…).
  El título visible es el nombre de esa carpeta (el usuario lo cambia con "Cambiar nombre").
- **Eliminar un widget o pestaña nunca borra la carpeta del disco** (puede tener archivos del usuario).
- Unir: las pestañas del widget origen se agregan **al final** del destino, el destino conserva posición y
  tamaño, la pestaña activa pasa a ser la primera de las agregadas. Carpetas repetidas se omiten.
- Separar: la pestaña sale con sus `Order/Icons/Titles`; el widget nuevo usa el tamaño del original y
  queda con la esquina superior izquierda bajo el cursor. Si el original queda con 1 pestaña, se ve normal
  (ya ocurre solo: `BuildTabs` usa `Folders.Count > 1`).
- Si en el origen `Name` está puesto y tiene una sola carpeta sin `Titles[folder]`, al unir se copia
  `Name` a `Titles[folder]` para que no cambie el nombre visible.
- Bloquear se guarda en config (`WidgetConfig.IsLocked`) y solo impide mover (redimensionar sigue igual).
- Fuera de alcance (no pedido): soltar una **pestaña** sobre otro widget para pasarla ahí; animación de
  reordenamiento; deshacer.

### Build (criterio común a todos los pasos)

Desde `C:\Users\yosefh\dev-projects\PERSONAL\DeskBox-2.0\DeskBox`:
`"/c/Program Files/dotnet/dotnet.exe" build OpenWinBox.csproj -nologo -v q` → 0 errores.
Antes de compilar cerrar DeskBox si está abierto (el .exe bloqueado rompe el build). Para probar: ejecutar
`bin\...\OpenWinBox.exe` (la ruta que deje el build) y revisar `%LOCALAPPDATA%\OpenWinBox\config.json`.
Sin tests unitarios: la verificación es build + prueba manual descrita en cada paso.

---

## Checklist

- [x] Paso 0 — Preparación (build base y app corriendo)
- [x] Paso 1 — Helpers en `App`: nuevo widget vacío, nueva pestaña vacía, unir, separar, reabrir
- [x] Paso 2 — Botón "+" con menú (Nuevo widget / Nueva pestaña en este widget) (código aprobado; prueba en pantalla pendiente del usuario)
- [x] Paso 3 — X → "⋯" con menú (Cambiar nombre, Cambiar icono, Ocultar, Desactivar) (código aprobado; prueba en pantalla pendiente del usuario)
- [x] Paso 4 — Bloquear / Desbloquear (código aprobado; prueba en pantalla pendiente)
- [x] Paso 5 — "Agrupar con otro widget" (submenú) (código aprobado; prueba en pantalla pendiente)
- [x] Paso 6 — Reordenar pestañas arrastrándolas (código aprobado; prueba en pantalla pendiente)
- [x] Paso 7 — Arrastrar una pestaña fuera del widget para separarla (código aprobado; prueba en pantalla pendiente)
- [x] Paso 8 — Arrastrar un widget 1.5 s sobre la barra de otro para unirlos (código aprobado tras fix del overlay; prueba en pantalla pendiente)
- [ ] Paso 9 — Prueba de regresión final

---

## Paso 0 — Preparación

- No paralelizable (los demás dependen de él).
- Acción: cerrar DeskBox (bandeja → Salir), respaldar `%LOCALAPPDATA%\OpenWinBox\config.json` a
  `config.backup.json`, compilar con el comando de Build, abrir la app.
- Aceptación: build 0 errores; los widgets de archivos actuales aparecen igual que antes.

## Paso 1 — Helpers en `App` (modelo)

- No paralelizable (escribe código; base de los pasos 2, 5, 7, 8).
- Archivos: `DeskBox\App.xaml.cs` (junto a `AddWidget`/`AddFolder`/`RemoveFolder`), `DeskBox\WidgetConfig.cs`
  (solo si hace falta una propiedad; `IsLocked` se agrega en el paso 4).
- Enfoque (mínimo, reutilizando el patrón cerrar+`OpenWidget` existente):
  - `private void Reopen(WidgetConfig w)`: `if (_widgets.Remove(w, out var win)) win.Close(); OpenWidget(w);`
    y usarlo en `AddFolder`/`RemoveFolder` (quita duplicación).
  - `public static string NewEmptyFolder()`: `Directory.CreateDirectory(FileActions.UniquePath(<LOCALAPPDATA>\OpenWinBox\widgets, "Nuevo widget"))`
    (crear antes la carpeta base `widgets`), devuelve la ruta.
  - `AddWidget(string folder, PointInt32? at = null)`: si `at` viene, `X/Y = at`; si no, centrado (actual).
    Si hay un parámetro opcional de tamaño (para separar), aceptar `int width = 400, int height = 500`.
  - `public void AddEmptyWidget()` → `AddWidget(NewEmptyFolder())`. Para que no salga encima del actual,
    se puede pasar la posición del widget origen + 30 px (opcional, decidir lo más simple).
  - `public void AddEmptyTab(WidgetConfig w)` → `AddFolder(w, NewEmptyFolder())`.
  - `public void MergeWidgets(WidgetConfig target, WidgetConfig source)`: por cada carpeta de `source` que
    no esté en `target.Folders` (comparación `OrdinalIgnoreCase`): agregar a `Folders` y copiar
    `Order/Icons/Titles` de esa carpeta; aplicar la regla de `Name` (ver Decisiones);
    `target.ActiveIndex` = índice de la primera agregada; cerrar ventana de `source` y quitarlo de
    `Config.Widgets` (sin tocar disco); `Save()`; `Reopen(target)`. Ignorar si `target == source` o algún
    `Kind != "File"`.
  - `public void SplitFolder(WidgetConfig source, string folder, PointInt32 at)`: solo si
    `source.Folders.Count > 1`. Crear `WidgetConfig` nuevo con `Folders={folder}`, `X/Y=at`, ancho/alto del
    origen y los `Order/Icons/Titles` de esa carpeta movidos; luego `RemoveFolder(source, folder)`
    (ya limpia diccionarios, ajusta ActiveIndex y reabre); agregar el nuevo a `Config.Widgets`, `Save()`,
    `OpenWidget`.
  - Al cerrar una ventana desde su propio handler (merge/split disparado desde esa ventana) encolar con
    `DispatcherQueue.TryEnqueue` en el llamador si se cierra la ventana dentro de su subclass proc o de un
    evento de puntero (se usa en pasos 7 y 8).
- Aceptación: build 0 errores. Sin cambio visible aún; widgets existentes abren igual y "Agrupar otra
  carpeta como pestaña…" / "Quitar esta pestaña" del clic derecho siguen funcionando (ahora vía `Reopen`).

## Paso 2 — Botón "+" con menú

- No paralelizable (modifica `WidgetWindow.xaml` igual que el paso 3).
- Archivos: `DeskBox\WidgetWindow.xaml` (Grid de la barra: `ColumnDefinitions="Auto,*,Auto"` →
  `"Auto,*,Auto,Auto"`, nuevo `Button` en columna 2, el actual X pasa a columna 3);
  `DeskBox\WidgetWindow.xaml.cs` (handler `Add_Click`, junto a `Hide_Click`).
- Enfoque: botón con glifo `&#xE710;` (Add, Segoe Fluent Icons), mismo estilo que el X (FontSize 10,
  Padding 8,6, fondo transparente), `ToolTipService.ToolTip="Nuevo widget o pestaña"`,
  `AutomationProperties.Name="Agregar"`. En `Add_Click`: `MenuFlyout` con
  `Entry("Nuevo widget", "&#xE8A0;"…, App.Current.AddEmptyWidget)` y
  `Entry("Nueva pestaña en este widget", …, () => App.Current.AddEmptyTab(_config))`, mostrado con
  `ShowPopup(menu, (FrameworkElement)sender)` (para que no lo tapen otros widgets).
  En widgets funcionales la barra tiene alto 0, así que no se ve: no requiere condición extra.
- Aceptación (build 0 errores, luego en pantalla):
  1. Cada widget de archivos muestra "+" a la izquierda de la X; el tooltip dice "Nuevo widget o pestaña".
  2. "+" → "Nuevo widget": aparece una ventana nueva titulada "Nuevo widget" vacía; existe la carpeta
     `%LOCALAPPDATA%\OpenWinBox\widgets\Nuevo widget`; arrastrar un acceso directo desde el escritorio a esa
     ventana lo agrega; `config.json` tiene un widget nuevo con esa carpeta en `Folders`.
  3. Repetir "Nuevo widget": se crea "Nuevo widget (2)" (no se reutiliza la carpeta).
  4. "+" → "Nueva pestaña en este widget" en un widget de 1 pestaña: ahora muestra 2 pestañas, la nueva
     seleccionada y vacía; `config.json` tiene 2 rutas en `Folders` de ese widget.

## Paso 3 — X → "⋯" con menú

- No paralelizable (modifica `WidgetWindow.xaml`/`.xaml.cs`; depende del paso 2 por la columna).
- Archivos: `DeskBox\WidgetWindow.xaml` (botón de columna 3: glifo `&#xE712;` More, tooltip
  "Opciones del widget", `AutomationProperties.Name="Opciones"`, `Click="More_Click"`);
  `DeskBox\WidgetWindow.xaml.cs` (`Hide_Click` → `More_Click`; reutilizar `Entry`, `ShowPopup`,
  `RenameTitleAsync`, `PickIcon`).
- Enfoque: `MenuFlyout` con
  - "Cambiar nombre" → `RenameTitleAsync(<botón de la pestaña activa>, ActiveFolder)` (renombra lo que se ve
    junto al icono de la pestaña activa; ancla = `TabsPanel.Children[_config.ActiveIndex]`).
  - "Cambiar icono…" → `PickIcon(ActiveFolder)` (IconPicker ya ofrece emoji/imagen/restablecer).
  - (paso 4 agrega Bloquear, paso 5 agrega "Agrupar con…")
  - separador; "Ocultar (F7 para mostrar)" → `AppWindow.Hide()` (lo que hacía la X).
  - "Desactivar widget" en rojo (`SystemFillColorCriticalBrush`) → `App.Current.RemoveWidget(_config)`.
  Mantener el menú de clic derecho de la pestaña tal cual.
- Aceptación (build 0 errores):
  1. La X ya no existe; en su lugar "⋯". Clic → menú con Cambiar nombre, Cambiar icono…, Ocultar, Desactivar.
  2. Cambiar nombre → escribir "Juegos" + Enter → la pestaña activa muestra "Juegos";
     `config.json` → `Titles["<carpeta>"] = "Juegos"`. Con nombre vacío vuelve al nombre de la carpeta.
  3. Cambiar icono… → elegir 🎮 → la pestaña muestra 🎮; `Icons["<carpeta>"] = "emoji:🎮"`.
  4. Ocultar → el widget desaparece; F7 lo vuelve a mostrar.
  5. Desactivar → la ventana se cierra, sale de `config.json` y la carpeta sigue en disco.
  6. El menú se ve completo aunque el widget esté detrás de otro (lo sube `ShowPopup`).

## Paso 4 — Bloquear / Desbloquear

- No paralelizable.
- Archivos: `DeskBox\WidgetConfig.cs` (`public bool IsLocked { get; set; }` en `WidgetConfig`);
  `DeskBox\WidgetWindow.Snap.cs` (`OnWindowMessage`, casos `WM_ENTERSIZEMOVE`/`WM_MOVING`);
  `DeskBox\WidgetWindow.xaml(.cs)` (entrada en `More_Click`, indicador visual).
- Enfoque:
  - Menú "⋯": `ToggleMenuFlyoutItem { Text = "Bloquear posición", IsChecked = _config.IsLocked }`
    (icono `&#xE72E;` Lock); al hacer clic: invertir `IsLocked`, `_save()`, actualizar indicador.
  - En `WM_MOVING` con `_config.IsLocked`: escribir en `lParam` el rect de `GetWindowRect` tomado en
    `WM_ENTERSIZEMOVE` (guardarlo en un campo nuevo o en `_startRect` antes del ancla) y `return 1`, sin imán.
    Así el arrastre nativo de `DragBar` no mueve la ventana. `WM_SIZING` sigue funcionando.
    (Alternativa si se ve parpadeo: no hay; WM_MOVING ajusta antes de mover.)
  - Indicador: un `FontIcon` `&#xE72E;` pequeño (FontSize 10, color secundario) en la barra (p. ej. dentro
    de `DragBar` antes de `StatusText`, o columna propia) visible solo si `IsLocked`, con tooltip
    "Bloqueado: desbloquéalo desde ⋯ para moverlo". Así el usuario entiende por qué no se mueve.
- Aceptación (build 0 errores):
  1. "⋯" → "Bloquear posición" → aparece el candado; arrastrar desde la barra no mueve el widget;
     `config.json` → `"IsLocked": true`. Redimensionar desde el borde sí funciona.
  2. Cerrar y abrir la app → sigue bloqueado (candado visible, no se mueve).
  3. "⋯" → desmarcar → candado desaparece, se mueve con imán como antes; `"IsLocked": false`.

## Paso 5 — "Agrupar con otro widget" (submenú)

- No paralelizable (depende de los pasos 1 y 3).
- Archivos: `DeskBox\WidgetWindow.xaml.cs` (`More_Click`); `DeskBox\App.xaml.cs` (exponer la lista:
  p. ej. `public IEnumerable<WidgetConfig> FileWidgets => _widgets.Keys.Where(w => w.Kind == "File")`);
  `DeskBox\IconPicker.cs` (helper pequeño para icono de menú).
- Enfoque:
  - `MenuFlyoutSubItem { Text = "Agrupar con…" }` con un `MenuFlyoutItem` por cada widget de archivos
    abierto distinto de `_config`. Texto = `Name` o el título de su pestaña activa (mover el cálculo de
    `TitleFor` a un helper estático o en `WidgetConfig` si hace falta, sin duplicar lógica); si tiene
    varias pestañas, sufijo " (3 pestañas)". Icono: `IconPicker.MenuIcon(spec)` nuevo →
    `emoji:` = `FontIcon { Glyph = emoji, FontFamily = "Segoe UI Emoji" }`,
    `image:` = `BitmapIcon { UriSource = new Uri(path), ShowAsMonochrome = false }`,
    sin spec = `FontIcon` carpeta (mismo glifo que `Render`).
  - Clic → `App.Current.MergeWidgets(_config, other)` (las pestañas del otro se suman a este; el otro se
    cierra). Encolar con `DispatcherQueue.TryEnqueue` porque la ventana actual se recrea.
  - Si no hay otros widgets: submenú deshabilitado (`IsEnabled = false`) con un hijo "No hay otros widgets".
- Aceptación (build 0 errores):
  1. Con widgets A (pestaña "Juegos" 🎮) y B ("Docs"): en A "⋯" → "Agrupar con…" lista "🎮"/"Docs" con su
     icono, sin listar A ni widgets funcionales.
  2. Elegir "Docs" → B se cierra; A muestra pestañas "Juegos" y "Docs" con "Docs" seleccionada; los íconos
     y orden de archivos de Docs se conservan.
  3. `config.json`: B ya no está; A tiene ambas carpetas en `Folders` y las entradas de B en
     `Order/Icons/Titles`.
  4. Con un solo widget de archivos: "Agrupar con…" aparece deshabilitado.

## Paso 6 — Reordenar pestañas arrastrándolas

- No paralelizable (base del paso 7: misma lógica de arrastre).
- Archivos: `DeskBox\WidgetWindow.xaml.cs` (`BuildTabs`; nueva región "Arrastre de pestañas" o
  `WidgetWindow.Tabs.cs` parcial si queda grande).
- Enfoque (drag manual, no DragDrop de WinUI):
  - En cada botón de pestaña: `tab.AddHandler(PointerPressedEvent, handler, handledEventsToo: true)`
    (Button marca el evento como manejado), lo mismo para `PointerMovedEvent` y `PointerReleasedEvent`;
    guardar índice y `GetCursorPos` inicial; `tab.CapturePointer(e.Pointer)`.
  - Umbral de 6 px antes de considerarlo arrastre (un clic normal sigue seleccionando la pestaña). Si hubo
    arrastre, suprimir el `Click` siguiente (flag `_tabDragged`).
  - Durante el arrastre: `tab.Opacity = 0.6`; calcular el índice destino con la X del puntero relativa a
    `TabsPanel` comparándola con el centro de cada hijo (`TransformToVisual(TabsPanel)` + `ActualWidth`).
  - Al soltar dentro de la ventana con índice distinto: mover en `_config.Folders`
    (`RemoveAt`/`Insert`), ajustar `ActiveIndex` para que siga apuntando a la misma carpeta, `_save()`,
    `BuildTabs()`. No hace falta recrear la ventana (las caches son por carpeta).
  - `PointerCaptureLost` → cancelar y restaurar opacidad.
  - Solo con `Folders.Count > 1`.
- Aceptación (build 0 errores):
  1. Widget con pestañas A, B, C: arrastrar A hasta la derecha de C y soltar → orden B, C, A; la pestaña
     seleccionada sigue siendo la misma carpeta y su contenido no cambia.
  2. Cerrar/abrir la app → orden B, C, A persiste (`Folders` en ese orden en `config.json`).
  3. Clic simple en una pestaña sigue cambiando de pestaña; clic derecho sigue abriendo su menú.
  4. Arrastrar desde `DragBar` sigue moviendo la ventana (no se rompe el arrastre nativo).

## Paso 7 — Arrastrar una pestaña fuera para separarla

- No paralelizable (depende de los pasos 1 y 6).
- Archivos: `DeskBox\WidgetWindow.xaml.cs` (handlers del paso 6), `DeskBox\WidgetWindow.Snap.cs`
  (reutilizar `VisualBounds`, `GetCursorPos`, `RECT/POINT`), `DeskBox\WidgetWindow.xaml` (texto de ayuda).
- Enfoque:
  - En `PointerMoved` del arrastre de pestaña, con `GetCursorPos` comparar contra `VisualBounds(Hwnd)`
    inflado ~20 px (escalado por DPI). Fuera → modo "separar": `StatusText.Text = "Suelta para separar"`
    y opacidad 0.4 en la pestaña; dentro → vuelve a modo reordenar.
  - Al soltar en modo separar y `Folders.Count > 1`:
    `DispatcherQueue.TryEnqueue(() => App.Current.SplitFolder(_config, folder, new PointInt32(cursor.X - 20, cursor.Y - 10)))`.
    El widget nuevo pasa por `FitToScreen` en su constructor (ya existe).
  - Con 1 sola pestaña no se hace nada (no hay drag de pestaña: el widget se mueve con la barra).
  - Mientras se arrastra la ventana tiene `CapturePointer`, así que `PointerMoved` llega aunque el cursor
    salga de la ventana.
- Aceptación (build 0 errores):
  1. Widget con pestañas Juegos, Docs: arrastrar "Docs" al escritorio vacío → durante el arrastre fuera se lee
     "Suelta para separar"; al soltar aparece un widget nuevo "Docs" con su esquina superior izquierda junto
     al cursor, mismo tamaño, mismo icono y orden de archivos.
  2. El original queda con "Juegos" como widget normal (sin estilo de pestañas: título en negrita).
  3. `config.json`: dos widgets, cada uno con una carpeta; `Icons/Titles/Order` de Docs movidos al nuevo.
  4. Arrastrar fuera y volver a entrar antes de soltar → no separa; se comporta como reordenar.
  5. Soltar fuera de toda pantalla (borde de monitor) → el widget queda dentro del área visible.

## Paso 8 — Arrastrar un widget 1.5 s sobre la barra de otro para unirlos

- No paralelizable (depende del paso 1).
- Archivos: `DeskBox\WidgetWindow.Snap.cs` (`OnWindowMessage`: `WM_MOVING`, `WM_EXITSIZEMOVE`; lista `All`),
  `DeskBox\WidgetWindow.xaml` (overlay nuevo), `DeskBox\WidgetWindow.xaml.cs` (método público para
  mostrar/ocultar overlay).
- Enfoque:
  - Overlay en el destino: `Border x:Name="MergeOverlay"` en `Root` (`Grid.RowSpan="2"`, `Canvas.ZIndex="11"`,
    `IsHitTestVisible="False"`, `Opacity="0"`, `OpacityTransition` 0.2 s como `Handle`), borde de acento
    2 px, fondo acento semitransparente y `TextBlock` centrado "Suelta para unir" (FontSize 14 SemiBold).
    Al empezar a esperar (cursor entra a la barra): mostrar el borde con un `ProgressBar`/barra que se llena
    o un pulso (p. ej. `Scale` 0.98→1) como "cargando"; a los 1.5 s el texto cambia a "Suelta para unir".
    Lo más simple: opacidad 0.5 con texto "Mantén para unir…" y a 1.5 s opacidad 1 + "Suelta para unir".
  - Origen, en `WM_MOVING` (solo si `IsFileWidget` y no bloqueado): con `GetCursorPos` buscar en `All` una
    ventana `w != this`, visible, `w.IsFileWidget`, cuyo rect de barra (`VisualBounds(w.Hwnd)` con
    `B = T + 36 * escala`) contenga el cursor. Guardar `_mergeTarget` y `_mergeSince = Environment.TickCount64`
    al cambiar de destino; si cambia/desaparece, ocultar overlay del anterior.
  - Un `DispatcherQueueTimer` (100 ms) mientras hay `_mergeTarget` decide la fase visual (≥1500 ms →
    "Suelta para unir"), porque si el mouse está quieto no llegan `WM_MOVING`. Además, en
    `WM_EXITSIZEMOVE` se decide por tiempo (`TickCount64 - _mergeSince >= 1500`), así funciona aunque el
    timer no avance dentro del bucle modal de movimiento.
  - En `WM_EXITSIZEMOVE`: ocultar overlay; si la condición se cumple:
    `DispatcherQueue.TryEnqueue(() => App.Current.MergeWidgets(target._config, _config))` (no cerrar la
    ventana dentro de su propio subclass proc). Limpiar `_mergeTarget`.
  - Para el destino `HWND_BOTTOM` puede quedar tapado por el origen: el overlay va en el destino pero el
    cursor está sobre su barra, que el origen tapa solo si se superponen; aceptar (el texto también puede
    mostrarse en `StatusText` del origen: "Suelta para unir con «Docs»"). Recomendado: ambos.
  - El imán sigue activo durante este arrastre (no interferir con `SnapRect`).
- Aceptación (build 0 errores):
  1. Widgets A y B separados: arrastrar A por su barra hasta que el cursor quede sobre la barra de B y
     esperar quieto → B muestra el borde/overlay "Mantén para unir…" y a ~1.5 s cambia a "Suelta para unir".
  2. Soltar → A se cierra, B muestra sus pestañas + las de A al final, con la primera de A seleccionada;
     `config.json` con un widget menos.
  3. Pasar por encima de la barra de B sin detenerse (<1.5 s) y soltar en otro lado → no se unen; overlay
     desaparece.
  4. Llegar a 1.5 s y luego sacar el cursor de la barra antes de soltar → overlay se va, no se unen.
  5. Soltar encima del área de archivos de B (no de la barra) → no se unen, solo se mueve.
  6. Widgets funcionales (Música, Glance…) nunca muestran overlay ni se unen.

## Paso 9 — Regresión final

- Paralelizable entre escenarios (solo verificación, sin escribir código) una vez hechos los pasos 1–8.
- Escenarios:
  1. Build 0 errores y 0 warnings nuevos.
  2. Soltar archivos externos en un widget sigue funcionando (acceso directo creado / .lnk movido).
  3. Reordenar íconos dentro de un widget sigue guardando `Order`.
  4. Imán al mover/redimensionar y brillo en bordes siguen funcionando.
  5. F7 oculta/muestra todos; "Fijar al escritorio" sigue mandando los widgets al fondo tras usar los menús.
  6. Configuración › Widgets lista los widgets nuevos/unidos/separados correctamente y "Agregar carpeta" /
     "Quitar" desde ahí siguen funcionando.
  7. Música/Glance/Tiempo: sin barra, sin "+"/"⋯", agarradera y menú de clic derecho intactos.
  8. Restaurar `config.backup.json` si se quiere volver al estado inicial.
