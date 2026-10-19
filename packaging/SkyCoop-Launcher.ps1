param([switch]$SmokeTest)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
. (Join-Path $PSScriptRoot 'Launcher-Helpers.ps1')
$form = New-Object Windows.Forms.Form
$form.Text = 'SkyCoop 2.55 — экспериментальный запуск'
$form.Size = New-Object Drawing.Size(780,610)
$form.MinimumSize = $form.Size
$form.StartPosition = 'CenterScreen'
$form.Font = New-Object Drawing.Font('Segoe UI',10)
function Add-Label($text,$x,$y,$width,$height) {
    $control = New-Object Windows.Forms.Label
    $control.Text=$text; $control.Location=New-Object Drawing.Point($x,$y); $control.Size=New-Object Drawing.Size($width,$height)
    $form.Controls.Add($control); return $control
}
function Add-Button($text,$x,$y,$width) {
    $control=New-Object Windows.Forms.Button
    $control.Text=$text; $control.Location=New-Object Drawing.Point($x,$y); $control.Size=New-Object Drawing.Size($width,36)
    $form.Controls.Add($control); return $control
}
$null=Add-Label 'Выберите Steam-установку TLD 2.55. Версия проверяется до установки.' 18 14 720 28
$folder = New-Object Windows.Forms.ComboBox
$folder.Location=New-Object Drawing.Point(18,49); $folder.Size=New-Object Drawing.Size(585,30); $folder.DropDownStyle='DropDown'
$form.Controls.Add($folder)
$browse=Add-Button 'Обзор…' 615 46 125
$null=Add-Label 'Автопоиск находит папки, но не подтверждает версию игры. Проверьте 2.55 в меню.' 18 88 720 28
$ack = New-Object Windows.Forms.CheckBox
$ack.Text='Буду использовать тестовое сохранение; понимаю экспериментальный статус мода.'
$ack.Location=New-Object Drawing.Point(18,122); $ack.Size=New-Object Drawing.Size(720,48); $form.Controls.Add($ack)
$null=Add-Label 'Сохранения не удаляются и не переносятся. Старый SkyCoop — только с подтверждением.' 18 172 720 26
$null=Add-Label 'IPv4 хоста из Radmin VPN:' 18 215 245 26
$address=New-Object Windows.Forms.TextBox
$address.Location=New-Object Drawing.Point(266,212); $address.Size=New-Object Drawing.Size(235,28); $form.Controls.Add($address)
$null=Add-Label 'Например: 26.10.20.30' 515 215 225 26
$hostButton=Add-Button 'Создать сервер' 18 260 230
$guestButton=Add-Button 'Подключиться' 266 260 235
$logsButton=Add-Button 'Собрать логи…' 519 260 220
$null=Add-Label 'Сначала включите Radmin VPN на обоих ПК и войдите в одну сеть. Хост: UDP 26950; гость: UDP 26951. Лаунчер не меняет VPN и брандмауэр.' 18 309 720 54
$status=Add-Label 'Готово. Для первого теста загрузите копии одного тестового мира Выживания.' 18 374 720 66
$status.BorderStyle='FixedSingle'
$null=Add-Label 'Эксперимент: общий мир, предметы и сохранения не синхронизируются. Модель Reborn требует отдельного экспорта; без неё используется маркер. Игра на двух ПК не проверена.' 18 455 720 70
$help=Add-Button 'Инструкция' 18 526 170
$script:LaunchedAt=$null
$script:LaunchedFolder=$null
$script:GameProcess=$null
function Show-Error($message) {
    $status.Text='Ошибка: '+$message
    [Windows.Forms.MessageBox]::Show($form,$message,'SkyCoop — ошибка','OK','Error') | Out-Null
}
$browse.Add_Click({
    $dialog=New-Object Windows.Forms.FolderBrowserDialog
    $dialog.Description='Выберите отдельную папку The Long Dark 2.55 с tld.exe'
    if ($dialog.ShowDialog($form) -eq 'OK') { $folder.Text=$dialog.SelectedPath }
    $dialog.Dispose()
})
function Start-Seat([string]$Role) {
    try {
        if (-not $ack.Checked) { throw 'Подтвердите использование тестового сохранения в TLD 2.55. Старую установку 2.01–2.02 выбирать нельзя.' }
        $game=Assert-SkyCoopTarget $folder.Text.Trim()
        if ($Role -eq 'Guest') { Assert-SkyCoopIPv4 $address.Text.Trim() }
        # Не заменять файлы, пока любая копия игры запущена.
        if (@(Get-Process -Name tld,TheLongDark -ErrorAction SilentlyContinue).Count -gt 0) { throw 'Закройте все запущенные копии The Long Dark перед установкой и новым запуском.' }
        $loader=Join-Path $game 'MelonLoader/net6/MelonLoader.dll'
        if (-not (Test-Path -LiteralPath $loader)) { throw 'В тестовой копии не найден MelonLoader 0.7.3 x64. Установите его по инструкции.' }
        if ([Reflection.AssemblyName]::GetAssemblyName($loader).Version.ToString() -ne '0.7.3.0') { throw 'Нужен экспериментальный MelonLoader 0.7.3 x64. Другой загрузчик автоматически не заменяется.' }
        $confirm = {
            param($files)
            $message="В выбранной папке уже есть файлы:`n"+($files -join "`n")+"`n`nЗаменить их экспериментальными? Старые файлы будут сохранены в SkyCoop-Backups. Убедитесь, что это НЕ рабочая TLD 2.01–2.02."
            return [Windows.Forms.MessageBox]::Show($form,$message,'Подтверждение замены файлов','YesNo','Warning','Button2') -eq 'Yes'
        }
        Install-SkyCoopLauncherFiles $PSScriptRoot $game $confirm -SteamInstall
        $status.Text='Файлы установлены. Запускаем игру…'
        $script:LaunchedAt=[datetime]::UtcNow; $script:LaunchedFolder=$game
        $options=@{GameDirectory=$game;Role=$Role;IsolatedCopy=$true;DisposableProfile=$true;Port=26950;PassThru=$true}
        if ($Role -eq 'Guest') { $options.HostAddress=$address.Text.Trim() }
        # Прямой вызов скрипта с параметрами: никакого eval и сборки команд из введённого IP.
        $script:GameProcess = & (Join-Path $PSScriptRoot 'Start-MinimalSurvival.ps1') @options
        $status.Text='Игра запущена. Загрузите тестовое Выживание на обоих ПК. Соединение ещё не подтверждено.'
    } catch { Show-Error $_.Exception.Message }
}
$hostButton.Add_Click({ Start-Seat 'Host' })
$guestButton.Add_Click({ Start-Seat 'Guest' })
$logsButton.Add_Click({
    try {
        $game=Assert-SkyCoopTarget $folder.Text.Trim()
        if ([Windows.Forms.MessageBox]::Show($form,'Логи могут содержать IP, имена и локальные пути. Архив останется у вас; отправки в интернет нет. Продолжить?','Сбор диагностики','YesNo','Information','Button2') -ne 'Yes') { return }
        $dialog=New-Object Windows.Forms.SaveFileDialog
        $dialog.Filter='ZIP-архив (*.zip)|*.zip'; $dialog.FileName='SkyCoop-логи-'+[datetime]::Now.ToString('yyyyMMdd-HHmmss')+'.zip'
        $dialog.OverwritePrompt=$true
        if ($dialog.ShowDialog($form) -eq 'OK') {
            Export-SkyCoopDiagnostics $game $dialog.FileName
            $status.Text='Диагностика сохранена: '+$dialog.FileName
        }
        $dialog.Dispose()
    } catch { Show-Error $_.Exception.Message }
})
$steamButton=Add-Button 'Установить и открыть через Steam' 206 526 350
$steamButton.Add_Click({
    try {
        if (-not $ack.Checked) { throw 'Подтвердите использование тестового сохранения.' }
        $game=Assert-SkyCoopSteamVersion $folder.Text.Trim()
        $null=Get-SkyCoopSteamManifest $game
        if (@(Get-Process -Name tld,TheLongDark -ErrorAction SilentlyContinue).Count -gt 0) { throw 'Закройте игру перед обновлением мода.' }
        $loader=Join-Path $game 'MelonLoader/net6/MelonLoader.dll'
        if (-not (Test-Path -LiteralPath $loader) -or [Reflection.AssemblyName]::GetAssemblyName($loader).Version.ToString() -ne '0.7.3.0') { throw 'Установите MelonLoader 0.7.3 x64 по инструкции; существующий загрузчик автоматически не заменяется.' }
        $confirm={ param($files) [Windows.Forms.MessageBox]::Show($form,"Заменить файлы мода в Steam-папке? Старые файлы будут сохранены в SkyCoop-Backups. Это может заменить прежний SkyCoop. Продолжить?",'Подтверждение замены','YesNo','Warning','Button2') -eq 'Yes' }
        Install-SkyCoopLauncherFiles $PSScriptRoot $game $confirm -SteamInstall
        Start-Process 'steam://run/305620'
        $status.Text='Запуск через Steam. В главном меню выберите МУЛЬТИПЛЕЕР. Игровое соединение ещё не подтверждено.'
    } catch { Show-Error $_.Exception.Message }
})
$help.Add_Click({ try { Start-Process (Join-Path $PSScriptRoot 'LAUNCHER-RU.txt') } catch { Show-Error $_.Exception.Message } })
$timer=New-Object Windows.Forms.Timer
$timer.Interval=2000
$timer.Add_Tick({
    if ($script:LaunchedAt) {
        if ($script:GameProcess -and $script:GameProcess.HasExited) { $status.Text='Игра закрыта. При сбое нажмите «Собрать логи». Для повторного соединения перезапустите обе игры.'; $script:LaunchedAt=$null }
        else { $status.Text=Get-SkyCoopConnectionStatus $script:LaunchedFolder $script:LaunchedAt }
    }
})
$form.Add_FormClosed({ $timer.Stop(); $timer.Dispose() })
if ($SmokeTest) {
    if ($hostButton.Text -ne 'Создать сервер' -or $guestButton.Text -ne 'Подключиться') { throw 'Ошибка построения интерфейса.' }
    $form.Dispose(); Write-Output 'ПРОЙДЕНО: русский GUI создан без запуска игры.'; return
}
try { foreach ($path in @(Get-SkyCoopGameFolders)) { $null=$folder.Items.Add($path) }; if ($folder.Items.Count -gt 0) { $folder.SelectedIndex=0 } } catch { $status.Text='Автопоиск недоступен. Выберите папку кнопкой «Обзор».' }
$timer.Start()
[Windows.Forms.Application]::Run($form)
