"""Read serialized legacy assets; no Unity execution and no model/audio export."""
import argparse
from collections import Counter
import hashlib
import io
import json
from pathlib import Path
import zipfile
import UnityPy
from inspect_assets import SHA, ROOT


def inventory(data):
    environment = UnityPy.load(data)
    scripts = []
    avatars = []
    for obj in environment.objects:
        if obj.type.name == 'MonoScript':
            script = obj.read()
            scripts.append({'name': script.m_ClassName, 'assembly': script.m_AssemblyName})
    for name, pointer in environment.container.items():
        if name.lower().endswith('/multiplayerplayer.prefab'):
            player = pointer.read()
            transform = next(c.component.read() for c in player.m_Component if c.component.type.name == 'Transform')
            avatars.append({'asset': name, 'root_children': [c.read().m_GameObject.read().m_Name for c in transform.m_Children]})
    return {'sha256': hashlib.sha256(data).hexdigest(), 'objects': len(environment.objects),
            'types': dict(sorted(Counter(o.type.name for o in environment.objects).items())),
            'container': sorted(environment.container), 'mono_scripts': sorted(scripts, key=lambda s: s['name']),
            'legacy_avatar': avatars}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--archive', type=Path, default=ROOT / 'local/SkyCoop-v13.2.zip')
    parser.add_argument('--output', type=Path, default=ROOT / 'artifacts/bundle-inventory.json')
    args = parser.parse_args()
    if hashlib.sha256(args.archive.read_bytes()).hexdigest() != SHA:
        raise ValueError('Official archive hash mismatch')
    with zipfile.ZipFile(args.archive) as archive:
        bundle = inventory(archive.read('multiplayerstuff.unity3d'))
        with zipfile.ZipFile(io.BytesIO(archive.read('SkyCoop.modcomponent'))) as component:
            item_bundle = inventory(component.read('auto-mapped/scitems.unity3d'))
    report = {'parser': 'UnityPy 1.23.0; pinned Linux/Python 3.12 wheel hashes', 'outer': bundle, 'items': item_bundle,
              'unity_execution': False, 'runtime_compatibility': 'NOT TESTED; successful deserialization does not prove Unity 6000 or native shader compatibility'}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2) + '\n')
    print(f"PASS: inventoried {bundle['objects']} outer objects, {len(bundle['container'])} assets; {len(item_bundle['container'])} item assets; NO runtime test")


if __name__ == '__main__': main()
