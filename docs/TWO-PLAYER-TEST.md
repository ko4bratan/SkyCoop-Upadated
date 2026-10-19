# Two-player acceptance checklist — experimental The Long Dark 2.55

**Status: NOT RUN with real clients.** All boxes below are pending. Managed tests, metadata inspection and installer fixtures are separate checks. A human session is useful supporting evidence; it does not replace the planned in-game driver and shared-state assertions. No game-driving automation is shipped yet because an exact game installation and migrated resources are unavailable here.

## Run record (fill separately for host and guest)

Record run ID and UTC start/end; machine/OS/architecture; store and exact build ID; game version; main executable SHA-256; observed Unity version; loader version; .NET runtime; mod DLL/PDB hashes; Steam binding hash; outer bundle and item-pack hashes; ModComponent/ModSettings versions; DLC ownership; transport and configured ports; disposable save identity; artifact commit and manifest. Keep private paths, account IDs, saves and logs out of public reports.

Both seats must use the same artifact and matching migrated assets. Pin the experimental loader to0.7.3 x64 and verify .NET6 Desktop Runtime. Do not accept a new loader version merely because it is newer. Use separate legal test copies and disposable saves/profiles. Steam lobby tests require separately authorized seats/accounts; do not assume two Steam sessions work on one PC.

## Gate0 — prerequisites and resource migration

- [ ] Both seats' store/game identity confirms2.55; compare executable and mod hashes.
- [ ] Official0.7.3 loader starts and generates interop successfully; retain loader/runtime logs.
- [ ] `Test-Installation.ps1` reports expected loader version and all required files. Its output is evidence of presence, not compatibility.
- [ ] Original item-pack path/Addressables blockers are resolved in a licensed migrated pack; record the migration source/rebuild and catalogs. Do not mark this gate passed with the unmodified v13.2 pack.
- [ ] Outer bundle loads in the actual Unity runtime; record any load exception rather than guessing from the header.
- [ ] Resolve known UI/player prefab, absent prefab, animation, material/shader, fonts and custom MonoBehaviour bindings. Assert known asset is non-null and missing asset terminates safely.
- [ ] ModComponent registers migrated items, recipes and spawn tables without pack-not-working/catalog errors. Verify stock gear still works and serialized custom gear round-trips.

## Gate1 — startup and diagnostics

- [ ] Capture both `session-*.jsonl`, `patch-manifest.json`, MelonLoader Latest.log and Unity Player.log.
- [ ] Compare startup identity/hash events to the artifact manifest. Require zero unexplained failed critical patches; explicitly classify any optional failure before continuing.
- [ ] Confirm the Unity diagnostic patch itself applied. Induce a controlled managed error in test instrumentation; verify severity, context, stack and session ID. Do not intentionally trigger a native crash on a valuable setup.
- [ ] Start offline: optional supporters lookup must timeout/fail diagnostically without stopping startup. Separately test ModComponent's external dependency checker; it is outside SkyCoop's control.
- [ ] Test unwritable diagnostic directory in a disposable fixture: one warning and original gameplay error preserved, not a logging crash.

## Gate2 — transport and admission

- [ ] Use direct-IP first on a controlled LAN: default server UDP26950, guest local UDP26951. Verify actual port bindings; do not infer connectivity from an open socket.
- [ ] Host a disposable Survival slot with capacity >=2; join guest. Assert **two actual connected players** on both seats. Logged slot capacity is not actual player count.
- [ ] Check version/mod mismatch rejection, unavailable host, wrong port, full server, host exit during join and cancellation. Record diagnostic event plus visible outcome; no indefinite wait or false success.
- [ ] Join twice, leave and rejoin; verify no duplicate player, ghost ownership or leaked socket.
- [ ] After direct-IP works, test Steam lobby creation, invite, invalid/default friend/server IDs, callback errors, host loss and join from main menu. Record native entry-point failures separately; never replace the game's steam_api64.dll to hide them.

## Gate3 — basic replication

- [ ] Driver commands each seat to move/turn/crouch/equip in turn. Capture authoritative and remote snapshots with UTC/sequence numbers; use a documented position/time tolerance chosen before the run.
- [ ] Assert host and guest remain in multiplayer mode and see the expected peer throughout the run. Missing snapshots/results or mode fallback cannot pass.
- [ ] Exercise interaction reach, doors, light/fire, sleep/time/weather, injuries and death/revival. Compare shared state; separate intentional per-player differences from desynchronization.
- [ ] Capture handler errors by packet ID/size and scene. A diagnostic-free run alone is insufficient; assert shared values independently.

## Gate4 — items, containers and persistence

- [ ] Host drops stock item, guest picks it up, then reverse roles. Assert one item owner and one world instance.
- [ ] Contend for one item/container simultaneously; require no duplication, loss or divergent contents.
- [ ] Repeat with migrated custom gear, liquids/weights/stacked items, crafted items, photographs/large sliced data and death containers.
- [ ] Save, exit and reload; verify multiplayer metadata, ownership/GUIDs and container/item state on both seats.
- [ ] Inject an isolated save-write/read failure; require explicit diagnostics and a failure result. Do not accept silently missing multiplayer state.

## Gate5 — travel, late join and recovery

- [ ] Travel indoor/outdoor and between regions, including Far Territory with matching DLC ownership. Test guest staying behind and following later.
- [ ] Late join after world changes; assert shared state, stable scene/item GUIDs and correct spawned player/animal ownership.
- [ ] Disconnect/reconnect guest during movement, inventory access, save, travel and large transfer; assert deterministic recovery or explicit rejection.
- [ ] Host closes during the same operations; verify clients leave cleanly and no obsolete socket callback disconnects a later session.
- [ ] For the supported content scope, test cougar/threat, ptarmigan, travois/cargo, noisemaker and trader stock/trust/delivery. Compare after travel, late join and save/reload. Story challenges remain outside the declared multiplayer scope.

## Gate6 — degraded network and exit

- [ ] Repeat admission, item contention, travel and recovery under controlled delay, loss and a temporary outage on your own test transport. Record exact settings and keep the ordinary LAN control run.
- [ ] Run a bounded soak with both seats reporting state; fail on growing divergence, timeout, crash, missing result or uncontrolled log growth.
- [ ] Quit guest then host, then reverse order. Assert clean process termination and preserved disposable-save state. An exit crash is a failed gate.
- [ ] Test the automated scorer with intentionally missing results, timeout and failed assertion; it must reject them. Turn each behavioral fix off in a disposable build and require its regression to fail.

## Evidence and release decision

For every box record PASS/FAIL/BLOCKED, exact trigger, expected/observed values, timestamps and both seats' evidence. An untested gate is BLOCKED, never PASS. Keep native crash reports even when no managed exception was logged. Publish only a reviewed redacted summary with artifact/source identities; logs and saves stay private by default. The mod may be described as playable only after two real matching game clients complete the agreed supported-scope gates and the recorded limitations are explicit.
