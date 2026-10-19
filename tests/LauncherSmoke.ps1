param([Parameter(Mandatory=$true)][string]$PackageDirectory)
$ErrorActionPreference='Stop'
. (Join-Path $PackageDirectory 'Launcher-Helpers.ps1')
& (Join-Path $PackageDirectory 'SkyCoop-Launcher.ps1') -SmokeTest
$root=Join-Path ([IO.Path]::GetTempPath()) ('SkyCoopLauncherTest-'+[guid]::NewGuid())
New-Item -ItemType Directory -Path $root | Out-Null
try {
    foreach ($bad in @('abc','26.1.1','26.1.1.1;calc','999.1.1.1','0.0.0.0','::1','26.1.1.1:26950')) {
        $blocked=$false; try { Assert-SkyCoopIPv4 $bad } catch { $blocked=$true }
        if (-not $blocked) { throw "Неверный адрес принят: $bad" }
    }
    Assert-SkyCoopIPv4 '26.10.20.30'
    $game=Join-Path $root 'Game'
    New-Item -ItemType Directory -Path (Join-Path $game 'Mods') -Force | Out-Null
    Set-Content (Join-Path $game 'tld.exe') 'Тестовый файл, не игра'
    Set-Content (Join-Path $game 'Mods/SkyCoop.dll') 'старый файл'
    $before=(Get-FileHash (Join-Path $game 'Mods/SkyCoop.dll')).Hash
    $blocked=$false
    try { Install-SkyCoopLauncherFiles $PackageDirectory $game { param($files) return $false } } catch { $blocked=$true }
    if (-not $blocked -or (Get-FileHash (Join-Path $game 'Mods/SkyCoop.dll')).Hash -ne $before) { throw 'Отмена замены изменила файл.' }
    $diagnostics=Join-Path $game 'UserData/SkyCoop/Diagnostics'
    New-Item -ItemType Directory -Path $diagnostics -Force | Out-Null
    $report=Join-Path $diagnostics 'minimal-state.json'
    [IO.File]::WriteAllText($report,'{"peerReady":true,"validatedPeerStates":10,"secondsSincePeerState":0,"localScene":"Lake","remoteScene":"Lake"}')
    if ((Get-SkyCoopConnectionStatus $game ([datetime]::UtcNow.AddMinutes(1))) -notmatch 'Старый отчёт') { throw 'Использован отчёт до запуска.' }
    if ((Get-SkyCoopConnectionStatus $game ([datetime]::UtcNow.AddMinutes(-1))) -notmatch '10 пакетов') { throw 'Не показаны реальные координаты.' }
    (Get-Item $report).LastWriteTimeUtc=[datetime]::UtcNow.AddMinutes(-1)
    if ((Get-SkyCoopConnectionStatus $game ([datetime]::UtcNow.AddMinutes(-2))) -notmatch '15 секунд') { throw 'Устаревшая связь показана как активная.' }
    Set-Content (Join-Path $game 'secret-save.sav') 'Не включать'
    $zip=Join-Path $root 'logs.zip'
    Export-SkyCoopDiagnostics $game $zip
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive=[IO.Compression.ZipFile]::OpenRead($zip)
    try {
        if (@($archive.Entries | Where-Object { $_.FullName -like '*save*' }).Count -gt 0) { throw 'В архив попало сохранение.' }
        if (@($archive.Entries | Where-Object { $_.Name -eq 'minimal-state.json' }).Count -ne 1) { throw 'Нет диагностики в архиве.' }
    } finally { $archive.Dispose() }
    $hash=(Get-FileHash $zip).Hash; $blocked=$false
    try { Export-SkyCoopDiagnostics $game $zip } catch { $blocked=$true }
    if (-not $blocked -or (Get-FileHash $zip).Hash -ne $hash) { throw 'Существующий архив был заменён.' }
    Write-Host 'ПРОЙДЕНО: GUI, IPv4, отказ от замены, свежесть статуса и архив диагностики. Игра не запускалась.'
} finally { Remove-Item -LiteralPath $root -Recurse -Force }
