# SkyCoop 2.55 — two-player test kit

Development build. Compile checks and managed regressions do not establish multiplayer compatibility. Real host/client QA has not run. Do not use valuable saves.

## Contents and prerequisites

- Mods/SkyCoop.dll: the modified mod.
- UserLibs/Steamworks.NET.dll: Windows x64 managed binding, version 20.2.0.
- manifest.json and SHA256SUMS: source identity, prerequisites and file hashes.
- License notices. Co-op Anything and all build/test tools are excluded from the runtime kit.

Both seats need their own legal The Long Dark 2.55 installation, MelonLoader 0.7.x and matching mod files. The compile reference resolves MelonLoader 0.7.1; 0.7.3 runtime behavior is not validated here. The **multiplayerstuff.unity3d** bundle and **SkyCoop.modcomponent** are external prerequisites, absent from this repository and ZIP. Obtain the official compatible assets and record SHA-256. The component requires ModComponent. The official v13.2 legacy archive was inspected: its bundle header is Unity 2018.4.21f1, and it is not validated for 2.55. See ASSET-PROVENANCE.md in the source repository. No claim is made that an arbitrary older bundle works with the game's Unity version.

Create isolated copies of the game for host and guest, with disposable saves/profiles. Do not overlay the user's live install or saves. Put the two Mods files and component in each test copy's Mods/; put the managed Steamworks.NET.dll in UserLibs/. Do not copy NuGet game/interop references into either installation. Do not replace the game's native steam_api64.dll.

For initial direct-IP testing, the code's server default is UDP 26950 and client default local port is 26951. The guest can use the host's LAN address; on one machine use 127.0.0.1 with isolated profiles. Inspect actual startup logs and port bindings. Do not assume two Steam sessions on one machine are supported. Steam lobby tests should use two separately authorized seats.

## Required automated game gates (pending)

An in-game driver must run actions through the real game/network paths and report state from both seats. No such driver is included yet. This is the remaining game QA work, not a request for a human playtest to replace it.

1. Record store/build, executable hash, Unity/loader versions, bundle hash and mod hash on both seats. Require matching identities.
2. Boot host and guest; record every applied/skipped Harmony patch. Fail on any required missing patch, crash or timeout.
3. Host a disposable Survival slot with at least two player slots, join the guest, and assert two connected players on both seats. Exercise a known bundle asset and a missing asset; both lookups must terminate.
4. Move each player; compare remote position snapshots with a bounded tolerance and timeout.
5. Transfer/drop/pick up an item; contend for one container. Assert one authoritative item owner, no duplication and matching shared contents.
6. Travel indoors/outdoors and between regions; test late join, disconnect and reconnect. Assert stable scene/GUID ownership.
7. Save, exit and reload both seats; compare shared state. Repeat relevant wildlife and DLC trader/cougar/travois tests.
8. Repeat join/travel/state tests with controlled latency/loss; require both clients to close cleanly. Missing results, crashes and timeouts cannot pass.

Keep private logs, identities and saves in ignored local/. Report QA as untested until these gates actually run on the exact game. A working menu or a successful join is insufficient evidence of synchronized multiplayer.
