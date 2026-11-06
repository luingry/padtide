$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$version = (Get-Content -LiteralPath (Join-Path $root 'VERSION') -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "VERSION must be MAJOR.MINOR.PATCH; found '$version'." }

& (Join-Path $PSScriptRoot 'build.ps1')

function Find-Iscc {
    $command = Get-Command iscc.exe -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($command) { return $command.Source }
    foreach ($candidate in @((Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'), (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'))) {
        if (Test-Path -LiteralPath $candidate) { return $candidate }
    }
    return $null
}

$iscc = Find-Iscc
if (-not $iscc) {
    if (-not $env:CI) { throw 'Inno Setup 6 is required to build the installer.' }
    choco install innosetup --no-progress -y
    if ($LASTEXITCODE) { throw 'Inno Setup installation failed.' }
    $iscc = Find-Iscc
    if (-not $iscc) { throw 'ISCC.exe was not found after installing Inno Setup.' }
}

& $iscc "/DAppVersion=$version" (Join-Path $root 'installer\PadTide.iss')
if ($LASTEXITCODE) { throw 'Installer compilation failed.' }
Get-Item (Join-Path $root "artifacts\PadTide-Setup-$version.exe") | Select-Object Name, Length
