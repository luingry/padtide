param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'src\PadTide\PadTide.csproj'
$icon = Join-Path $root 'assets\padtide.ico'
$exe = Join-Path $root 'src\PadTide\bin\Release\net48\PadTide.exe'

# The icon is drawn by the app itself, so the first build has none to embed.
function Build { dotnet build $project -c Release --nologo -v q; if ($LASTEXITCODE) { throw 'build failed' } }
Build
& $exe --export-icon $icon | Out-Null
Build
dotnet build (Join-Path $root 'src\PadTide.Module\PadTide.Module.csproj') -c Release --nologo -v q; if ($LASTEXITCODE) { throw 'module build failed' }

if (-not $SkipTests) {
    dotnet run --project (Join-Path $root 'tests\PadTide.Tests\PadTide.Tests.csproj') -c Release --nologo
    if ($LASTEXITCODE) { throw 'tests failed' }
}

$artifacts = Join-Path $root 'artifacts'
if (-not (Test-Path $artifacts)) { New-Item -ItemType Directory -Path $artifacts | Out-Null }
Copy-Item $exe (Join-Path $artifacts 'PadTide.exe') -Force
Get-Item (Join-Path $artifacts 'PadTide.exe') | Select-Object Name, Length
