# The Long Dark 2.55 — initial audit

Historical findings from the first setup are retained below. Follow-up: a real fork clone and pinned SDK now exist; Release compilation succeeded. SC-001 is repaired and covered by isolated managed regressions with a failing original-bug mutation. SC-004 fixes missing Steamworks output. Current status and test evidence are in BUGS.md, BUILD.md and VALIDATION.md. Game QA remains untested.

## Observed

- Upstream README already calls this a 2.55 port. SkyCoop.csproj targets net6.0, TLDVersion 2.55.0, STBlade.Modding.TLD.Il2CppAssemblies.Windows and Steamworks.NET 20.2.0. These are declarations, not verified package resolution or runtime compatibility.
- Compatibility shims exist in GameCompat.cs; per-class Harmony loader exists in PatchLoader.cs; DLC synchronization exists in FarTerritory.cs and packet handlers. The task is validation and revival of existing code.
- MIT license is included. README credits original SkyCoop authors and third-party models/tools. Asset redistribution rights still need separate verification.
- Complete file inventory: 52 upstream files; no upstream AGENTS.md, CI build workflow, test suite, Unity asset project or runtime bundles. Only a bug-report issue template exists under .github.
- Co-op Anything DIRECTORY.md:222 contains SkyCoop as partial and untested; no dedicated game write-up. Open issue and PR searches for "Long Dark" in that repository returned zero matches on 2026-10-19. DoperDodge fork has issues disabled and no published releases.
- All imported source files passed Git blob hash comparison. No C# runtime source or csproj dependency changes have been made.

## Derived

- Correct workflow: triage/research -> revive -> QA. Console/decomp-port and building new multiplayer from zero do not fit this source tree. The playbook's Unreal subclass-package example is not a prescription for this Unity C# project; preserve the existing mod and make minimal reviewed changes.
- BundleAsset recursion is a critical defect under the valid-bundle condition (SC-001).
- Missing distribution assets and silent partial patch application prevent treating the README's compatibility statement as established (SC-002/003).

## Unknown

Exact user's store/build and executable hash; actual Unity version; game availability; 2.55 interop API correctness; NuGet restore/build success; whether DLC trader/cougar/travois changes survive late join, travel, reconnect and reload; standalone dedicated-server integration; asset provenance and compatibility.

## Next audit gates

1. Restore a working proxy and scoped GitHub CLI authorization; create the real fork and branch, preserving upstream history.
2. Restore/build Release with a pinned SDK. Review package contents and record dependency hashes. Compile first; do not install into an owner's live game directory.
3. Verify and repair SC-001 against the actual interop signature, then exercise successful and missing-asset paths with a valid bundle.
4. Record every Harmony target and applied/failed class on exact 2.55. Audit startup and critical UI paths before packet/DLC work.
5. Verify packet readers/writers and registration by subsystem; save identity/GUID stability; containers and inventories; travel and authority transfer; DLC late join and persistence.
6. Implement isolated in-game host/client automation per docs/DEVELOPMENT.md. No multiplayer QA has run; QA remains untested.

## Sources

- https://github.com/cranebeaker/coop-anything/tree/bffd56f00bdd6063db13fa59c18a74678b121ca0
- https://api.github.com/repos/DoperDodge/SkyCoop-Upadated/git/trees/0527724cdda3ed85a2e365c462a5ff152ca06e43?recursive=1
- https://github.com/DoperDodge/SkyCoop-Upadated (default branch BeforeTFTFT)
- https://api.github.com/repos/DoperDodge/SkyCoop-Upadated/releases
- Local file/line evidence in BUGS.md corresponds to the verified snapshot.
