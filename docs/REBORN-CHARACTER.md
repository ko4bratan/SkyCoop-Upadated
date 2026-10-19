# Экспериментальный персонаж Reborn для TLD 2.55

Статус: DLL компилируется; реализация загрузчика и анимации подготовлена. Unity AssetBundle НЕ собран в облаке. Нативная загрузка, внешний вид и два игровых клиента НЕ проверены. Это минимальный Survival-режим для проверки соединения и отображения персонажа, без общего мира, инвентаря, животных, урона, голосового чата и общих сохранений.

## Проверенные источники

Официальный [Filigrani/SkyCoop, Reborn commit7b0616e](https://github.com/Filigrani/SkyCoop/tree/7b0616e24173950de66d99c34d2f1a871de56161/SkyCoopBundles). Зафиксирован конкретный commit; другая ветка/версия может иметь другой контракт.

- `Assets/Prefabs/SkyCoopPlayer.prefab` — вариант импортированной модели `Assets/Models/TLD_Person_Model_Last.fbx`, GUID9cf42d351eff86c4eb9d7041561def2c. FBX содержит skinned meshes одежды/тела и скелет с `hips`, `root`, `Spine1`, `Spine2`, `Chest`; Humanoid mapping описан в `.fbx.meta`. Бинарный FBX здесь не импортировался Unity Editor: полный список импортированных материалов/костей пока неизвестен.
- `Assets/Animations/Player.controller`, GUID194d057b645f03d4f9c5ba5b96488574 — контроллер prefab. Параметры: `IsMoving`/`Crouch`/`Vehicle` bool; `DirectionX`/`DirectionY`/`Speed` float; `Action`/`Gear` int. Есть Idle, Walk и Crouch, directional blend trees. Walk/Crouch используют параметр Speed. Отдельный авторский клип Run не найден: Run пока означает ускоренную походку, а не новую беговую анимацию.
- Текстуры проекта: `Assets/Textures/Face.png`, `Beard.png`, `Browns.png`, `Foots.png`, `Legs.png`, `Pants.png`, `Mouth.png` и другие. Это список доступных файлов, не доказательство, что все они подключены к выбранным мешам. `Materials/Mouth.mat` и `refmanwhite.mat` используют встроенный shader; GraphicsSettings не назначает SRP. Фактические mesh/material/texture зависимости выбранного prefab определит Unity: builder сохраняет их отдельными отчётами.
- `ProjectSettings/ProjectVersion.txt`:6000.0.60f1, revision61dfb374e36f. Это версия ИСХОДНОГО ПРОЕКТА, а не доказанная версия установленной игры. `startup.identity` записывает реальную `Application.unityVersion` при запуске игры. До получения этих логов точный Unity runtime конкретной установки неизвестен.
- `SkyCoopClient/AssetManager.cs` загружает UI/player bundle напрямую; ModComponent item pack — отдельная система. Полный проект содержит Addressables2.7.4 в manifest и2.7.6 в lock, ModComponentSDK и AssetBundles Browser. Подготовка выравнивает Addressables на2.7.6 и фиксирует git-зависимости. Для отдельного персонажа builder использует обычный AssetBundle, включает зависимости в него и не требует загрузки Addressables/ModComponent в игре. Это не миграция старого `SkyCoop.modcomponent`.

Legacy `multiplayerPlayer` ожидает корневые Clothing/Body/Extras/hips/Speaker и глубокие пути для факела, камеры, голоса и урона. Reborn prefab этим контрактом не является. В минимальном режиме используется независимый объект рендера, без старых `MultiplayerPlayer` и новых `NetworkPlayer`, без коллайдеров, Rigidbody, контроллеров, камер, звука, скриптов, инвентаря или взаимодействий. Слоты `MyMod.players[]` остаются null.

## Что реализовано в DLL

- Тот же UDP transport и базовый пакет9055/version1, прежний бинарный формат координат. Пакет9056/version1 отдельно передаёт Crouch/Run, session nonce и sequence. Сверяются DLL SHA256, game identity и seed; разные сборки намеренно не соединяются. Это сохранение формата протокола, не обещание подключения старой DLL к новой.
- Буфер16 snapshots, задержка интерполяции100мс, короткий поворот через0/360°, отбрасывание старых sequence, сброс при смене сцены/скачке >25м; нет extrapolation. Через300мс без состояния останавливается анимация, через10с скрывается объект и записывается timeout.
- Reborn Animator получает параметры движения; RootMotion отключён. Crouch приходит из `PlayerIsCrouched`, Run — из `PlayerIsSprinting`; скорость/направление оцениваются по входящим координатам. Idle/Walk/Crouch и ускоренный Run ещё требуют визуальной проверки.
- Loader принимает только санитизированный `Mods/SkyCoopResources/skycoop-character` и `character.json`, проверяет SHA256, версию Unity, prefab, типы компонентов, rig/controller, параметры, меши и поддерживаемые shaders. Чужие скрипты отклоняются ДО Instantiate. Ошибка загрузки/рендера оставляет капсулу и не закрывает UDP.
- Проверки не могут предотвратить любой native crash Unity. Поддерживаемый shader ещё не гарантирует отсутствие розовых материалов, корректное освещение или подходящий rig.

## Локальная сборка ресурсов на Windows

Нужны Git, Python3, лицензированный Unity Editor6000.0.60f1 и Windows Build Support. Игру2.01–2.02 не менять. Работать в отдельном каталоге исходников; проект Unity нельзя помещать в каталог игры.

1. Сначала запустить DLL без ресурсов по инструкции ниже и сохранить `startup.identity`/MelonLoader log с фактическими TLD/Unity/loader версиями. Если Unity игры отличается от6000.0.60f1, остановить сборку: нужен отдельный проверенный профиль Editor, соответствующий установленной игре. Самовольное изменение одной строки manifest не исправляет совместимость.
2. Из checkout этой ветки выполнить:

   ```powershell
   py -3 tools/prepare_reborn_project.py --destination C:/SkyCoop255/reborn-source
   ```

   Скрипт скачивает официальный pinned commit в НОВЫЙ каталог. Manifest фиксирует SDK `f2f7a81a7c5cc3699a655b113559388bbdd601a1` и Browser `c7923746fa5e95916bf1a4087eceadb72305d5b5`. Исходный prefab не перезаписывается. Первый импорт пакетов требует сети. Нужны все зависимости проекта; при compile/import error сохранить Editor.log, не брать случайные DLL из старой игры.
3. Выполнить (путь Editor при необходимости заменить):

   ```powershell
   & 'C:/Program Files/Unity/Hub/Editor/6000.0.60f1/Editor/Unity.exe' `
     -batchmode -quit -nographics -buildTarget Win64 `
     -projectPath C:/SkyCoop255/reborn-source/SkyCoopBundles `
     -executeMethod SkyCoopCharacterBuild.Build `
     -skycoopAssetOutput C:/SkyCoop255/character-output `
     -logFile C:/SkyCoop255/character-editor.log
   if ($LASTEXITCODE -ne 0) { throw 'Character export failed: inspect editor log' }
   ```

   Builder создаёт отдельный prefab, оставляет один фиксированный комплект мешей, удаляет все игровые компоненты, отключает RootMotion, клонирует clips без animation events и экспортирует Windows64 bundle. Выход: `skycoop-character`, `character.json`, список зависимостей и render inventory. Наличие файлов ещё не подтверждает native load. Эта Editor-процедура подготовлена, но здесь не исполнялась; import/package/API ошибки остаются возможны.
4. После успешной сборки скопировать ТОЛЬКО `skycoop-character` и `character.json` в `Mods/SkyCoopResources/` отдельной тестовой установки на обоих ПК. Не класть туда весь Unity-проект, legacy DLL, весь skycoop bundle или старый modcomponent. Проверить SHA256 на обоих ПК.

## Установка DLL и первый тест двух ПК

1. Подготовить отдельную копию **TLD2.55 Windows x64** на каждом ПК и отдельные disposable Windows-профили/профили сохранений. Копирование только игры не изолирует `%USERPROFILE%/AppData/...`! Существующие2.01/2.02 и их сохранения оставить нетронутыми. Проверить версию в меню/логе; идентичности exe/store/build записать в частный протокол.
2. Установить туда MelonLoader0.7.3 x64 и .NET6 Desktop Runtime x64 по `DEPENDENCIES.md`.0.7.3 — закреплённый экспериментальный кандидат, а не подтверждённо совместимый loader. Проверка AssemblyRef/TypeRef не заменяет runtime test. Для probe не устанавливать ModComponent/ModSettings и старые bundles: они не требуются. Удалять чужие моды в рабочих установках не нужно; тестовая копия должна быть чистой.
3. Распаковать experimental ZIP. В PowerShell:

   ```powershell
   ./Install-Experimental.ps1 -GameDirectory C:/SkyCoop255/Game -IsolatedCopy
   ./Test-Installation.ps1 -GameDirectory C:/SkyCoop255/Game -Minimal
   ```

4. На хосте создать disposable Survival world. Сделать неизменяемую резервную копию; перенести копию тестового мира на гостевой disposable профиль. Оба игрока должны загрузить один seed, регион и наружную сцену. Автоматического создания/переноса save нет. Каждый vanilla-клиент пишет СВОЙ тестовый save: probe не синхронизирует эти записи.
5. Разрешить UDP26950 на хосте и UDP26951 на госте в private LAN firewall. Начать с одной локальной сети, без публичного порта/Steam relay. В разных сетях нужен отдельно настроенный UDP route/VPN; мод не делает NAT traversal.
6. Запустить на хосте:

   ```powershell
   ./Start-MinimalSurvival.ps1 -GameDirectory C:/SkyCoop255/Game -Role Host -IsolatedCopy -DisposableProfile
   ```

   На госте (заменить IPv4 на адрес хоста):

   ```powershell
   ./Start-MinimalSurvival.ps1 -GameDirectory C:/SkyCoop255/Game -Role Guest -HostAddress 192.168.1.10 -IsolatedCopy -DisposableProfile
   ```

   Launcher задаёт `-skycoop-minimal`, роль и D3D11, оставляет обычные Survival menus. На обоих клиентах вручную загрузить disposable Survival. Не использовать старое сетевое меню. Оба должны показать peer accepted и растущий счётчик received states. Сначала проверить marker fallback без ассетов (или добавить launcher-параметр `-MarkerOnly`), затем перезапустить с locally built character без `-MarkerOnly`.
7. Проверить оба направления: Idle10с; Walk вперёд/назад/вбок; ускоренный Run; Crouch стоя/в движении; разворот350°→10°; обход локального игрока; вход/выход из интерьера. Персонаж не должен толкать игрока, перехватывать камеру/звук/подбор вещей, мешать инвентарю или падать под физикой. Гость и хост должны видеть по ОДНОМУ персонажу. Смена одежды в этой версии не передаётся.
8. Негативные проверки: отсутствующий bundle, неправильный hash, другой Unity manifest, недоступный IP, разный seed, peer exit, потеря UDP. Ошибка ассетов должна оставить маркер; ошибка соединения должна иметь точный лог. После disconnect/restart выполнить полный перезапуск клиента: автоматическое восстановление сеанса пока не реализовано.
9. Сохранить на обоих ПК: MelonLoader/Latest.log, Player.log, `UserData/SkyCoop/Diagnostics/*.jsonl`, `minimal-state.json`, patch manifest, SHA256 DLL/bundle, фактические версии, скриншоты и длительность теста. Поля `realTwoClientTestComplete=false` намеренно не объявляют автоматический успех. Только отдельный QA-протокол с реальными двумя клиентами может подтвердить результат.

## Точные блокеры и практический следующий шаг

В облаке нет Unity Editor, его лицензии и реальной TLD2.55: нельзя импортировать FBX, собрать/прогнать shaders, проверить нативный loader или увидеть рендер. Следующий шаг — локальный export выше и обе игровые сессии. Если build/import не проходит, прислать Editor.log; если появляется маркер — `character.load-fallback` и оба game logs; если модель белая/розовая/неверной высоты — screenshot плюс render inventory. Это позволяет исправлять конкретный shader/texture/rig/root-offset, не переписывая сеть.

MIT лицензия корневого репозитория не устанавливает автоматически права на каждую стороннюю модель/текстуру/Mixamo-клип и на отдельное распространение исходных assets. Здесь не публикуются Reborn binary assets. Локальная подготовка исходников и инструкции не являются разрешением на их распространение: для готового публичного bundle нужен отдельный аудит происхождения выбранных meshes/textures/clips и разрешений. Утверждать, что совместимые и законно распространяемые character assets уже готовы, пока нельзя.
