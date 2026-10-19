# Audit findings

Follow-up fixes are isolated on port/tld-2.55-audit. Managed regressions and compilation are distinct from live game QA. No real host/client run has occurred.

## Open

### SC-002 — required play assets absent (distribution blocker, high confidence)

README.md:50-80 lists multiplayerstuff.unity3d and SkyCoop.modcomponent. Neither is present in the complete upstream tree; releases endpoint returned an empty collection. There is no Unity asset project in this tree. Source-only checkout cannot deliver the documented complete installation. Obtain an official, licensed, versioned bundle/component and validate hashes and Unity compatibility. Missing bundle disables UI/models (MyModMain.cs:1466-1482). The official Filigrani v13.2 archive contains a Unity 2018.4.21f1 bundle and a component; their hashes are recorded in docs/ASSET-PROVENANCE.md. No live compatibility test was possible, and they are excluded from the test ZIP. No inference that all historical bundles are incompatible.

### SC-003 — patch failures lack a compatibility gate (high risk, static observation)

PatchLoader.cs:49-73 catches failures per patch and continues. Search of all source found FailedPatches used only inside PatchLoader. Thus a loaded mod can have disabled subsystems while logging that the rest works. Partial class application/rollback semantics are unknown. Need a full patch manifest and host/client behavior checks; do not label 2.55 compatible from a successful startup alone. No missing target method has yet been confirmed.

## Suspected / needs evidence

- SaveSlotCompat.LoadDataFromSlot returns null on missing/error (GameCompat.cs:392-407); MyModMain.cs:730 calls GetHashCode without checking null. Determine dedicated rendered-server reachability and reproduce with an isolated missing/corrupt slot before fixing.
- Save persistence callers often ignore a false return (Patches.cs:3006, 3079-3131). Exercise write failure/reload and verify whether users can lose multiplayer state. Actual loss has not been observed.
- Locked NuGet dependencies compile, and static assembly/type resolution against official MelonLoader0.7.3 passes. Full method behavior, regenerated game interop and native Steam ABI remain unverified; real runtime logs are required.

## Fixed in source; game validation pending

### SC-001 — recursive bundle resource lookup (critical)

MyModMain.cs:1487-1499 now delegates to LoadedBundle.LoadAsset<T>(name) and constrains T to UnityEngine.Object, matching the real 2.55 interop API. All callers compile. Regression checks execute the actual extracted method body against a controlled bundle facade: absent bundle, valid asset and missing-asset deduplication. Restoring the recursive call in generated test code makes the child process fail, proving the check detects the original defect. Native Unity resource loading remains untested without a valid bundle/game.

### SC-004 — Steamworks.NET omitted from build output

SkyCoop.csproj concatenated NuGetPackageRoot and steamworks.net without a separator. With NUGET_PACKAGES set to a path without a trailing slash, the copy silently did nothing. It now uses GeneratePathProperty and PkgSteamworks_NET, explicitly selects the Windows x64 managed binding, and fails if missing. The Release directory and test ZIP contain the exact restored runtime DLL. No native Steam library is shipped or replaced.

## Experimental follow-up fixes and open blockers

- All six compiler warnings fixed (WARNING-AUDIT.md), with0 warnings under warning-as-error Release build.
- Diagnostics now report startup identities/hashes, full patch manifest, packet-handler failures, connection errors and save exceptions. This improves evidence for SC-003; actual Harmony/game compatibility is still open.
- ClientUser.UDP's unused private Disconnect method contained self-recursion; it now closes its own socket and clears state. Receive-failure game-state cleanup is queued on the main thread and ignores callbacks from an obsolete UDP instance. Live reconnect/travel tests remain required.
- SC-002 now has a concrete nested-pack finding: original auto-mapped/scitems.unity3d is rejected by ModComponent7 and lacks Addressables catalogs. Outer Unity2018.4.21f1 and inner2018.4.2f1 bundles remain native-runtime untested. See ASSET-PROVENANCE.md.
- Experimental loader candidate is0.7.3 x64 due official Unity6000 fixes; static assembly/type reference resolution passes, but game compatibility is not certified.

## Refuted

None.

## Out of reach in this environment

Executable/store identity; full live 2.55 API behavior; valid-bundle reproduction; two-client game QA. Compilation against pinned 2.55 references now passes. These are environment gaps, not proof of defects.

## Reborn/minimal Survival follow-up

- Observed: official Reborn source project6000.0.60f1 provides SkyCoopPlayer prefab, FBX rig and Player.controller. Parameter contract and legacy hierarchy mismatch verified from pinned source7b0616e; detailed sources/unknowns in docs/REBORN-CHARACTER.md. Actual TLD runtime identity remains unknown.
- Implemented, game validation pending: opt-in transport/render probe bypasses all legacy gameplay/asset initialization; keeps UDP framing and9055; adds optional9056 pose. Separate renderer never occupies legacy players[] or attaches gameplay components. Missing/bad render asset falls back to marker without closing transport. Pure interpolation and protocol behavior have managed checks.
- SC-005: missing chatPanel/MP_ChatText previously dereferenced during welcome chat rendering; rendering now requires panel, prefab and Text component while network fanout remains outside that condition. Static trigger confirmed; live welcome test pending.
- Open: no Unity Editor/license/game here, character export NOT RUN. No redistributable Reborn assets included; selected art/clip rights need specific review. Run is currently speed-scaled Walk, not an authored run clip. Full world/items/save/voice synchronization remains disabled in probe.
