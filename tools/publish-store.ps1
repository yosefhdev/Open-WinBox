# Genera dist/store/.../OpenWinBox_<versión>_x64.msix para subirlo a Partner Center. La Store lo firma.
$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot
$project = Join-Path $root 'src\OpenWinBox\OpenWinBox.csproj'
$manifest = Join-Path $root 'src\OpenWinBox\Package.appxmanifest'

# El paquete solo admite cuatro números con el último en 0, y cada versión debe ser mayor que la anterior:
# 1.0.0-beta.N → 1.0.N.0; final X.Y.Z → X.(Y+1).Z.0, para que la 1.0.0 quede por encima de todas las betas
function Get-PackageVersion([string]$version) {
    if ($version -match '^1\.0\.0-beta\.(\d+)$') { return "1.0.$($Matches[1]).0" }
    if ($version -match '^(\d+)\.(\d+)\.(\d+)$') { return "$($Matches[1]).$([int]$Matches[2] + 1).$($Matches[3]).0" }
    # shortcut: solo betas de la 1.0.0; ampliar si hay betas de otra versión
    throw "Sin correspondencia para la versión $version"
}

$version = ([xml](Get-Content -Raw $project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$expected = Get-PackageVersion $version
$identity = ([xml](Get-Content -Raw $manifest)).Package.Identity
if ($identity.Version -ne $expected) { throw "Pon Version=`"$expected`" en Package.appxmanifest (la app es $version)" }

$out = Join-Path $root 'dist\store'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
# El paquete y el instalador comparten obj/: sin --no-incremental se mezclarían sus archivos generados
dotnet build $project -c Release --no-incremental -p:StoreBuild=true -p:DebugType=none
if ($LASTEXITCODE) { throw "dotnet build falló ($LASTEXITCODE)" }
Get-ChildItem $out -Recurse -Filter "OpenWinBox_*.msix" | ForEach-Object { Write-Host "$($_.FullName) ($([math]::Round($_.Length / 1MB)) MB)" }
