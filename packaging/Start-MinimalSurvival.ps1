[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$GameDirectory,
    [Parameter(Mandatory=$true)][ValidateSet('Host','Guest')][string]$Role,
    [string]$HostAddress = '127.0.0.1',
    [ValidateRange(1024,65535)][int]$Port = 26950,
    [switch]$IsolatedCopy,
    [switch]$DisposableProfile,
    [switch]$MarkerOnly,
    [switch]$PassThru,
    [switch]$VerifiedSteamInstall,
    [switch]$TestSave
)
$ErrorActionPreference = 'Stop'
if ($VerifiedSteamInstall) {
    if (-not $TestSave) { throw 'Подтвердите использование тестового сохранения.' }
    . (Join-Path $PSScriptRoot 'Launcher-Helpers.ps1')
    $null=Assert-SkyCoopSteamVersion $GameDirectory
    $null=Get-SkyCoopSteamManifest $GameDirectory
} elseif (-not $IsolatedCopy -or -not $DisposableProfile) {
    throw 'Нужны отдельная TLD 2.55 и тестовый профиль Windows/сохранений. Копия папки игры не изолирует сохранения. Подтвердите оба условия.'
}
if ($Port -eq 26951) { throw 'Порт 26951 зарезервирован для гостя.' }
$game = (Resolve-Path -LiteralPath $GameDirectory).Path
$exe = Join-Path $game 'tld.exe'
if (-not (Test-Path -LiteralPath $exe) -or -not (Test-Path -LiteralPath (Join-Path $game 'Mods/SkyCoop.dll'))) { throw 'Не найдены tld.exe или установленная экспериментальная SkyCoop.dll.' }
$arguments = @('-skycoop-minimal','-force-d3d11',"-skycoop-test-port=$Port")
if ($MarkerOnly) { $arguments += '-skycoop-marker-only' }
if ($Role -eq 'Host') { $arguments += '-skycoop-test-host' }
else {
    $ip = $null
    if (-not [Net.IPAddress]::TryParse($HostAddress, [ref]$ip) -or $ip.AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork) { throw 'Укажите IPv4 хоста из Radmin VPN.' }
    $arguments += "-skycoop-test-join=$HostAddress"
}
Write-Host 'Проверьте TLD 2.55 в меню. Загрузите копии одного тестового мира Выживания на обоих ПК.'
Write-Host 'Без ресурсов персонажа используется маркер. Рабочую TLD 2.01–2.02 оставьте отдельно.'
Start-Process -FilePath $exe -WorkingDirectory $game -ArgumentList $arguments -PassThru:$PassThru
