"""Rebuild from identical inputs in another path and compare shipped DLL bytes."""
import hashlib
from pathlib import Path
import shutil
import subprocess
import tempfile

from run_tests import dotnet_path

ROOT = Path(__file__).resolve().parents[1]


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    sdk = dotnet_path()
    with tempfile.TemporaryDirectory(prefix='skycoop-repro-', dir=ROOT.parent) as folder:
        target = Path(folder)
        inputs = list(ROOT.glob('*.cs')) + [ROOT / name for name in (
            'SkyCoop.csproj', 'global.json', 'NuGet.Config', 'packages.lock.json')]
        for subdir in ('Properties', 'TinyJSON'):
            inputs.extend((ROOT / subdir).rglob('*.cs'))
        for path in inputs:
            destination = target / path.relative_to(ROOT)
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(path, destination)
        subprocess.run([sdk, 'restore', 'SkyCoop.csproj', '--locked-mode'], cwd=target, check=True)
        subprocess.run([sdk, 'build', 'SkyCoop.csproj', '-c', 'Release', '--no-restore',
                        '-p:ContinuousIntegrationBuild=true', '-v:q'], cwd=target, check=True)
        for filename in ('SkyCoop.dll', 'SkyCoop.pdb', 'Steamworks.NET.dll'):
            original = digest(ROOT / 'bin/Release' / filename)
            rebuilt = digest(target / 'bin/Release' / filename)
            if original != rebuilt:
                raise SystemExit('FAIL: non-reproducible ' + filename + ': ' + original + ' != ' + rebuilt)
            print('PASS: identical ' + filename + ' SHA-256 across independent source directories: ' + original)


if __name__ == '__main__':
    main()
