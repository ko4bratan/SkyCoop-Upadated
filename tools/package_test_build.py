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
        'Mods/SkyCoop.dll': ROOT / 'bin/Release/SkyCoop.dll',
        'UserLibs/Steamworks.NET.dll': ROOT / 'bin/Release/Steamworks.NET.dll',
        'TEST-BUILD.md': ROOT / 'docs/TWO-PLAYER-TEST.md',
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
        'required_external_assets': ['Mods/multiplayerstuff.unity3d', 'Mods/SkyCoop.modcomponent'],
        'prerequisites': ['The Long Dark 2.55', 'MelonLoader 0.7.x (actual package references 0.7.1)', 'ModComponent for SkyCoop.modcomponent'],
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
    print('Incomplete runtime kit: external asset bundle/component required; game multiplayer QA NOT RUN')
    return manifest


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path, default=ROOT / 'artifacts/SkyCoop-2.55-two-player-test.zip')
    args = parser.parse_args()
    package(args.output.resolve())


if __name__ == '__main__':
    main()
