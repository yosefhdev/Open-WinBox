# PLAN — Seguridad y documentación para agentes de IA

Fecha: 2026-10-29. Versión de partida: `1.0.0-beta.1` (commit `cf0599a`). Plan anterior archivado en
`docs/plans/2026-10-27-pestanas-tipo-navegador.md`.

## Objetivo

1. **Seguridad:** que OpenWinBox sea confiable en cualquier PC con Windows.
   - No puede abrir la puerta a nada malicioso ni saltarse protecciones de Windows.
   - El usuario debe poder comprobar que el `.exe` que descarga es exactamente el que salió de este código, sin troyanos.
2. **Agentes de IA:** que cualquier agente (Claude Code, Codex, Copilot, Cursor, Gemini, Windsurf, Cline, Aider, Junie…) entienda:
   - la estructura, el funcionamiento, la seguridad y el rendimiento de la app;
   - cómo hacer cambios nuevos con el mismo orden y estilo.

## Reglas para ejecutar el plan

- **Orden:** un paso a la vez, en el orden de la checklist. Se puede usar `/ciclo` con este archivo.
- **Compilar:** cada paso compila sin errores:
  ```bash
  "/c/Program Files/dotnet/dotnet.exe" build OpenWinBox.csproj -nologo -v q
  ```
  Se corre desde la carpeta del proyecto y antes hay que cerrar la app.
- **Commits:** un commit por paso con la identidad del repo (Yosefh <yosefhknt@gmail.com>). No se hace push hasta que haya remoto.
- **Versión:** al terminar todo, `1.0.0-beta.2`, con su entrada en CHANGELOG y su etiqueta.

## Checklist

**Parte W — Widgets visibles tras Win + D** (prioridad: va antes de la Parte A)
- [x] W1. Restaurar sin activar el widget cuando Windows lo minimiza (Win + D)
- [x] W2. (Solo si W1 no basta) Subir los widgets sobre el escritorio mientras el escritorio está en primer plano

**Parte A — Seguridad del código**
- [ ] A0. Renombrar la carpeta `DeskBox/` → `OpenWinBox/` (antes de documentar rutas)
- [ ] A1. Auditoría de seguridad del código con hallazgos priorizados (`docs/security/audit-2026-10.md`)
- [ ] A2. Corregir hallazgos de red: URLs y nombres de archivo de Bing, límites de tamaño, un solo HttpClient
- [ ] A3. Corregir la apertura de archivos: no saltarse el bloqueo de Windows para ejecutables descargados
- [ ] A4. Manifiesto de la app: sin permisos de administrador, Windows 10/11, DPI
- [ ] A5. Privacidad: documentar y limitar lo que sale del equipo (ubicación, IP)

**Parte B — Cadena de suministro (que el .exe sea confiable)**
- [ ] B1. Dependencias bloqueadas: `packages.lock.json` + restauración en modo bloqueado
- [ ] B2. Compilación reproducible y determinista + script de publicación (instalador + SHA-256)
- [ ] B3. CI en GitHub Actions: build, CodeQL, escaneo de dependencias, atestación de origen (para cuando haya remoto)
- [ ] B4. Firma de código: elegir opción (SignPath para OSS, Azure Trusted Signing o sin firma) — **decisión del usuario**
- [ ] B5. `SECURITY.md`: cómo reportar vulnerabilidades y cómo verificar una descarga

**Parte C — Documentación para agentes de IA**
- [ ] C1. Investigar (web) qué archivo lee hoy cada agente
- [ ] C2. `AGENTS.md`: fuente única de verdad
- [ ] C3. Guías en `docs/dev/`: arquitectura, convenciones, seguridad, rendimiento, recetas
- [ ] C4. Archivos puente por agente (CLAUDE.md, GEMINI.md, Copilot, Cursor…) que solo apunten a AGENTS.md
- [ ] C5. `CONTRIBUTING.md` + plantilla de PR con checklist de seguridad y rendimiento
- [ ] C6. Prueba: un agente "en frío" agrega un widget de ejemplo siguiendo solo la documentación

**Cierre**
- [ ] D1. Versión `1.0.0-beta.2`, CHANGELOG, commit y etiqueta

---

## Parte W — Widgets visibles tras Win + D

**Problema:** Win + D (Mostrar escritorio) minimiza todo, incluidos los `WidgetWindow`. Deben quedar visibles siempre,
con "Mantener en el escritorio" (`_pinned`: `WS_EX_NOACTIVATE` + `SendToBottom`) y sin él. F7 / clic en la bandeja
(`App.ToggleWidgets`, `App.xaml.cs:268`) sigue ocultando/mostrando con `AppWindow.Hide()` / `ShowOnDesktop()`; eso
no es minimizar, así que no choca. El usuario no puede minimizar a propósito (`IsMinimizable = false`,
`WidgetWindow.xaml.cs:104`): **cualquier minimizado viene de fuera** y se puede deshacer sin bandera extra.

**Enfoque elegido:** (a) detectar el minimizado en el `AppWindow.Changed` que ya existe y restaurar sin activar.
Descartado (b) emparentar con Progman/WorkerW: frágil entre builds de Windows y rompe arrastre/imán/DPI.

**Comando de build** (en todos los pasos, desde la raíz del repo; cerrar antes la app):
```bash
taskkill //IM OpenWinBox.exe //F 2>/dev/null; "/c/Program Files/dotnet/dotnet.exe" build DeskBox/OpenWinBox.csproj -c Debug -nologo -v q
```
Debe terminar con 0 errores (y sin warnings nuevos).

### W1. Restaurar al ser minimizado (≈15 min) — no paralelizable

- **Archivo:** `DeskBox/WidgetWindow.xaml.cs`, método `OnAppWindowChanged` (≈línea 625).
  - Hoy, si el presenter está `Minimized`, hace `return` para no guardar la posición (-32000,-32000). Conservar ese
    "no guardar" y, en vez de solo salir, encolar la restauración:
    ```csharp
    if (sender.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } p)
    {
        // Win+D (u otra app) lo minimizó; un widget de escritorio no se minimiza nunca
        if (sender.IsVisible) DispatcherQueue.TryEnqueue(() => { if (_closed) return; p.Restore(false); if (_pinned) SendToBottom(); });
        return;
    }
    ```
  - `Restore(false)` = restaurar sin activar (no roba el foco). `TryEnqueue` evita restaurar dentro del propio evento.
  - Guardia `IsVisible`: un widget oculto con F7 no debe reaparecer por esto.
  - No tocar `ShowOnDesktop` (`:144`) ni `ToggleWidgets`: su manejo de "minimizado" queda como red de seguridad.
  - Ojo: el minimizado llega con `DidPositionChange/DidSizeChange`, pero el `return` está DESPUÉS del
    `if (!args.DidPositionChange && !args.DidSizeChange) return;`; está bien así (minimizar siempre mueve la ventana).
- **Criterio:**
  1. Build con 0 errores.
  2. Prueba manual, modo normal (Configuración → "Mantener en el escritorio" apagado), con 2+ widgets y alguna app
     abierta (Bloc de notas): Win + D → las apps se minimizan y **los widgets siguen visibles** en su sitio y tamaño;
     `config.json` (`%APPDATA%`/carpeta de config de la app) no queda con X/Y = -32000.
  3. Igual con "Mantener en el escritorio" encendido: widgets visibles tras Win + D y siguen al fondo (abrir una app
     encima los tapa; clic en el widget no le roba el foco a esa app).
  4. Win + D otra vez → Bloc de notas vuelve; los widgets siguen donde estaban.
  5. F7 oculta todos los widgets; Win + D con ellos ocultos NO los hace reaparecer; F7 de nuevo los muestra.
     Clic en el ícono de bandeja hace lo mismo.
  6. Si tras Win + D el widget no está minimizado (sigue `Restored`) pero no se ve, o queda tapado por el fondo del
     escritorio → pasar a W2. Para distinguirlo: `Debug.WriteLine(sender.Presenter is OverlappedPresenter o ? o.State : default)`
     temporal en `OnAppWindowChanged` y mirar la salida de depuración; quitarlo antes del commit.

### W2. Por encima del escritorio mientras está al frente (≈40 min) — solo si W1 no basta — no paralelizable, depende de W1

En Windows 11, Win + D puede, además de minimizar, traer la ventana del escritorio (`WorkerW`/`Progman`) al frente y
tapar las ventanas que no son topmost, sobre todo las que están en `HWND_BOTTOM` (modo "Mantener en el escritorio").
Es lo que hace Rainmeter: topmost mientras el escritorio es la ventana en primer plano; normal en cuanto deja de serlo.

- **Archivos:**
  - `DeskBox/App.xaml.cs`: un solo `SetWinEventHook(EVENT_SYSTEM_FOREGROUND (0x3), ..., WINEVENT_OUTOFCONTEXT (0)|WINEVENT_SKIPOWNPROCESS (2))`
    al arrancar (junto a donde se abren los widgets) y `UnhookWinEvent` en `Quit()`. Guardar el delegado en un campo
    (igual que `_subclassProc` en `WidgetWindow.Snap.cs:23`). En el callback: `GetClassName(hwnd)` es `"WorkerW"` o
    `"Progman"` → `w.SetAboveDesktop(true)` en cada widget de `_widgets.Values`; si no → `SetAboveDesktop(false)`.
    El callback llega en el hilo de UI (hook out-of-context en el hilo que lo instaló), no hace falta `TryEnqueue`.
  - `DeskBox/WidgetWindow.xaml.cs`: método nuevo `public void SetAboveDesktop(bool above)` junto a `SetPinnedToDesktop`:
    - si cambia el estado: `SetWindowPos(Hwnd, above ? HWND_TOPMOST : HWND_NOTOPMOST, 0,0,0,0, SWP_NOMOVE|SWP_NOSIZE|SWP_NOACTIVATE)`;
      al salir, si `_pinned`, `SendToBottom()`.
    - campo `bool _aboveDesktop`, y en `OnAppWindowChanged` la condición que arranca `_lowerTimer` debe exigir
      `!_aboveDesktop`; si no, el temporizador lo vuelve a mandar al fondo y deshace el topmost.
    - Constantes `HWND_TOPMOST = new(-1)`, `HWND_NOTOPMOST = new(-2)` junto a `HWND_BOTTOM` (`:710`).
- **Criterio:**
  1. Build con 0 errores.
  2. Repetir las pruebas 2–5 de W1: ahora los widgets se ven tras Win + D en ambos modos.
  3. Con el escritorio mostrado, clic en una app de la barra de tareas → la app queda ENCIMA de los widgets
     (el topmost se quitó); en modo "Mantener en el escritorio" los widgets vuelven al fondo.
  4. El menú contextual / renombrar de un widget sigue funcionando con el escritorio al frente.

---

## Parte A — Seguridad del código

### A0. Renombrar la carpeta del proyecto (≈10 min)

Hoy la carpeta se llama `DeskBox/` porque Windows la tenía bloqueada al renombrar.

- **Pasos:**
  1. Cerrar la app.
  2. `git mv DeskBox OpenWinBox`.
  3. Actualizar las rutas en README (10 idiomas), CHANGELOG, el acceso directo del menú Inicio y el de "hubs", la entrada de autoinicio y los scripts de rendimiento.
- **Criterio:**
  - `git grep -n "DeskBox/"` no da resultados.
  - La app compila y arranca desde la ruta nueva.
  - El autoinicio apunta al `.exe` nuevo.

### A1. Auditoría de seguridad (≈1 h)

Revisar todo el código buscando cada tipo de riesgo y escribir `docs/security/audit-2026-10.md`. Cada hallazgo lleva: archivo:línea, riesgo, severidad (alta/media/baja), corrección y estado.

| Superficie | Qué revisar |
|---|---|
| Red | Cada `HttpClient`; URLs armadas con datos de la respuesta; tamaño de descargas; solo HTTPS |
| Archivos | Rutas armadas con datos externos (path traversal); mover, copiar o borrar archivos del usuario; escrituras atómicas |
| Procesos | Cada `Process.Start` y `ShellExecute`: qué se ejecuta, con qué argumentos y si puede ejecutar algo no pedido |
| Registro | Solo `HKCU\...\Run\OpenWinBox`, sin nada más |
| COM e interop | `IPolicyConfig` (no documentada), `IShellItemImageFactory`, WScript.Shell |
| Entrada no confiable | `config.json` (lo puede editar otro programa), el layout del DeskBox original, JSON de las APIs |
| Comunicación entre procesos | Mutex y EventWaitHandle con nombre: qué puede provocar otro proceso |

**Hallazgos que ya detecté hoy** (para confirmar en la auditoría):

1. **Alta — redirección de la descarga de Bing** (`GlanceView.xaml.cs:163`).
   - Problema: la descarga arma `"https://www.bing.com" + url` con lo que dice la respuesta. Si `url` empezara con `@otro-sitio.com/...`, la descarga iría a otro servidor.
   - Corrección: armar la URL con `new Uri(baseUri, relative)` y comprobar que el host sigue siendo `www.bing.com`.
2. **Media — path traversal en la caché de imágenes** (`GlanceView.xaml.cs:161`).
   - Problema: el nombre del archivo sale de `hsh` de la respuesta. Un valor con `..\` podría escribir fuera de la caché.
   - Corrección: aceptar solo hexadecimal.
3. **Media — descargas sin límite de tamaño** (`GetByteArrayAsync` y `GetStringAsync`).
   - Corrección: `MaxResponseContentBufferSize` de unos 15 MB para imágenes y 1 MB para JSON.
4. **Alta — saltarse el bloqueo de Windows** (`FileActions.Open`).
   - Problema: si Windows bloquea un `.lnk` que apunta a un archivo descargado, la app abre el destino directamente. Eso está bien para una imagen, pero no debe hacerse con ejecutables ni scripts (.exe, .bat, .cmd, .ps1, .vbs, .js, .msi, .scr, .hta, .lnk…).
   - Corrección: en esos tipos, respetar el bloqueo y mostrar el aviso.
5. **Baja — un `HttpClient` nuevo en cada descarga** de Glance: unificarlo en un cliente compartido.
6. **Baja — argumento sin comillas** en `OpenAs_RunDLL` y en `explorer.exe /select`: revisar comillas y caracteres especiales en las rutas.

- **Criterio:** el documento existe y cada hallazgo tiene severidad y corrección propuesta.

### A2. Endurecer la red (≈45 min)

- **Archivos:** `GlanceView.xaml.cs`, `WeatherView.xaml.cs`, `SettingsWindow.xaml.cs`.
  - Un helper nuevo `Net.cs` con un `HttpClient` compartido: timeout, User-Agent `OpenWinBox/<versión>` y límite de tamaño.
- **Cambios:** corregir los hallazgos 1, 2, 3 y 5. Solo `https://` a hosts conocidos (lista en `Net.cs`).
- **Criterio:**
  - `git grep "new HttpClient"` da un solo resultado (en `Net.cs`).
  - Una prueba con JSON falso (`url = "@ejemplo.com/x"`, `hsh = "..\\x"`) no descarga ni escribe nada. Se hace con un harness de consola en el scratchpad, como el de `FileActions`.
  - Glance y Tiempo siguen funcionando.

### A3. Apertura segura de archivos (≈30 min)

- **Archivo:** `FileActions.cs`.
- **Cambio:** abrir el destino del acceso directo sin pasar por el bloqueo de Windows solo si NO es ejecutable ni script. Para esos tipos, mostrar "Windows bloqueó este archivo porque viene de internet" con la opción de abrir su carpeta.
- **Criterio:**
  - Una imagen descargada se sigue abriendo.
  - Un `.exe` descargado (con Zone.Identifier=3) detrás de un `.lnk` NO se ejecuta y se ve el aviso.

### A4. Manifiesto de la aplicación (≈20 min)

- **Archivos:** `app.manifest` nuevo y su `<ApplicationManifest>` en el csproj.
- **Contenido:**
  - `requestedExecutionLevel level="asInvoker"`: nunca pide administrador.
  - `supportedOS` Windows 10/11.
  - `dpiAwareness PerMonitorV2`.
  - `longPathAware`.
- **Criterio:**
  - `mt.exe -inputresource:OpenWinBox.exe -out:x.manifest` (o un harness) muestra `asInvoker`.
  - La app arranca sin aviso de UAC.

### A5. Privacidad (≈30 min)

- **Documentar en `docs/dev/security.md` y en el README** qué datos salen del equipo:
  - Coordenadas a Open-Meteo y BigDataCloud.
  - La IP a ipapi.co solo si la ubicación de Windows está apagada.
  - El idioma a Bing.
- Ninguna telemetría.
- **Opcional (decidir mañana):** una opción "Ubicación aproximada por IP" que se pueda apagar.
- **Criterio:** la lista coincide con un `git grep "https://"` del código.

## Parte B — Cadena de suministro

### B1. Dependencias bloqueadas (≈15 min)

- `RestorePackagesWithLockFile=true` en el csproj; commitear `packages.lock.json`.
- En CI: `dotnet restore --locked-mode`.
- **Criterio:** cambiar a mano una versión en el lock hace fallar la restauración.

### B2. Compilación reproducible y paquete con hash (≈45 min)

- **En el csproj:** `Deterministic`, `ContinuousIntegrationBuild` (en CI), `PathMap` para no incrustar rutas del equipo (`C:\Users\yosefh\...`) en el `.exe`/`.pdb`.
- **Script `tools/publish.ps1`:**
  1. `dotnet publish -c Release`.
  2. Instalador `OpenWinBox-<versión>-Setup.exe` (hecho: `tools/publish.ps1`, `tools/installer.iss`).
  3. `SHA256SUMS.txt`.
- Antes, revisar por qué Release mostró 520 MB de memoria residente y si la memoria privada es igual.
- **Criterio:**
  - Dos publicaciones seguidas dan el mismo SHA-256 del `.dll` principal.
  - `strings OpenWinBox.dll | grep yosefh` no encuentra nada.

### B3. CI en GitHub Actions (≈1 h, se activa al tener remoto)

- **`.github/workflows/build.yml`:** build y publicación en `windows-latest`, en push y PR.
- **`.github/workflows/codeql.yml`:** análisis de seguridad de C#.
- **`.github/dependabot.yml`:** NuGet y Actions.
- **`release.yml`:** al crear la etiqueta `v*`:
  1. Compila.
  2. Genera la atestación de origen (`actions/attest-build-provenance`).
  3. Sube el instalador y `SHA256SUMS.txt` a Releases.
- Las Actions se fijan por SHA, no por etiqueta.
- **Criterio:** `actionlint` sin errores. Al tener remoto: un push corre build y CodeQL en verde.

### B4. Firma de código — DECISIÓN DEL USUARIO

Sin firma, SmartScreen muestra "Windows protegió su PC" y algunos antivirus desconfían más.

| Opción | Costo | Nota |
|---|---|---|
| **SignPath Foundation** | Gratis para OSS | Hay que solicitarlo; firma desde CI; requiere repo público y licencia OSI (MIT sirve) |
| **Azure Trusted Signing** | ≈ USD 10/mes | Requiere validación de identidad; integración con GitHub Actions |
| Certificado OV/EV tradicional | USD 200–600/año | Más trámite |
| Sin firma (por ahora) | 0 | Compensar con hash SHA-256 + atestación + VirusTotal en cada release |

- **Recomendación:** empezar sin firma (betas) + atestación + hashes, y pedir SignPath al hacer público el repo.
- **Criterio:** decisión anotada en `docs/dev/security.md`.

### B5. SECURITY.md (≈20 min)

- **Cómo reportar una vulnerabilidad:** por privado (GitHub Security Advisories), con un tiempo de respuesta.
- **Versiones soportadas:** la beta más reciente.
- **Cómo verificar una descarga:** `Get-FileHash` contra `SHA256SUMS.txt` y `gh attestation verify`.
- **Criterio:** el archivo existe y el README enlaza a él.

## Parte C — Documentación para agentes de IA

**Idea central:** un solo lugar con la verdad (`AGENTS.md` + `docs/dev/`). Los archivos de cada herramienta solo apuntan ahí, para que nunca se contradigan.

### C1. Investigar convenciones actuales (≈20 min)

Confirmar en la web qué archivo lee hoy cada agente (cambian seguido):

| Agente | Archivo esperado |
|---|---|
| Estándar abierto (Codex, Copilot agent, Cursor, Jules, Zed…) | `AGENTS.md` |
| Claude Code | `CLAUDE.md` (puede importar con `@AGENTS.md`) |
| Gemini CLI | `GEMINI.md` (o configurar que lea `AGENTS.md`) |
| GitHub Copilot | `.github/copilot-instructions.md` |
| Cursor | `.cursor/rules/*.mdc` |
| Windsurf | `.windsurf/rules/` |
| Cline | `.clinerules` |
| Aider | `CONVENTIONS.md` (`--read`) |
| JetBrains Junie | `.junie/guidelines.md` |

- **Criterio:** la tabla queda confirmada o corregida, con enlaces, en `docs/dev/ai-agents.md`.

### C2. AGENTS.md (≈45 min)

**Contenido**, corto y escaneable (menos de 200 líneas):

1. **Qué es la app** y su stack: WinUI 3, .NET 8, sin empaquetar, Windows App SDK self-contained.
2. **Comandos exactos:** compilar, ejecutar, cerrar la app antes de compilar, medir rendimiento (`tools/perf.ps1`).
3. **Mapa del código:** un renglón por archivo y qué hace.
4. **Reglas que no se rompen:**
   - Seguridad: lista de A1–A5.
   - Rendimiento: CPU en reposo menor a 0.1 s/min, sin `ProgressBar` animada, temporizadores mínimos, caché de llamadas COM caras.
   - Nada de `Windows.Storage` en modo sin empaquetar.
   - Ventanas `WS_EX_NOACTIVATE` + `WS_EX_TOOLWINDOW`.
   - Escritura atómica de la config.
   - Textos de UI en español.
   - Comentarios cortos en español que explican el porqué.
5. **Flujo de trabajo:**
   1. Plan.
   2. Cambio mínimo.
   3. Compilar.
   4. Probar en pantalla.
   5. Medir CPU si se agregó un timer o un evento.
   6. CHANGELOG y versión beta.
6. **Enlaces** a las guías de `docs/dev/`.

**Criterio:** cada comando del archivo se probó y funciona tal cual.

### C3. Guías en docs/dev/ (≈2 h)

| Archivo | Contenido |
|---|---|
| `architecture.md` | Ciclo de vida (App → ShellHost → WidgetWindow), config y migraciones, ventanas, imán, pestañas, widgets funcionales, diagrama |
| `conventions.md` | Nombres, comentarios, estilo XAML (tokens de tema, `SettingRow`), menús con `ShowPopup`, idioma de UI, CRLF |
| `security.md` | Modelo de amenazas, reglas (red, archivos, procesos, registro), datos que salen del equipo, firma y releases |
| `performance.md` | Presupuestos (CPU en reposo, arranque, memoria), cómo medir con `tools/perf.ps1`, anti-patrones encontrados (ProgressBar, FriendlyName, timers de 1 s) |
| `winui-gotchas.md` | Lo aprendido: Windows.Storage, íconos en STA, acrílico sin activar, coordenadas DIP vs. físicas, `ShouldConstrainToRootBounds`… |
| `recipes/add-functional-widget.md` | Paso a paso real: `FeatureKinds`, `DefaultSize`, `FeatureTitle`, `SetupFeature`, fila y subpágina en Configuración, clase de opciones, `FeatureOptionsChanged` |
| `recipes/add-setting.md` | Propiedad en `AppConfig`, fila `SettingRow`, carga y guardado |
| `recipes/release.md` | Subir beta, CHANGELOG, etiqueta, publicar |

- Mover `tools/perf.ps1` y `perf-split.ps1` desde el scratchpad al repo.
- **Criterio:** cada receta se sigue tal cual sobre el código actual, sin pasos faltantes. Lo comprueba el paso C6.

### C4. Archivos puente (≈20 min)

- **`CLAUDE.md`:** `@AGENTS.md` y nada más (salvo particularidades de Claude Code).
- **`GEMINI.md`, `.github/copilot-instructions.md`, `.cursor/rules/openwinbox.mdc`, `.windsurf/rules/openwinbox.md`, `.clinerules`, `CONVENTIONS.md`, `.junie/guidelines.md`:**
  - 3–5 líneas cada uno.
  - Contenido: "Lee AGENTS.md y docs/dev/ antes de cambiar código" + las 5 reglas más importantes.
- **Criterio:**
  - Ningún archivo puente pasa de 15 líneas.
  - Todos enlazan a `AGENTS.md`.

### C5. CONTRIBUTING.md y plantilla de PR (≈30 min)

- **`CONTRIBUTING.md`:** cómo compilar, el estilo, cómo proponer cambios y la política de versiones.
- **`.github/pull_request_template.md`** con checklist:
  - [ ] compila
  - [ ] probado en pantalla
  - [ ] sin red nueva sin revisar
  - [ ] sin `Process.Start` nuevo sin revisar
  - [ ] CPU en reposo medida si hay timers
  - [ ] CHANGELOG
- **Criterio:** los archivos existen y el README enlaza a CONTRIBUTING.

### C6. Prueba "en frío" con un agente (≈45 min)

- Lanzar un subagente sin contexto de esta conversación. Solo recibe: "Lee AGENTS.md y agrega un widget funcional de ejemplo 'Notas' siguiendo la receta".
- Revisar si siguió la estructura, las reglas de seguridad y rendimiento, y si compiló.
- Corregir la documentación donde dudó o se equivocó.
- Descartar el widget de ejemplo, salvo que el usuario lo quiera.
- **Criterio:**
  - El agente termina sin preguntar cosas que ya están en la documentación.
  - El widget compila y respeta las reglas.

## Cierre

### D1. Beta 2 (≈10 min)

- `<Version>1.0.0-beta.2</Version>` y CHANGELOG con lo hecho.
- Commit y `git tag -a v1.0.0-beta.2`.
- **Criterio:** Configuración → Acerca muestra `1.0.0-beta.2`.

## Estimado total

Unas 9–10 h de trabajo, repartidas así:

| Parte | Tiempo |
|---|---|
| A (seguridad del código) | ≈3 h |
| B (cadena de suministro) | ≈3 h, pero B3 solo se puede validar del todo con remoto |
| C (agentes) | ≈4 h |

Si mañana hay poco tiempo, la prioridad es:

1. A1–A3 (riesgos reales).
2. C2 + C4 (AGENTS.md y puentes).
3. B1–B2.
4. El resto.

## Decisiones pendientes del usuario

1. **Firma de código (B4):** SignPath (gratis, requiere repo público), Azure Trusted Signing (≈USD 10/mes) o sin firma durante las betas.
2. **Ubicación por IP (A5):** ¿se deja como respaldo automático o como opción que el usuario activa?
3. **Remoto:** dónde vivirá el repo (GitHub recomendado para Actions, CodeQL, atestaciones y Security Advisories).
