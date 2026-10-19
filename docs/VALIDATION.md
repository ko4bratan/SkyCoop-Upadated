# Validation record — 2026-10-19

## Checks completed

- Real fork and all existing branches inspected. BeforeTFTFT and claude/long-dark-multiplayer-compat-c7d6qo have identical source trees. Development branch is based on the original commit; neither existing branch was changed.
- Prior Co-op Anything setup reused. MCP JSON-RPC initialize, tools/list (8 tools), directory search and revive skill read: PASS. Six skill symlinks resolve. Framework remains outside runtime dependencies and ZIP.
- Microsoft .NET SDK 8.0.426 archive: official SHA-512 verified; committed SHA-256 is bb691c98abc8797eedbd48d3a766bc734214aed746e52e2691d8bbb268c60764.
- Locked NuGet restore and Release compilation against TLD 2.55.0: PASS, **0 errors, 6 existing warnings**. Warnings concern obsolete WebClient/SHA256Managed and comparisons of value types with null; they remain visible and were not suppressed.
- Managed regression suite: **6/6 PASS**. Covers actual extracted BundleAsset method body with a controlled bundle facade, plus the original main-thread queue source (order, next-tick behavior and concurrent producers).
- SC-001 mutation: PASS. Restoring the recursive call in disposable generated test source makes the child process fail; production source remains fixed.
- Reproducibility: PASS. Independent source directories, one with Git metadata and one without, produced identical SkyCoop.dll and Steamworks.NET.dll. SourceLink's Git metadata queries are disabled; an explicit PathMap normalizes source paths. The package manifest records source commit separately.
- Packaging: PASS. Deterministic ZIP bytes, exact build-output bytes, internal hashes, expected file allowlist and explicit missing-assets status. Missing required DLLs correctly prevent packaging. Game/interop DLLs and Co-op Anything are excluded.
- Python tool syntax compilation and Git diff whitespace check with upstream CRLF handling: PASS.

## DLL SHA-256

```
c73b72dffb48115f78c67b3c6a2ad0d36f860ce779f2d241f097dff27e1d82a4  SkyCoop.dll
de85048ba8d7337627034cfb11481e76604a76a4d7f2263cc4785462e5d11bba  Steamworks.NET.dll
```

The official legacy v13.2 assets were also inspected and hashed (ASSET-PROVENANCE.md); the Unity 2018.4.21f1 bundle is not validated for 2.55 and is excluded from the ZIP.

## Checks not run

Native Unity/IL2CPP asset loading, live Harmony target application, Steam ABI/lobbies, direct-IP host/client gameplay, save migration, DLC synchronization, latency/loss and clean two-client exit have **not** run. The environment has no installed The Long Dark or recorded store/build/executable identity. The external asset bundle and component are absent from the selected source tree. The test ZIP is a compiled development kit, not a complete validated multiplayer release. Required isolated game gates are documented in TWO-PLAYER-TEST.md.

## Source preservation

49 of the 52 original upstream files remain byte-for-byte unchanged. Only .gitignore, MyModMain.cs (two-line repair) and SkyCoop.csproj (build/reproducibility settings) differ. Original source remains in the untouched fork branches and Git ancestry; no synthetic snapshot history was pushed. New tests/tools/instructions are separate files.
