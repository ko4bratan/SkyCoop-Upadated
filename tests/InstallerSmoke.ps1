# File-copy/backup/hash-rejection checks only. No game process is launched.
param([Parameter(Mandatory=$true)][string]$PackageDirectory)
$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ('SkyCoopInstallTest-' + [guid]::NewGuid())
try {
    $game = Join-Path $root 'FakeGameCopy'
    New-Item -ItemType Directory -Path (Join-Path $game 'Mods') -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $game 'tld.exe') -Value 'Installer fixture, not a game executable'
    Set-Content -LiteralPath (Join-Path $game 'Mods/SkyCoop.dll') -Value 'original-fixture'
    $old = (Get-FileHash -LiteralPath (Join-Path $game 'Mods/SkyCoop.dll')).Hash
    $installer = Join-Path $PackageDirectory 'Install-Experimental.ps1'
    & $installer -GameDirectory $game -IsolatedCopy
    $backups = @(Get-ChildItem -LiteralPath (Join-Path $game 'SkyCoop-Backups') -Recurse -File -Filter SkyCoop.dll)
    if ($backups.Count -ne 1 -or (Get-FileHash -LiteralPath $backups[0].FullName).Hash -ne $old) { throw 'Backup did not preserve original fixture.' }
    if ((Get-FileHash -LiteralPath (Join-Path $game 'Mods/SkyCoop.dll')).Hash -ne (Get-FileHash -LiteralPath (Join-Path $PackageDirectory 'Mods/SkyCoop.dll')).Hash) { throw 'Installed DLL differs.' }
    $blocked = $false
    try { & $installer -GameDirectory $game } catch { $blocked = $true }
    if (-not $blocked) { throw 'Installer accepted missing isolated-copy acknowledgement.' }
    Add-Content -LiteralPath (Join-Path $PackageDirectory 'UserLibs/Steamworks.NET.dll') -Value 'tamper-fixture'
    $blocked = $false
    try { & $installer -GameDirectory $game -IsolatedCopy } catch { $blocked = $true }
    if (-not $blocked) { throw 'Installer accepted a tampered dependency.' }
    Write-Host 'PASS: installer copies exact binaries, preserves backup, and rejects tampering/missing acknowledgement. No game QA performed.'
} finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
