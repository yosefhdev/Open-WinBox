# Genera dist/OpenWinBox-<versión>-Setup.exe y dist/SHA256SUMS.txt. Requiere Inno Setup 6:
#   winget install JRSoftware.InnoSetup
$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot
$project = Join-Path $root 'src\OpenWinBox\OpenWinBox.csproj'
$dist = Join-Path $root 'dist'
$version = ([xml](Get-Content -Raw $project)).Project.PropertyGroup.Version

$iscc = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'No se encontró Inno Setup 6 (winget install JRSoftware.InnoSetup)' }

if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }

# Con el runtime incluido para que no haga falta instalar .NET; sin .pdb para no llevar rutas del equipo
dotnet publish $project -c Release -r win-x64 --self-contained -p:DebugType=none -o (Join-Path $dist 'app')
if ($LASTEXITCODE) { throw "dotnet publish falló ($LASTEXITCODE)" }

& $iscc /Q "/DAppVersion=$version" "/DFileVersion=$($version -replace '-.*')" (Join-Path $PSScriptRoot 'installer.iss')
if ($LASTEXITCODE) { throw "ISCC falló ($LASTEXITCODE)" }

$setup = Get-Item (Join-Path $dist "OpenWinBox-$version-Setup.exe")
$hash = (Get-FileHash $setup.FullName -Algorithm SHA256).Hash.ToLower()
# Con LF: `sha256sum -c` no acepta CRLF
[IO.File]::WriteAllText((Join-Path $dist 'SHA256SUMS.txt'), "$hash  $($setup.Name)`n")

Write-Host "$($setup.FullName) ($([math]::Round($setup.Length / 1MB)) MB)"
