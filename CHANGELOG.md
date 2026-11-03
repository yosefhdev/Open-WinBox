# Changelog

Versiones con [SemVer](https://semver.org/lang/es/). Mientras la app está en pruebas se publican betas
(`1.0.0-beta.1`, `1.0.0-beta.2`, …). Cuando todo esté probado sale la versión final `1.0.0`.

- **beta.N** sube con cada entrega de pruebas (funciones nuevas o arreglos).
- Si hace falta, antes de la final puede haber candidatas `1.0.0-rc.N` solo con arreglos.
- Cada versión lleva su etiqueta de Git `vX.Y.Z[-beta.N]`. La versión vive en `src/OpenWinBox/OpenWinBox.csproj` (`<Version>`).

## [Sin publicar]

## [1.0.0-beta.1] - 2026-11-03

Primera versión pública.

- Widgets de carpetas con pestañas tipo navegador: crear, renombrar, cambiar icono, bloquear, agrupar
  (menú o arrastrando un widget sobre otro), reordenar y separar pestañas.
- Arrastrar archivos y accesos directos al widget; abrir cualquier tipo de archivo.
- Imán al mover y redimensionar; widgets siempre detrás de las apps, fuera de la barra de tareas y Alt+Tab, y visibles con Win+D.
- Widgets funcionales: Música, Spotify, Glance (reloj dual 12/24 h), Tiempo y Audio (dispositivos y volumen por app).
- Configuración con subpáginas por widget, asistente de primeros pasos, bandeja propia y F7.
- Buscar e instalar actualizaciones desde Configuración → Acerca de, comprobando el hash del instalador.
- Canal de actualizaciones: Oficiales (solo versiones estables) o Betas.
- Instalador para Windows (`OpenWinBox-<versión>-Setup.exe`): no requiere administrador ni instalar .NET.
- Importa widgets, orden e íconos de DeskBox 1.5.
- README en inglés, español, francés, portugués, alemán, italiano, ruso, chino, japonés y coreano.
