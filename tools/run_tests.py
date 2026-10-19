"""Run managed regressions, optionally prove SC-001's regression detects the original bug."""
import argparse
import os
from pathlib import Path
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]


def dotnet_path():
    path = os.environ.get('DOTNET_EXE') or shutil.which('dotnet')
    if not path:
        path = str(ROOT.parent / '.dotnet/dotnet')
    if not Path(path).is_file():
        raise SystemExit('Pinned .NET SDK not found; see docs/BUILD.md')
    return path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--mutation-check', action='store_true')
    args = parser.parse_args()
    sdk = dotnet_path()
    subprocess.run([sys.executable, str(ROOT / 'tools/prepare_regression.py')], check=True)
    project = ROOT / 'tests/Regression/Regression.csproj'
    subprocess.run([sdk, 'restore', str(project), '--locked-mode'], cwd=ROOT, check=True)
    subprocess.run([sdk, 'run', '--project', str(project), '-c', 'Release', '--no-restore'], cwd=ROOT, check=True, timeout=60)
    if not args.mutation_check:
        return
    generated = ROOT / 'tests/Regression/obj/BundleAssetUnderTest.g.cs'
    original = generated.read_text()
    replacement = original.replace('LoadedBundle.LoadAsset<T>(name)', 'BundleAsset<T>(name)')
    if replacement == original:
        raise SystemExit('Mutation cannot be applied; review test extraction')
    local = ROOT / 'local'
    local.mkdir(exist_ok=True)
    try:
        generated.write_text(replacement)
        subprocess.run([sdk, 'build', str(project), '-c', 'Release', '--no-restore', '-v:q'], cwd=ROOT, check=True)
        environment = dict(os.environ, DOTNET_DbgEnableMiniDump='0')
        with (local / 'mutation.log').open('w') as log:
            try:
                result = subprocess.run([sdk, str(ROOT / 'tests/Regression/bin/Release/net8.0/Regression.dll')],
                                        cwd=ROOT, env=environment, stdout=log, stderr=log, timeout=15)
                detected = result.returncode != 0
            except subprocess.TimeoutExpired:
                detected = True
        if not detected:
            raise SystemExit('FAIL: regression suite accepted original infinite recursion')
        print('PASS: SC-001 mutation detected (original recursive method fails in child process)')
    finally:
        generated.write_text(original)
        subprocess.run([sdk, 'build', str(project), '-c', 'Release', '--no-restore', '-v:q'], cwd=ROOT, check=True)


if __name__ == '__main__':
    main()
