# Вспомогательные функции без запуска GUI или игры.
function Get-SkyCoopGameFolders {
    $roots = New-Object 'System.Collections.Generic.List[string]'
    foreach ($key in @('HKCU:\Software\Valve\Steam','HKLM:\SOFTWARE\WOW6432Node\Valve\Steam')) {
        if (Test-Path $key) {
            $entry = Get-ItemProperty $key
            foreach ($name in @('SteamPath','InstallPath')) { if ($entry.$name) { $roots.Add([string]$entry.$name) } }
        }
    }
    foreach ($path in @("${env:ProgramFiles(x86)}\Steam", "$env:ProgramFiles\Steam")) { if (Test-Path -LiteralPath $path) { $roots.Add($path) } }
    foreach ($root in @($roots.ToArray())) {
        $vdf = Join-Path $root 'steamapps/libraryfolders.vdf'
        if (Test-Path -LiteralPath $vdf) {
            foreach ($match in [regex]::Matches([IO.File]::ReadAllText($vdf), '"path"\s+"([^"]+)"')) { $roots.Add($match.Groups[1].Value.Replace('\\','\')) }
        }
    }
    $folders = @()
    foreach ($root in $roots) {
        $path = Join-Path $root 'steamapps/common/TheLongDark'
        if (Test-Path -LiteralPath (Join-Path $path 'tld.exe')) { $folders += $path }
    }
    foreach ($key in @('HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*','HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*','HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*')) {
        foreach ($entry in @(Get-ItemProperty $key -ErrorAction SilentlyContinue)) {
            if ($entry.DisplayName -match 'The Long Dark' -and $entry.InstallLocation -and (Test-Path -LiteralPath (Join-Path $entry.InstallLocation 'tld.exe'))) { $folders += $entry.InstallLocation }
        }
    }
    return @($folders | Sort-Object -Unique)
}
function Assert-SkyCoopIPv4([string]$Address) {
    $ip = $null
    if ($Address -notmatch '^\d{1,3}(\.\d{1,3}){3}$' -or -not [Net.IPAddress]::TryParse($Address,[ref]$ip) -or $ip.AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork) {
        throw 'Введите IPv4 хоста из Radmin VPN: например, 26.10.20.30. Без пробелов и номера порта.'
    }
    if ($Address -eq '0.0.0.0' -or $Address -eq '255.255.255.255') { throw 'Этот адрес нельзя использовать для подключения к хосту.' }
}
function Assert-SkyCoopTarget([string]$GameDirectory) {
    $game = (Resolve-Path -LiteralPath $GameDirectory -ErrorAction Stop).Path
    $exe = Join-Path $game 'tld.exe'
    if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw 'В выбранной папке нет tld.exe. Выберите отдельную копию The Long Dark 2.55.' }
    $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe).ProductVersion
    if ($version -match '^2\.(01|02)(\D|$)') { throw 'Обнаружена старая версия 2.01–2.02. Установка и запуск запрещены: выберите отдельную TLD 2.55.' }
    return $game
}
function Install-SkyCoopLauncherFiles([string]$PackageDirectory,[string]$GameDirectory,[scriptblock]$ConfirmOverwrite,[switch]$SteamInstall) {
    if ($SteamInstall) { $null=Assert-SkyCoopSteamVersion $GameDirectory; $null=Get-SkyCoopSteamManifest $GameDirectory }
    $owned = @('Mods/SkyCoop.dll','Mods/SkyCoop.pdb','UserLibs/Steamworks.NET.dll')
    $existing = @($owned | Where-Object { Test-Path -LiteralPath (Join-Path $GameDirectory $_) })
    if ($existing.Count -gt 0 -and -not (& $ConfirmOverwrite $existing)) { throw 'Замена файлов отменена. Установка не изменена.' }
    & (Join-Path $PackageDirectory 'Install-Experimental.ps1') -GameDirectory $GameDirectory -IsolatedCopy:(-not $SteamInstall) -VerifiedSteamInstall:$SteamInstall -OverwriteConfirmed | Out-Null
}
function Get-SkyCoopConnectionStatus([string]$GameDirectory,[datetime]$Since) {
    $path = Join-Path $GameDirectory 'UserData/SkyCoop/Diagnostics/minimal-state.json'
    if (-not (Test-Path -LiteralPath $path)) { return 'Ожидание запуска мода. Загрузите тестовое Выживание на обоих ПК.' }
    $file = Get-Item -LiteralPath $path
    if ($file.LastWriteTimeUtc -lt $Since.ToUniversalTime()) { return 'Ожидание нового отчёта мода. Старый отчёт не используется.' }
    if (([datetime]::UtcNow - $file.LastWriteTimeUtc).TotalSeconds -gt 15) { return 'Нет свежего отчёта более 15 секунд. Игра закрыта, загружается или мод остановился; проверьте логи.' }
    try { $state = [IO.File]::ReadAllText($path) | ConvertFrom-Json } catch { return 'Ожидание записи отчёта мода…' }
    if (-not $state.peerReady) { return 'Мод запущен. Ожидание второго игрока: проверьте Radmin VPN, адрес и UDP 26950/26951.' }
    if ($state.secondsSincePeerState -gt 10) { return 'Связь с игроком прервана: более 10 секунд без пакетов.' }
    if ($state.validatedPeerStates -lt 1) { return 'Сеанс принят. Ожидание координат второго игрока.' }
    if ($state.localScene -ne $state.remoteScene) { return 'Пакеты получены, но игроки в разных сценах. Перейдите в один регион/интерьер.' }
    return "Получены координаты второго игрока: $($state.validatedPeerStates) пакетов. Это ещё не подтверждение работоспособности кооператива."
}
function Export-SkyCoopDiagnostics([string]$GameDirectory,[string]$Destination) {
    if (Test-Path -LiteralPath $Destination) { throw 'Архив уже существует. Выберите новое имя; существующий файл не заменён.' }
    $temp = Join-Path ([IO.Path]::GetTempPath()) ('SkyCoopLogs-' + [guid]::NewGuid())
    New-Item -ItemType Directory -Path $temp | Out-Null
    try {
        $sources = @()
        foreach ($relative in @('MelonLoader/Latest.log','MelonLoader/Latest.log.previous')) {
            $path = Join-Path $GameDirectory $relative
            if (Test-Path -LiteralPath $path -PathType Leaf) { $sources += @{Path=$path;Name=([IO.Path]::GetFileName($path))} }
        }
        $diagnostics = Join-Path $GameDirectory 'UserData/SkyCoop/Diagnostics'
        if (Test-Path -LiteralPath $diagnostics) {
            foreach ($file in Get-ChildItem -LiteralPath $diagnostics -File | Where-Object { $_.Name -match '\.(json|jsonl|previous)$' } | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 25) {
                $sources += @{Path=$file.FullName;Name=('Diagnostics/' + $file.Name)}
            }
        }
        $player = Join-Path $env:USERPROFILE 'AppData/LocalLow/Hinterland/TheLongDark/Player.log'
        if (Test-Path -LiteralPath $player -PathType Leaf) { $sources += @{Path=$player;Name='Player.log'} }
        foreach ($source in $sources) {
            $file = Get-Item -LiteralPath $source.Path
            if ($file.Length -gt 20MB) { continue }
            $target = Join-Path $temp $source.Name
            New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
            # Разрешить чтение файла, который ещё открыт игрой.
            $inputStream = [IO.File]::Open($file.FullName,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
            try { $output = [IO.File]::Create($target); try { $inputStream.CopyTo($output) } finally { $output.Dispose() } } finally { $inputStream.Dispose() }
        }
        [IO.File]::WriteAllText((Join-Path $temp 'README.txt'), 'Диагностика экспериментального SkyCoop 2.55. Сохранения и игровые ресурсы не включены. Логи могут содержать IP, имена и локальные пути. Проверьте содержимое перед отправкой. Player.log относится к текущему Windows-профилю и может быть от другого запуска.')
        Compress-Archive -Path (Join-Path $temp '*') -DestinationPath $Destination -ErrorAction Stop
    } finally { Remove-Item -LiteralPath $temp -Recurse -Force }
}

function Assert-SkyCoopSteamVersion([string]$GameDirectory) {
    $game=Assert-SkyCoopTarget $GameDirectory
    $exe=Join-Path $game 'tld.exe'
    $version=[Diagnostics.FileVersionInfo]::GetVersionInfo($exe).ProductVersion
    $verified=$version -match '^2\.55(?:\.\d+)*(?:\s.*)?$'
    $identity=Join-Path $game 'UserData/SkyCoop/Diagnostics/game-identity.json'
    if (-not $verified -and (Test-Path -LiteralPath $identity)) {
        $record=[IO.File]::ReadAllText($identity) | ConvertFrom-Json
        $verified=($record.gameVersion -match '^2\.55(?:\.\d+)*$') -and ($record.exeSha256 -eq (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant())
    }
    if (-not $verified) {
        throw 'Версия 2.55 не подтверждена. Файлы не изменены. Нужны метаданные версии 2.55 либо game-identity.json от этой же игры с совпадающим SHA256 tld.exe. Файловая версия Unity и Steam buildid сами по себе недостаточны. См. STEAM-2.55-RU.md.'
    }
    return $game
}
function Get-SkyCoopSteamManifest([string]$GameDirectory) {
    $game=(Resolve-Path -LiteralPath $GameDirectory).Path
    $apps=Split-Path (Split-Path $game)
    $path=Join-Path $apps 'appmanifest_305620.acf'
    if (-not (Test-Path -LiteralPath $path)) { throw 'Не найдена Steam-запись The Long Dark для выбранной папки.' }
    $text=[IO.File]::ReadAllText($path)
    $match=[regex]::Match($text,'"installdir"\s+"([^"]+)"')
    if (-not $match.Success -or [IO.Path]::GetFullPath((Join-Path $apps ('common/'+$match.Groups[1].Value))) -ne $game) { throw 'Выбранная папка не совпадает с установкой Steam. Запуск через Steam отменён.' }
    return $path
}
