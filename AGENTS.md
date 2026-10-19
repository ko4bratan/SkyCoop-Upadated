# SkyCoop development instructions

Target: The Long Dark 2.55. Use Co-op Anything as an external development playbook and read-only analysis tool only. Never add it to SkyCoop.csproj, runtime assemblies, asset bundles, installers or release packages.

Framework checkout/snapshot: ../coop-anything. Read its AGENTS.md, then .agents/skills/coop-revive/SKILL.md and playbook/reviving-abandoned-mods.md. Workflow: coop-triage -> coop-research -> coop-revive -> coop-qa; use coop-reverse-engineer only for unknown game API details. The selected upstream already claims a 2.55 port: audit that implementation before proposing a rewrite. The console/decomp-port workflow is not appropriate.

The user authorized repository downloads, local tool setup, building/testing the port, and transferring changes to ko4bratan/SkyCoop-Upadated on a separate development branch. Do not ask for those permissions again. The user also authorized publishing the experimental GitHub Actions artifact. Publishing issues, messages, PRs or releases still requires separate authorization. No claim issue or framework contribution is authorized by this setup task.

Read docs/DEVELOPMENT.md, docs/AUDIT-2.55.md and BUGS.md, then local/ at each session start. Keep private game identities, install paths, logs and saves in ignored local/. Never modify the user's game installation or saves. Use isolated test copies.

Run python3 tools/dev_check.py before work. It checks MCP transport, documentation access and source metadata; it does not prove multiplayer works. Run dotnet build SkyCoop.csproj -c Release when the SDK and NuGet access are available. Do not call Dedicated configurations standalone supported builds: the separate server project is absent.

Audit by subsystem and verify each finding before changing runtime code. Record source locations, trigger, confidence and verification status in BUGS.md. Treat comments and README claims as unverified until tested. Keep observed, derived, suspected and unknown results distinct. Runtime changes need a focused repro and a meaningful check, then isolated host/client QA. Skipped Harmony patches must be investigated individually; a successful load is not proof of compatibility.

This is now a real clone of ko4bratan/SkyCoop-Upadated. Branch port/tld-2.55-audit starts at upstream commit 0527724cdda3ed85a2e365c462a5ff152ca06e43. Prior setup/audit work was transferred as a patch; synthetic snapshot history was not imported. Preserve the existing BeforeTFTFT and claude/long-dark-multiplayer-compat-c7d6qo branches. Original snapshot work remains archived separately in the cloud workspace. See docs/BUILD.md for locked restore, regression and reproducibility commands. Tests and tools are not runtime dependencies.
