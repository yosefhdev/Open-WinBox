<div align="center">

<img src="../icon.png" width="96" alt="Icono de OpenWinBox" />

# OpenWinBox

**Widgets de código abierto para el escritorio de Windows: tus carpetas, música, reloj, clima y audio, a la vista.**

[![Descargar](https://img.shields.io/badge/Descargar-Instalador%20para%20Windows-E9705A?style=for-the-badge)](https://github.com/yosefhdev/Open-WinBox/releases)

![Versión](https://img.shields.io/badge/versión-1.0.0--beta.3-orange)
![Plataforma](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4)
![Licencia](https://img.shields.io/badge/licencia-MIT-green)

[English](../../README.md) · **Español** · [Français](README.fr.md) · [Português](README.pt-BR.md) · [Deutsch](README.de.md) · [Italiano](README.it.md) · [Русский](README.ru.md) · [简体中文](README.zh-CN.md) · [日本語](README.ja.md) · [한국어](README.ko.md)

<br>

<img src="../banner.svg" width="100%" alt="Ilustración de los widgets de OpenWinBox en un escritorio: un widget de carpetas con pestañas, un reloj, el tiempo, música y controles de audio" />

<sub>Ilustración de los tipos de widget. El aspecto real depende de tu tema y tu configuración.</sub>

</div>

> [!NOTE]
> OpenWinBox está en **beta**. Algunas cosas pueden cambiar o fallar entre versiones. Reporta los problemas en la pestaña [Issues](https://github.com/yosefhdev/Open-WinBox/issues).

## ¿Qué es OpenWinBox?

El escritorio de Windows suele llenarse de archivos sueltos, y para ver la hora de otro país o cambiar de canción hay que abrir otra app. OpenWinBox pone pequeños widgets en el escritorio para que todo eso esté siempre a la vista.

Funciona desde la bandeja del sistema, no necesita cuenta y quitar un widget nunca borra tus archivos.

## Widgets

### Widgets de carpetas

Convierte cualquier carpeta en un widget que muestra sus archivos y accesos directos.

- **Arrastrar y soltar:** arrastra archivos o accesos directos a un widget para añadirlos, y haz doble clic para abrirlos.
- **Pestañas como en el navegador:** agrupa varios widgets en uno, reordena las pestañas arrastrándolas o saca una para separarla de nuevo.
- **A tu estilo:** cambia el nombre de los widgets, usa un emoji o una imagen como icono, bloquea su posición y ordena los iconos como quieras.

### Widgets funcionales

Activa o desactiva cada uno por separado, y haz clic en su fila de Configuración para ver sus opciones.

| Widget | Qué hace |
|---|---|
| 🎵 **Música** | Muestra lo que suena en cualquier app, con controles de reproducción y tres estilos |
| 🟢 **Spotify** | La misma idea solo para Spotify, con aleatorio y repetición |
| 🕒 **Glance** | Reloj grande sobre la foto del día de Bing, con segunda zona horaria opcional y formato de 12 o 24 horas |
| ☀️ **Tiempo** | El tiempo actual de tu ubicación o de cualquier ciudad |
| 🔊 **Audio** | Cambia la salida y el micrófono, y ajusta el volumen de cada app, como en Win+G |

## Por qué te puede gustar

- **Se queda en el escritorio.** Los widgets están detrás de tus apps, siguen visibles con Win+D y nunca aparecen en la barra de tareas ni en Alt+Tab.
- **Se acomoda solo.** Los widgets se alinean suavemente entre sí y con los bordes de la pantalla.
- **Tema claro y oscuro.** Sigue el tema del sistema o elige uno.
- **Tranquilo en reposo.** Usa unos 0,05 segundos de CPU por minuto.
- **Importa desde DeskBox.** Si tienes DeskBox 1.5 instalado, tus widgets, su orden y sus iconos se importan la primera vez que abres la app.

## Instalación

**Necesitas** Windows 10 versión 1809 o posterior, o Windows 11 (64 bits). El instalador incluye todo lo demás, así que no hace falta instalar .NET.

1. Descarga el último `OpenWinBox-<versión>-Setup.exe` desde la página [Releases](https://github.com/yosefhdev/Open-WinBox/releases).
2. Ejecútalo. Se instala solo para tu usuario y no pide permisos de administrador.
3. Abre OpenWinBox. La primera vez se abre una guía rápida.

> [!IMPORTANT]
> El instalador todavía no está firmado, así que Windows puede mostrar *Windows protegió su PC*. Elige **Más información** y luego **Ejecutar de todas formas**. Para comprobar la descarga, compara su hash con `SHA256SUMS.txt` en la versión publicada. Si el Control inteligente de aplicaciones está activado, Windows puede bloquear la app sin opción de continuar.

## Cómo se usa

> [!TIP]
> La interfaz está disponible en **español** e **inglés**. Sigue el idioma de Windows y puedes cambiarlo en *Configuración → General → Idioma*. Los textos que verás en pantalla aparecen en *cursiva*.

Los dos controles que debes recordar son **F7**, que muestra u oculta todos los widgets, y el **ícono de la bandeja**, que abre el menú con clic derecho.

<details open>
<summary><b>Acciones del día a día</b></summary>
<br>

| Acción | Cómo |
|---|---|
| Mostrar u ocultar todos los widgets | Pulsa **F7** o haz clic izquierdo en el ícono de la bandeja |
| Abrir Configuración | Clic derecho en el ícono de la bandeja → *Configuración…*, o vuelve a abrir la app |
| Nuevo widget | **+** en cualquier widget → *Nuevo widget*, o *Configuración → Widgets de archivos → Nuevo widget* |
| Nueva pestaña en un widget | **+** → *Nueva pestaña en este widget* |
| Añadir archivos | Arrastra archivos o accesos directos al widget (mantén **Ctrl** para copiar un acceso directo en lugar de moverlo) |

</details>

<details>
<summary><b>Organizar widgets</b></summary>
<br>

| Acción | Cómo |
|---|---|
| Renombrar, cambiar icono, bloquear, agrupar | **⋯** en el widget → *Cambiar nombre*, *Cambiar icono…*, *Bloquear posición*, *Agrupar con…* |
| Agrupar dos widgets | Arrastra un widget por su barra y mantenlo sobre la barra de otro hasta que aparezca *Suelta para unir*; luego suelta |
| Reordenar o separar pestañas | Arrastra una pestaña hacia los lados para reordenarla, o fuera del widget para independizarla |
| Mover un widget funcional | Pasa el cursor por encima y arrastra el tirador de la parte superior |

</details>

<details>
<summary><b>Configuración</b></summary>
<br>

| Acción | Cómo |
|---|---|
| Activar o desactivar widgets funcionales | *Configuración → Widgets funcionales* |
| Cambiar el tema | *Configuración → Apariencia → Tema* |
| Iniciar con Windows | *Configuración → General → Lanzar al inicio*, o *Iniciar con Windows* en el menú de la bandeja |

</details>

## Actualizaciones

Abre *Configuración → Acerca de* y pulsa *Buscar actualizaciones*. Si hay una versión nueva, *Actualizar* la descarga, comprueba su hash y la instala, conservando tu configuración. La app se cierra un momento y vuelve a abrirse.

En *Canal de actualizaciones*, elige *Oficiales* para recibir solo versiones estables, o *Betas* para recibir también versiones de prueba. OpenWinBox solo se conecta a GitHub para esto cuando pulsas el botón.

## Privacidad

OpenWinBox no tiene cuentas ni analíticas propias. Tu configuración se queda en tu PC, en `%LOCALAPPDATA%\OpenWinBox`.

Dos widgets se conectan directamente desde tu PC a servicios en línea, sin clave de API:

| Widget | Servicio | Para qué |
|---|---|---|
| Tiempo | [Open-Meteo](https://open-meteo.com/) | Pronóstico y búsqueda de ciudades |
| Tiempo | [BigDataCloud](https://www.bigdatacloud.com/) | Nombre de la ciudad de tu ubicación |
| Tiempo | [ipapi.co](https://ipapi.co/) | Ubicación aproximada cuando la ubicación de Windows está desactivada |
| Glance | Microsoft Bing | Foto del día |

Los widgets Música y Spotify leen lo que Windows informa que se está reproduciendo, así que no te piden iniciar sesión. Las condiciones de cada servicio están en [THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md).

## Desinstalar

1. Elige *Salir* en el menú de la bandeja.
2. Desinstala OpenWinBox en **Configuración de Windows → Aplicaciones → Aplicaciones instaladas**. Esto también quita su inicio con Windows.
3. Opcional: borra `%LOCALAPPDATA%\OpenWinBox` para eliminar también tu configuración.

## Compilar desde el código

Necesitas el [SDK de .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
git clone https://github.com/yosefhdev/Open-WinBox.git
cd Open-WinBox
dotnet build src/OpenWinBox/OpenWinBox.csproj -c Release
```

La app se crea en `src/OpenWinBox/bin/Release/net8.0-windows10.0.19041.0/win-x64/OpenWinBox.exe`.

Para generar el instalador, instala [Inno Setup 6](https://jrsoftware.org/isinfo.php) y ejecuta `tools/publish.ps1`. El resultado queda en `dist/`.

Hecho con C#, [WinUI 3](https://github.com/microsoft/microsoft-ui-xaml) y el [Windows App SDK](https://github.com/microsoft/WindowsAppSDK).

## Versiones

OpenWinBox usa [versionado semántico](https://semver.org/lang/es/). Las betas se numeran `1.0.0-beta.N` hasta la versión final `1.0.0`. Consulta el [CHANGELOG.md](../../CHANGELOG.md).

## Contribuir

Los reportes de errores, ideas y pull requests son bienvenidos. Para cambios grandes, abre primero un issue. Para empezar con el código, consulta la [guía para desarrolladores](../development.md) (en inglés).

## Licencia

[MIT](../../LICENSE) © 2026 Yosefh. Los componentes y servicios de terceros están en [THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md). Datos del clima de [Open-Meteo.com](https://open-meteo.com/) (CC BY 4.0).

OpenWinBox es un proyecto independiente y no está afiliado a Microsoft, Spotify ni DeskBox.
