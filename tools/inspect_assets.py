"""Inspect the pinned official legacy ZIP and nested component without executing it."""
import argparse
from collections import Counter
import hashlib
import io
import json
from pathlib import Path
import struct
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1]
URL = 'https://github.com/Filigrani/SkyCoop/releases/download/v13.2/SkyCoopRelease.zip'
SHA = '6fee0723ebd72af22c5362aa7de966881875962305599dc38f321118e0b661b2'


def bundle_identity(data):
    if not data.startswith(b'UnityFS\0'):
        raise ValueError('Not a UnityFS bundle')
    format_version = struct.unpack_from('>I', data, 8)[0]
    generation, editor, _ = data[12:].split(b'\0', 2)
    return {'signature': 'UnityFS', 'format': format_version, 'generation': generation.decode(), 'editor': editor.decode()}


def inspect(archive):
    with archive.open('rb') as stream:
        actual = hashlib.file_digest(stream, 'sha256').hexdigest()
    if actual != SHA: raise ValueError('Official legacy ZIP hash mismatch')
    with zipfile.ZipFile(archive) as outer:
        bundle = outer.read('multiplayerstuff.unity3d')
        component = outer.read('SkyCoop.modcomponent')
    with zipfile.ZipFile(io.BytesIO(component)) as pack:
        names = pack.namelist()
        json_files = [name for name in names if name.endswith('.json')]
        for name in json_files: json.loads(pack.read(name).decode('utf-8-sig'))
        embedded = []
        for name in names:
            data = pack.read(name)
            if data.startswith(b'UnityFS\0'):
                embedded.append({'path': name, 'sha256': hashlib.sha256(data).hexdigest(), **bundle_identity(data)})
        return {
            'source': URL, 'archive_sha256': SHA,
            'multiplayer_bundle': {'sha256': hashlib.sha256(bundle).hexdigest(), **bundle_identity(bundle)},
            'component': {'sha256': hashlib.sha256(component).hexdigest(), 'files': len(names),
                          'json_files_parsed': len(json_files), 'categories': dict(Counter(n.split('/')[0] for n in names)),
                          'build_info': json.loads(pack.read('BuildInfo.json')), 'embedded_bundles': embedded,
                          'addressables_catalogs': [n for n in names if 'catalog' in n.lower()]},
            'format_blocker': 'ModComponent 7.0.0 rejects auto-mapped/scitems.unity3d; requires bundle/ and current Addressables pipeline',
            'runtime_compatibility': 'NOT TESTED; old Unity editor headers are evidence of age, not proof of load success or failure',
        }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--archive', type=Path, default=ROOT / 'local/SkyCoop-v13.2.zip')
    parser.add_argument('--output', type=Path, default=ROOT / 'artifacts/asset-audit.json')
    args = parser.parse_args()
    if not args.archive.exists():
        args.archive.parent.mkdir(parents=True, exist_ok=True)
        urllib.request.urlretrieve(URL, args.archive)
    report = inspect(args.archive)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2) + '\n')
    print('PASS: archive hashes, both UnityFS headers and component JSON inspected; runtime remains unverified')


if __name__ == '__main__':
    main()
