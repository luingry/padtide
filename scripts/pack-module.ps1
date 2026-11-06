<#
    Builds the SwissTechKnife module package:
      artifacts/padtide-<ver>.zip
        module.json, PadTide.Module.dll   (loaded by the host)
        app/PadTide.Hosted.exe            (PadTide, run in its own process with --hosted)
    <ver> is VERSION, stamped into the packaged module.json. The SDK dll is never packaged: the host provides it.
#>
param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

$root = Split-Path $PSScriptRoot -Parent
$version = (Get-Content -LiteralPath (Join-Path $root 'VERSION') -Raw).Trim()
$manifest = Get-Content -LiteralPath (Join-Path $root 'src/PadTide.Module/module.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($manifest.version -ne $version) { throw "module.json says $($manifest.version) but VERSION says $version." }
if (-not ($manifest.releaseNotes | Where-Object { $_.version -eq $version })) { throw "module.json has no releaseNotes for $version." }

$staging = Join-Path $root 'artifacts/module-staging'
if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
$moduleOut = Join-Path $staging 'module'
$appOut = Join-Path $staging 'app'

dotnet build (Join-Path $root 'src/PadTide.Module/PadTide.Module.csproj') -c $Configuration -o $moduleOut -v q --nologo | Out-Host
if ($LASTEXITCODE) { throw 'Module build failed.' }
dotnet build (Join-Path $root 'src/PadTide/PadTide.csproj') -c $Configuration -o $appOut -v q --nologo | Out-Host
if ($LASTEXITCODE) { throw 'App build failed.' }

$zipPath = Join-Path $root "artifacts/$($manifest.id)-$version.zip"
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath }

$skip = '.pdb', '.xml'
$zip = [IO.Compression.ZipFile]::Open($zipPath, 'Create')
try {
    $entry = $zip.CreateEntry('module.json', 'Optimal')
    $writer = New-Object IO.StreamWriter($entry.Open(), (New-Object Text.UTF8Encoding($false)))
    try { $writer.Write(($manifest | ConvertTo-Json -Depth 6)) } finally { $writer.Dispose() }

    Get-ChildItem -LiteralPath $moduleOut -File |
        Where-Object { $_.Name -ne 'module.json' -and $_.Name -notlike 'SwissTechKnife.Sdk.*' -and $_.Extension -notin $skip } |
        ForEach-Object { [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $_.Name, 'Optimal') | Out-Null }

    # The hosted copy gets its own image name, so it is told apart from a standalone PadTide.exe.
    Get-ChildItem -LiteralPath $appOut -File |
        Where-Object { $_.Name -like 'PadTide.exe*' -and $_.Extension -notin $skip } |
        ForEach-Object { [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, 'app/' + ($_.Name -replace '^PadTide\.exe', 'PadTide.Hosted.exe'), 'Optimal') | Out-Null }
} finally { $zip.Dispose() }

Remove-Item -LiteralPath $staging -Recurse -Force
[pscustomobject]@{
    Id = $manifest.id
    Version = $version
    File = $zipPath
    Sha256 = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Size = (Get-Item -LiteralPath $zipPath).Length
}
