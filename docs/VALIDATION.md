# Validation record — experimental installation follow-up

The original 23f6e61 build/audit has been continued on the same development branch. The existing BeforeTFTFT and claude branches and original Git ancestry remain preserved.

## Completed locally

- Pinned SDK8.0.426, locked TLD2.55.0 dependencies, Release rebuild with warnings treated as errors: **0 compiler warnings, 0 errors**. All six previous warnings were audited and fixed rather than suppressed (WARNING-AUDIT.md).
- **11/11 managed regressions PASS**: asset lookup and missing assets, original-recursion mutation, SHA-256 compatibility/disposal, Steam ID validity, main-thread scheduling, concurrent queue producers, diagnostic JSON/rate limiting/rethrow/report behavior. These tests use a controlled bundle facade and do not execute native Unity code.
- Original SC-001 recursion restored in generated test source: child process fails as required. Production source remains fixed.
- **3/3 Python asset-inspection tests PASS**: UnityFS identity, hash rejection and actual nested pack structure. Official legacy archive hashes and26 JSON files checked without executing downloaded content.
- Static runtime audit:43 AssemblyRefs categorized;18 require external .NET6,3 resolve against official MelonLoader0.7.3,21 against the2.55 compile snapshot,1 against the shipped Steam binding. No unresolved non-framework references or missing direct types. Method/native/installed-game behavior remains unverified.
- Independent-directory source rebuild: **SkyCoop.dll, SkyCoop.pdb and Steamworks.NET.dll are byte-identical**. A local restore initially reported NU1900 when vulnerability metadata was unreachable inside the network sandbox; this is a network audit warning, not a remaining C# warning. The network-enabled CI reruns restore.
- Package checks: deterministic ZIP, exact payloads and checksums, explicit external/migration blockers, rejection of missing required outputs. Python syntax and upstream CRLF-aware Git whitespace checks pass.
- Co-op Anything stdio MCP preflight remains available. It is development-only and excluded from the runtime and installation ZIP.

## Current shipped binary SHA-256

`1649de3e2ee106e4017e3e242202fbda5270e09915f6cc15a7bf28a9869eff9b` — SkyCoop.dll

`ac03851831d8b28bbe15447af07087702444dc2246ddccbf7693eaa9fb15c8c8` — SkyCoop.pdb

`de85048ba8d7337627034cfb11481e76604a76a4d7f2263cc4785462e5d11bba` — Steamworks.NET.dll

## CI-only installer checks

GitHub Actions builds and publishes the experimental ZIP. A separate Windows job exercises the actual PowerShell installer against a fake executable fixture: exact copies, preservation of old binaries in backups, refusal without isolated-copy acknowledgement, and rejection of a tampered dependency. This is a file-operation test, not a game launch. Consult the final Actions run status for the result.

## Not run / blockers

No actual game/store/executable identity was supplied. Two real game clients, native Unity asset loading, live Harmony application, full method-signature/interop verification, Steam native ABI, player/item/world replication, saves/travel/DLC/lag/exit remain **NOT TESTED**.

The outer legacy bundle is Unity2018.4.21f1; the embedded item bundle is2018.4.2f1. The old item pack has a confirmed ModComponent7 layout/Addressables migration blocker. No licensed compatible resource rebuild has been obtained. Thus the artifact is an experimental installation kit with explicit external instructions, not a complete working multiplayer installation or a playable release.
