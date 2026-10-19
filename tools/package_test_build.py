"""Package only redistributable mod outputs, never game/interop assemblies."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[1]


def package(output):
    files = {
        'STEAM-2.55-RU.md': ROOT / 'docs/STEAM-2.55-RU.md',
        'Открыть-SkyCoop.cmd': ROOT / 'packaging/Открыть-SkyCoop.cmd',
        'SkyCoop-Launcher.ps1': ROOT / 'packaging/SkyCoop-Launcher.ps1',
        'Launcher-Helpers.ps1': ROOT / 'packaging/Launcher-Helpers.ps1',
        'LAUNCHER-RU.txt': ROOT / 'docs/LAUNCHER-RU.txt',
        'Mods/SkyCoop.dll': ROOT / 'bin/Release/SkyCoop.dll',
        'Mods/SkyCoop.pdb': ROOT / 'bin/Release/SkyCoop.pdb',
        'UserLibs/Steamworks.NET.dll': ROOT / 'bin/Release/Steamworks.NET.dll',
        'REBORN-CHARACTER.md': ROOT / 'docs/REBORN-CHARACTER.md',
        'Start-MinimalSurvival.ps1': ROOT / 'packaging/Start-MinimalSurvival.ps1',
        'tools/prepare_reborn_project.py': ROOT / 'tools/prepare_reborn_project.py',
        'tools/unity/SkyCoopCharacterBuild.cs': ROOT / 'tools/unity/SkyCoopCharacterBuild.cs',
        'bundle-inventory.json': ROOT / 'artifacts/bundle-inventory.json',
        'TWO-PLAYER-TEST.md': ROOT / 'docs/TWO-PLAYER-TEST.md',
        'INSTALL.md': ROOT / 'docs/INSTALL.md',
        'DEPENDENCIES.md': ROOT / 'docs/RUNTIME-DEPENDENCIES.md',
        'ASSET-COMPATIBILITY.md': ROOT / 'docs/ASSET-PROVENANCE.md',
        'DIAGNOSTICS.md': ROOT / 'docs/DIAGNOSTICS.md',
        'runtime-audit.json': ROOT / 'artifacts/runtime-audit.json',
        'asset-audit.json': ROOT / 'artifacts/asset-audit.json',
        'Install-Experimental.ps1': ROOT / 'packaging/Install-Experimental.ps1',
        'Test-Installation.ps1': ROOT / 'packaging/Test-Installation.ps1',
        'Get-ExternalDependencies.ps1': ROOT / 'packaging/Get-ExternalDependencies.ps1',
        'external-dependencies.json': ROOT / 'packaging/external-dependencies.json',
        'LICENSE.txt': ROOT / 'LICENSE',
        'licenses/Steamworks.NET-LICENSE.txt': ROOT / 'third_party/Steamworks.NET-LICENSE.txt',
    }
    missing = [str(path.relative_to(ROOT)) for path in files.values() if not path.is_file()]
    if missing:
        raise RuntimeError('Required build outputs missing: ' + ', '.join(missing))
    payloads = {name: path.read_bytes() for name, path in files.items()}
    commit = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
    dirty = bool(subprocess.check_output(['git', 'status', '--porcelain', '--untracked-files=normal'], cwd=ROOT, text=True))
    manifest = {
        'target_game': 'The Long Dark 2.55',
        'platform': 'Windows x64',
        'source_commit': commit,
        'source_dirty': dirty,
        'qa': 'compile-verified; managed regressions only; two-player game QA NOT RUN',
        'runtime_ready': False,
        'required_external_assets': [],
        'default_test_mode': 'native main menu: Steam lobby/P2P or UDP; transport/remote-render probe only',
        'optional_character_assets': ['Mods/SkyCoopResources/skycoop-character', 'Mods/SkyCoopResources/character.json'],
        'character_assets_built': False,
        'character_assets_redistributed': False,
        'legacy_full_mode_assets': ['Mods/multiplayerstuff.unity3d', 'Mods/SkyCoop.modcomponent'],
        'loader_pin': '0.7.3 Windows x64; exact experimental candidate, not runtime-certified',
        'blockers': ['No real two-client game QA', 'Native game UI and Steam ABI/overlay/relay not tested', 'Character Unity export NOT RUN; actual game Unity version unknown', 'Full shared-world mode still needs item-pack migration'],
        'prerequisites': ['The Long Dark 2.55', '.NET 6 Desktop Runtime x64', 'MelonLoader 0.7.3 x64', 'For full legacy mode only: migrated assets and ModComponent stack'],
        'files': {name: hashlib.sha256(data).hexdigest() for name, data in sorted(payloads.items())},
    }
    payloads['manifest.json'] = (json.dumps(manifest, indent=2, sort_keys=True) + '\n').encode()
    payloads['SHA256SUMS'] = ''.join(hashlib.sha256(data).hexdigest() + '  ' + name + '\n'
                                       for name, data in sorted(payloads.items())).encode()
    output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(output, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for name, data in sorted(payloads.items()):
            entry = zipfile.ZipInfo(name, date_time=(2026, 1, 1, 0, 0, 0))
            entry.compress_type = zipfile.ZIP_DEFLATED
            entry.external_attr = 0o100644 << 16
            archive.writestr(entry, data)
    digest = hashlib.sha256(output.read_bytes()).hexdigest()
    output.with_suffix(output.suffix + '.sha256').write_text(digest + '  ' + output.name + '\n')
    print(str(output) + '\nSHA-256: ' + digest)
    print('Experimental render probe kit: character export NOT RUN, marker fallback; full mode BLOCKED; game QA NOT RUN')
    return manifest


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path, default=ROOT / 'artifacts/SkyCoop-2.55-experimental-install.zip')
    args = parser.parse_args()
    package(args.output.resolve())


if __name__ == '__main__':
    main()
