"""Resolve every compiled AssemblyRef without loading/executing game or loader code.

The release loader is inspected, not installed. Game providers are compile snapshots;
this is not verification of a user's regenerated interop files or native game runtime.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import urllib.request
import zipfile

from run_tests import dotnet_path

ROOT = Path(__file__).resolve().parents[1]
LOADER_URL = 'https://github.com/LavaGang/MelonLoader/releases/download/v0.7.3/MelonLoader.x64.zip'
LOADER_SHA = '5b2b2f3d1cd42b59ec886c5bdc2663edae87a0097a4f4a8f58c0965a99dda416'


def inspect(paths, sdk):
    command = [sdk, str(ROOT / 'tests/RuntimeAudit/bin/Release/net8.0/RuntimeAudit.dll')]
    result = subprocess.run(command + list(map(str, paths)), check=True, text=True, capture_output=True)
    return [json.loads(line) for line in result.stdout.splitlines()]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path, default=ROOT / 'artifacts/runtime-audit.json')
    args = parser.parse_args()
    sdk = dotnet_path()
    local = ROOT / 'local'
    local.mkdir(exist_ok=True)
    archive = local / 'MelonLoader-0.7.3-x64.zip'
    if not archive.exists():
        urllib.request.urlretrieve(LOADER_URL, archive)
    with archive.open('rb') as stream:
        actual = hashlib.file_digest(stream, 'sha256').hexdigest()
    if actual != LOADER_SHA:
        raise SystemExit('FAIL: loader archive SHA-256 mismatch')
    directory = local / 'loader-0.7.3'
    # Extract only the verified archive into our own analysis directory.
    with zipfile.ZipFile(archive) as bundle:
        for entry in bundle.infolist():
            target = (directory / entry.filename).resolve()
            if not target.is_relative_to(directory.resolve()):
                raise SystemExit('Unsafe archive entry')
        bundle.extractall(directory)
    subprocess.run([sdk, 'build', str(ROOT / 'tests/RuntimeAudit/RuntimeAudit.csproj'), '-c', 'Release',
                    '-p:RestoreLockedMode=true', '-v:q'], cwd=ROOT, check=True)
    nuget = Path(os.environ.get('NUGET_PACKAGES', Path.home() / '.nuget/packages'))
    game = nuget / 'stblade.modding.tld.il2cppassemblies.windows/2.55.0/lib/net6.0'
    if not game.is_dir():
        raise SystemExit('TLD 2.55.0 compile snapshot not found in NUGET_PACKAGES')
    providers = {}
    for folder, origin in ((game, 'TLD-2.55.0-compile-snapshot'),
                           (directory / 'MelonLoader/net6', 'official-MelonLoader-0.7.3'),
                           (ROOT / 'bin/Release', 'shipped-mod')):
        paths = sorted(folder.glob('*.dll'))
        for path, metadata in zip(paths, inspect(paths, sdk)):
            if metadata['name'] == 'SkyCoop': continue
            providers[metadata['name']] = (origin, path, metadata)
    mod = inspect([ROOT / 'bin/Release/SkyCoop.dll'], sdk)[0]
    records = []
    unresolved = []
    missing_types = []
    for reference in mod['references']:
        name = reference['name']
        record = dict(reference)
        if name.startswith('System.') or name in ('netstandard', 'mscorlib'):
            record.update(provider='.NET-6-Desktop-Runtime-x64-external', status='framework-reference; installed-runtime-not-checked')
        elif name in providers:
            origin, path, metadata = providers[name]
            required = tuple(map(int, reference['version'].split('.')))
            found = tuple(map(int, metadata['version'].split('.')))
            record.update(provider=origin, provided_version=metadata['version'], sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                          status='static-assembly-reference-resolved' if found >= required else 'version-too-old')
            if found < required: unresolved.append(name)
        else:
            record.update(provider=None, status='unresolved')
            unresolved.append(name)
        records.append(record)
    for reference in mod['type_references']:
        name = reference['assembly']
        if name in providers and reference['name'] not in providers[name][2]['types']:
            missing_types.append(reference)
    report = {
        'loader_version': '0.7.3', 'loader_archive_sha256': LOADER_SHA,
        'compile_loader_version': next(r['version'] for r in mod['references'] if r['name'] == 'MelonLoader'),
        'game_version': '2.55.0', 'assembly_references': records, 'unresolved': unresolved, 'missing_direct_types': missing_types,
        'verification': 'static AssemblyRef name/version and direct TypeRef existence only; not method signatures, API behavior, native ABI, installed runtime or game QA',
        'multiplayer_tested': False,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2) + '\n')
    if unresolved: raise SystemExit('FAIL: unresolved references: ' + ', '.join(unresolved))
    if missing_types: raise SystemExit('FAIL: missing direct type references: ' + str(missing_types))
    print('PASS: every non-framework AssemblyRef resolves against the pinned loader, compile snapshot or shipped mod binding')
    print('Game-generated interop, native ABI and two-client runtime behavior remain untested')


if __name__ == '__main__':
    main()
