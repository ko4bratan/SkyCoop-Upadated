[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$GameDirectory,
    [switch]$IsolatedCopy,
    [switch]$VerifiedSteamInstall,
    [switch]$OverwriteConfirmed
)
$ErrorActionPreference = 'Stop'
if ($VerifiedSteamInstall) {
    . (Join-Path $PSScriptRoot 'Launcher-Helpers.ps1')
    $null=Assert-SkyCoopSteamVersion $GameDirectory
    $null=Get-SkyCoopSteamManifest $GameDirectory
} elseif (-not $IsolatedCopy) { throw 'Нужно подтверждение тестовой копии или проверенная Steam-установка.' }
$game = (Resolve-Path -LiteralPath $GameDirectory).Path
if (-not ((Test-Path -LiteralPath (Join-Path $game 'tld.exe')) -or (Test-Path -LiteralPath (Join-Path $game 'TheLongDark.exe')))) {
    throw 'No tld.exe or TheLongDark.exe found. Select an isolated Windows game copy.'
}
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'manifest.json') -Raw | ConvertFrom-Json
$allowed = @('Mods/SkyCoop.dll', 'Mods/SkyCoop.pdb', 'UserLibs/Steamworks.NET.dll')
foreach ($relative in $allowed) {
    $source = Join-Path $PSScriptRoot $relative
    $expected = $manifest.files.PSObject.Properties[$relative].Value
    if (-not $expected -or (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected) {
        throw "Package file hash mismatch: $relative"
    }
}
if ($VerifiedSteamInstall -and -not $OverwriteConfirmed) {
    foreach ($relative in $allowed) { if (Test-Path -LiteralPath (Join-Path $game $relative)) { throw 'Нужно явное подтверждение замены старого мода через лаунчер.' } }
}
$backup = Join-Path $game ('SkyCoop-Backups/' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-ffff'))
foreach ($relative in $allowed) {
    $source = Join-Path $PSScriptRoot $relative
    $destination = Join-Path $game $relative
    if (Test-Path -LiteralPath $destination) {
        $saved = Join-Path $backup $relative
        New-Item -ItemType Directory -Path (Split-Path $saved) -Force | Out-Null
        Copy-Item -LiteralPath $destination -Destination $saved
    }
    New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination -Force
}
Write-Host 'Experimental mod binaries installed. Loader/runtime remain external. Minimal probe needs no old bundles; see REBORN-CHARACTER.md for optional local character export.'
Write-Host 'This is not a playable-compatibility result. Follow INSTALL.md and TWO-PLAYER-TEST.md.'
