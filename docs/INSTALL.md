# Experimental installation — The Long Dark 2.55, Windows x64

**Русский GUI:** распакуйте ZIP и откройте `Открыть-SkyCoop.cmd`. Подробности: `LAUNCHER-RU.txt`. Выбор папки не подтверждает версию игры; нужна отдельная TLD2.55 и тестовый профиль. Перед заменой файлов лаунчер запрашивает согласие.

**Minimal Survival/render probe:** follow [REBORN-CHARACTER.md](REBORN-CHARACTER.md) first. It bypasses legacy resources and ModComponent, uses the existing UDP transport, and retains a marker when character assets are absent. The character exporter has NOT run here; neither character rendering nor two real clients are validated. Keep the working TLD2.01/2.02 installation and its save profile separate.

**Not a playable release. Two real game clients have not been tested.** This kit contains all redistributable SkyCoop-owned runtime outputs, the managed Steam binding, symbols, diagnostics and installation tools. A complete working game installation is currently blocked by unvalidated/missing rebuilt resources and an item-pack format migration. The ZIP does not solve those blockers by bundling old assets.

## 1. Prepare an isolated seat

Use a separate legal test copy of The Long Dark 2.55 with a disposable save/profile for each seat. Confirm the store build and in-game version; do not infer the game version from the ZIP or Unity executable's file version. Back up saves. Do not install into a valuable live save setup. Use Windows x64; Linux/Proton is not validated by this artifact.

## 2. Install the external runtime and loader

Experimental pin: **MelonLoader 0.7.3 x64**, not “latest”. Its official release adds Unity 6000+ asset/image manager fixes. No exact loader/game combination is certified without game testing. Install **.NET 6 Desktop Runtime x64** (pinned official download 6.0.36), then the official loader for the isolated copy. SDK 8 is only a build tool. Follow the loader's official installation instructions; retain all its dependencies. Do not cherry-pick its MelonLoader.dll, change DLL names arbitrarily, or copy NuGet reference assemblies into the game.

Official sources and pinned hashes are in external-dependencies.json. For a verified download into this extracted kit's ExternalDependencies/ cache:

```powershell
.\Get-ExternalDependencies.ps1 -Id melonloader-0.7.3-x64
.\Get-ExternalDependencies.ps1 -Id dotnet-desktop-6.0.36-x64
```

The download helper never executes or installs downloaded files. Manually install the official runtime/loader after verifying the hash. Loader bootstrap details and .NET portable-runtime choices should follow its own documentation, not an old SkyCoop README.

## 3. Install this mod's files

Extract the ZIP into its own folder. Inspect the included scripts and manifest, then:

```powershell
.\Install-Experimental.ps1 -GameDirectory 'D:\TLD-Test-Host' -IsolatedCopy
.\Test-Installation.ps1 -GameDirectory 'D:\TLD-Test-Host'
```

The installer verifies hashes of the three copied files and copies only Mods/SkyCoop.dll, Mods/SkyCoop.pdb and UserLibs/Steamworks.NET.dll. Existing versions of those files are backed up under SkyCoop-Backups/. It does not change saves, the game's native steam_api64.dll, loader files or external resources. Manual installation uses exactly the same destinations. PDB symbols are included to improve managed stack traces.

## 4. Resolve the resource blockers before claiming an install is complete

For full legacy mode only, two external files are required: Mods/multiplayerstuff.unity3d and Mods/SkyCoop.modcomponent. The official v13.2 legacy ZIP is available for analysis via `-Id legacy-assets-v13.2`; **do not install its old SkyCoop DLL or assume its assets work**. The outer bundle is Unity 2018.4.21f1; the item pack contains another Unity 2018.4.2f1 bundle. ModComponent 7.0.0 rejects the old internal bundle path and uses an Addressables pipeline absent from the old pack. See ASSET-COMPATIBILITY.md.

Migration candidates are dommrogers/ModComponent 7.0.0 plus ModSettings 1.9.0; their release targets are older than 2.55. Their verified download entries are investigation inputs, not installation approval. Do not mix obsolete ds5678 net472/Unhollower dependencies into this net6/Il2CppInterop setup. The legacy assets' redistribution rights are not established, so they are external rather than repackaged. A compatible asset project/rebuild and migrated item pack are needed if the real-game load tests fail; merely editing headers or moving a ZIP entry is not a rebuild.

## 5. Collect diagnostics and follow the two-seat gates

Boot logs should show actual loader, Unity and game versions. SkyCoop writes UserData/SkyCoop/Diagnostics/session-*.jsonl and patch-manifest.json. Check every missing patch and external file. Follow TWO-PLAYER-TEST.md and DIAGNOSTICS.md. A working menu, clean compilation or presence of both DLLs does not prove multiplayer synchronization.

To roll back this mod, remove only the three copied mod files and restore their backups. Keep diagnostics and disposable saves for investigation. Do not remove or modify the game/loader's own libraries as part of rollback.
