# Development environment

Updated 2026-10-19. This is a real clone of **ko4bratan/SkyCoop-Upadated**, with upstream **DoperDodge/SkyCoop-Upadated**. Branch `port/tld-2.55-audit` starts at commit `0527724cdda3ed85a2e365c462a5ff152ca06e43` (tree `5d9de4ef29844121aad8b036e50edaa6a12ffe3f`). Existing branches `BeforeTFTFT` and `claude/long-dark-multiplayer-compat-c7d6qo` were inspected; their commit IDs differ but their source trees are identical. They are preserved.

The previous setup and audit commit was transferred as a patch onto real upstream history. Its synthetic snapshot baseline was not merged. The old workspace remains archived as SkyCoop-snapshot, including private session notes. No game installation or saves were modified.

GitHub connector reports repository push permission. Shell gh authentication still reports an invalid GH_TOKEN; use the authorized connector for writes if direct git push cannot authenticate. Repository access and source writes are distinct from permission to edit workflows; an Actions rejection must be reported explicitly.

Co-op Anything is an external development framework, pinned at commit `bffd56f00bdd6063db13fa59c18a74678b121ca0`. Its snapshot and 105 verified source/text files remain in the sibling coop-anything directory. Do not add it to the runtime project or ZIP. Framework workflow: triage/research -> revive -> QA. Local MCP configs and six skill links are preserved. MCP was tested through real stdio JSON-RPC, not assumed registered in this running session.

Python 3 and PyYAML 6.0.3 are available. .NET SDK 8.0.426 is installed into a workspace-local directory with verified Microsoft SHA-512 and committed SHA-256. See [BUILD.md](BUILD.md) for exact bootstrap, locked dependency restore, compilation and test commands. No SDK is bundled with SkyCoop. The mod still targets net6.0 and TLD interop 2.55.0.

Ordinary sandboxed shell networking cannot reach the proxy; additional network permission enables GitHub/Microsoft/NuGet access. The initial dotnetcli.blob.core.windows.net request was denied; the documented builds.dotnet.microsoft.com endpoint works through the configured proxy. No direct-route or proxy bypass was used. .NET build commands require approved execution outside the sandbox because named mutex creation is blocked inside it.

Private install identities, executable hashes, logs and saves belong in ignored local/. No game installation, executable identity or required asset bundle/component was provided. README claims about Unity 6000.0.60f1 and MelonLoader 0.7.3 remain unverified; package resolution confirms a 0.7.1 compile reference. Compilation is possible without the game, real multiplayer testing is not.

The current QA boundary and pending isolated two-client gates are in [TWO-PLAYER-TEST.md](TWO-PLAYER-TEST.md). Never upgrade the game QA label based on managed tests or a working menu alone.
