"""Prepare an isolated, pinned official Reborn Unity project. Never edits a game install."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess

COMMIT = '7b0616e24173950de66d99c34d2f1a871de56161'
ROOT = Path(__file__).resolve().parents[1]


def prepare(destination):
    destination = destination.resolve()
    if destination.exists(): raise RuntimeError('Destination must not exist: original source/projects are preserved')
    destination.parent.mkdir(parents=True, exist_ok=True)
    subprocess.run(['git', 'init', str(destination)], check=True)
    subprocess.run(['git', '-C', str(destination), 'remote', 'add', 'origin', 'https://github.com/Filigrani/SkyCoop.git'], check=True)
    subprocess.run(['git', '-C', str(destination), 'sparse-checkout', 'set', 'SkyCoopBundles'], check=True)
    subprocess.run(['git', '-C', str(destination), 'fetch', '--depth=1', 'origin', COMMIT], check=True)
    subprocess.run(['git', '-C', str(destination), 'checkout', '--detach', 'FETCH_HEAD'], check=True)
    actual = subprocess.check_output(['git', '-C', str(destination), 'rev-parse', 'HEAD'], text=True).strip()
    if actual != COMMIT: raise RuntimeError('Unexpected source revision')
    project = destination / 'SkyCoopBundles'
    version = (project / 'ProjectSettings/ProjectVersion.txt').read_text()
    if 'm_EditorVersion: 6000.0.60f1\n' not in version: raise RuntimeError('Unexpected source editor version')
    manifest = project / 'Packages/manifest.json'
    packages = json.loads(manifest.read_text())
    packages['dependencies'].update({
        'com.unity.addressables': '2.7.6',
        'com.deadman.modcomponent.sdk': 'https://github.com/dommrogers/ModComponentSDK.git#f2f7a81a7c5cc3699a655b113559388bbdd601a1',
        'com.unity.assetbundlebrowser': 'https://github.com/Unity-Technologies/AssetBundles-Browser.git#c7923746fa5e95916bf1a4087eceadb72305d5b5',
    })
    manifest.write_text(json.dumps(packages, indent=2) + '\n')
    editor = project / 'Assets/Editor/SkyCoopCharacterBuild.cs'
    editor.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(ROOT / 'tools/unity/SkyCoopCharacterBuild.cs', editor)
    provenance = {'repository': 'https://github.com/Filigrani/SkyCoop', 'commit': COMMIT,
                  'editor': '6000.0.60f1', 'builder_sha256': hashlib.sha256(editor.read_bytes()).hexdigest(),
                  'package_changes': packages['dependencies'], 'asset_redistribution_permission': 'NOT ESTABLISHED; local build only'}
    (project / 'skycoop-source.json').write_text(json.dumps(provenance, indent=2) + '\n')
    print('Prepared:', project)
    print('Windows Unity Editor and license required. Run reviewed builder; no assets have been built yet.')
    return project


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--destination', type=Path, default=ROOT / 'local/reborn-source')
    prepare(parser.parse_args().destination)
