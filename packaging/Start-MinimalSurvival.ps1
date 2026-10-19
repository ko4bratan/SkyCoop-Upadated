[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$GameDirectory,
    [Parameter(Mandatory=$true)][ValidateSet('Host','Guest')][string]$Role,
    [string]$HostAddress = '127.0.0.1',
    [ValidateRange(1024,65535)][int]$Port = 26950,
    [switch]$IsolatedCopy,
    [switch]$DisposableProfile,
    [switch]$MarkerOnly
)
$ErrorActionPreference = 'Stop'
if (-not $IsolatedCopy -or -not $DisposableProfile) {
    throw 'Use a separate TLD2.55 test copy AND a disposable Windows/game-save profile. A copied game directory alone does not isolate saves. Pass both acknowledgements.'
}
if ($Port -eq 26951) { throw 'Port26951 is reserved for the guest socket.' }
$game = (Resolve-Path -LiteralPath $GameDirectory).Path
$exe = Join-Path $game 'tld.exe'
if (-not (Test-Path -LiteralPath $exe) -or -not (Test-Path -LiteralPath (Join-Path $game 'Mods/SkyCoop.dll'))) { throw 'Test game executable or installed experimental DLL missing.' }
$arguments = @('-skycoop-minimal','-force-d3d11',"-skycoop-test-port=$Port")
if ($MarkerOnly) { $arguments += '-skycoop-marker-only' }
if ($Role -eq 'Host') { $arguments += '-skycoop-test-host' }
else {
    $ip = $null
    if (-not [Net.IPAddress]::TryParse($HostAddress, [ref]$ip) -or $ip.AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork) { throw 'Specify a literal IPv4 host address.' }
    $arguments += "-skycoop-test-join=$HostAddress"
}
Write-Host 'Verify TLD2.55 in its menu/log. Load disposable copies of ONE Survival world on both PCs. This probe does not share world/items/saves.'
Write-Host 'Character assets are optional; fallback marker retains the connection. Existing2.01/2.02 install must stay separate.'
Start-Process -FilePath $exe -WorkingDirectory $game -ArgumentList $arguments
