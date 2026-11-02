<div align="center">

<img src="../icon.png" width="96" alt="Icono de OpenWinBox" />

# OpenWinBox

**Widgets de código abierto para el escritorio de Windows: tus carpetas, música, reloj, clima y audio, a la vista.**

![Versión](https://img.shields.io/badge/versión-1.0.0--beta.2-orange)
![Plataforma](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4)
![Licencia](https://img.shields.io/badge/licencia-MIT-green)

[English](../../README.md) · **Español** · [Français](README.fr.md) · [Português](README.pt-BR.md) · [Deutsch](README.de.md) · [Italiano](README.it.md) · [Русский](README.ru.md) · [简体中文](README.zh-CN.md) · [日本語](README.ja.md) · [한국어](README.ko.md)

</div>

> [!NOTE]
> OpenWinBox está en **beta**. Algunas cosas pueden cambiar o fallar entre versiones. Reporta los problemas en la pestaña Issues.

## Funciones

- **Widgets de carpetas.** Cualquier carpeta se vuelve un widget con sus archivos y accesos directos. Arrastra archivos o accesos directos encima para agregarlos; doble clic para abrir.
- **Pestañas como en el navegador.** Agrupa varios widgets en uno: arrastra un widget sobre otro, reordena las pestañas arrastrándolas o saca una pestaña para separarla.
- **A tu gusto.** Cambia el nombre, elige un emoji o una imagen como icono, bloquea la posición y acomoda los iconos en el orden que quieras.
- **Siempre en el escritorio.** Los widgets quedan detrás de tus apps, no aparecen en la barra de tareas ni en Alt+Tab y se alinean suavemente entre sí y con los bordes de la pantalla.
- **Widgets funcionales:**
  - **Música**: lo que suena en cualquier app, con controles y tres estilos.
  - **Spotify**: solo Spotify, con aleatorio y repetir.
  - **Glance**: reloj grande sobre la foto del día de Bing, segundo reloj opcional, 12 h o 24 h.
  - **Tiempo**: el clima de tu ubicación o de cualquier ciudad.
  - **Audio**: cambia la salida y el micrófono, y ajusta el volumen de cada app (como Win+G).
- **Ligero.** Alrededor de 0.005 % de CPU en reposo.
- **Importa desde DeskBox.** Si tienes DeskBox 1.5 instalado, la primera vez se importan tus widgets, su orden y sus iconos.

## Requisitos

- Windows 10 versión 1809 o posterior, o Windows 11 (64 bits).
- El instalador incluye todo lo demás. Para compilar desde el código necesitas el [SDK de .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0).

## Instalación

### Opción 1: descargar una versión

1. Abre la página **Releases** de este repositorio y descarga el último `OpenWinBox-<versión>-Setup.exe`.
2. Ejecútalo. Se instala solo para tu usuario y no pide permisos de administrador.
3. Abre OpenWinBox. La primera vez se abre una guía rápida.

> El instalador todavía no está firmado, así que Windows puede mostrar *Windows protegió su PC*. Elige **Más información** y luego **Ejecutar de todas formas**. Puedes comparar su hash con `SHA256SUMS.txt` en la versión publicada.

### Opción 2: compilar desde el código

```bash
git clone <url-del-repositorio>
cd <carpeta-del-repositorio>
dotnet build DeskBox/OpenWinBox.csproj -c Release
```

La app queda en `DeskBox/bin/Release/net8.0-windows10.0.19041.0/win-x64/OpenWinBox.exe`.

## Cómo se usa

| Acción | Cómo |
|---|---|
| Mostrar u ocultar todos los widgets | Pulsa **F7** o haz clic izquierdo en el icono de la bandeja |
| Abrir la configuración | Clic derecho en el icono de la bandeja → *Configuración…*, o vuelve a abrir la app |
| Nuevo widget | **+** en cualquier widget → *Nuevo widget*, o *Configuración → Widgets de archivos → Nuevo widget* |
| Nueva pestaña en un widget | **+** → *Nueva pestaña en este widget* |
| Renombrar, cambiar icono, bloquear, agrupar | **⋯** en el widget → *Cambiar nombre*, *Cambiar icono…*, *Bloquear posición*, *Agrupar con…* |
| Agregar archivos | Arrastra archivos o accesos directos al widget (mantén **Ctrl** para copiar un acceso directo en vez de moverlo) |
| Agrupar dos widgets | Arrastra un widget por su barra y mantenlo sobre la barra de otro hasta que aparezca *Suelta para unir*; luego suelta |
| Reordenar o separar pestañas | Arrastra una pestaña a los lados para reordenarla, o fuera del widget para independizarla |
| Mover un widget funcional | Pasa el mouse encima y arrastra la agarradera de arriba |
| Activar o desactivar widgets funcionales | *Configuración → Widgets funcionales* (clic en una fila para ver sus opciones) |
| Iniciar con Windows | *Configuración → General → Lanzar al inicio*, o *Iniciar con Windows* en el menú de la bandeja |

La configuración se guarda en `%LOCALAPPDATA%\OpenWinBox`. Quitar un widget o una pestaña nunca borra tus archivos.

## Desinstalar

1. Apaga *Lanzar al inicio* en la configuración y elige *Salir* en el menú de la bandeja.
2. Borra la carpeta de la app.
3. Opcional: borra `%LOCALAPPDATA%\OpenWinBox` para eliminar tu configuración.

## Versiones

OpenWinBox usa [versionado semántico](https://semver.org/lang/es/). Las betas se numeran `1.0.0-beta.N` hasta la versión final `1.0.0`. Consulta el [CHANGELOG.md](../../CHANGELOG.md).

## Contribuir

Los reportes de errores, ideas y pull requests son bienvenidos. Para cambios grandes, abre primero un issue.

## Licencia

[MIT](../../LICENSE) © 2026 Yosefh. Los componentes y servicios de terceros están en [THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md). Datos del clima de [Open-Meteo.com](https://open-meteo.com/) (CC BY 4.0).

OpenWinBox es un proyecto independiente y no está afiliado a Microsoft, Spotify ni DeskBox.
