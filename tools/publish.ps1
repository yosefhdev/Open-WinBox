# Genera dist/OpenWinBox-<versión>-Setup.exe y dist/SHA256SUMS.txt. Requiere Inno Setup 6:
#   winget install JRSoftware.InnoSetup
# Sin -Stage hace todo sin firmar. GitHub Actions lo llama por etapas para firmar entre una y otra:
#   App (compila y crea el desinstalador) → firma → Installer → firma → Sums
param([ValidateSet('All', 'App', 'Installer', 'Sums')][string]$Stage = 'All')
$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot
$project = Join-Path $root 'src\OpenWinBox\OpenWinBox.csproj'
$dist = Join-Path $root 'dist'
$version = ([xml](Get-Content -Raw $project)).Project.PropertyGroup.Version

function Compile-Installer {
    $iscc = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" |
        Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $iscc) { throw 'No se encontró Inno Setup 6 (winget install JRSoftware.InnoSetup)' }
    $uninstaller = Join-Path $dist 'uninstaller\uninst-*.e32'
    $signed = $Stage -eq 'App' -or ($Stage -eq 'Installer' -and (Get-AuthenticodeSignature (Get-Item $uninstaller).FullName).Status -eq 'Valid')
    & $iscc /Q "/DAppVersion=$version" "/DFileVersion=$($version -replace '-.*')" $(if ($signed) { '/DSignedBuild' }) (Join-Path $PSScriptRoot 'installer.iss')
    # En la etapa App es normal que falle: Inno Setup crea el desinstalador y se detiene hasta que esté firmado
    if ($LASTEXITCODE -and -not ($Stage -eq 'App' -and (Test-Path $uninstaller))) { throw "ISCC falló ($LASTEXITCODE)" }
    $global:LASTEXITCODE = 0
}

if ($Stage -in 'All', 'App') {
    if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
    # Con el runtime incluido para que no haga falta instalar .NET; sin .pdb para no llevar rutas del equipo
    dotnet publish $project -c Release -r win-x64 --self-contained -p:DebugType=none -o (Join-Path $dist 'app')
    if ($LASTEXITCODE) { throw "dotnet publish falló ($LASTEXITCODE)" }
}

# En la etapa App, esta compilación solo sirve para que Inno Setup cree el desinstalador que se va a firmar
if ($Stage -ne 'Sums') { Compile-Installer }

if ($Stage -in 'All', 'Sums') {
    $setup = Get-Item (Join-Path $dist "OpenWinBox-$version-Setup.exe")
    $hash = (Get-FileHash $setup.FullName -Algorithm SHA256).Hash.ToLower()
    # Con LF: `sha256sum -c` no acepta CRLF
    [IO.File]::WriteAllText((Join-Path $dist 'SHA256SUMS.txt'), "$hash  $($setup.Name)`n")
    Write-Host "$($setup.FullName) ($([math]::Round($setup.Length / 1MB)) MB)"
}
