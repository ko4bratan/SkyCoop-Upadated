"""Validate the actual ZIP: identities, dependency bytes, hashes, no game/framework files."""
import hashlib
import json
from pathlib import Path
import tempfile
import zipfile

import package_test_build as builder

ROOT = Path(__file__).resolve().parents[1]


def main():
    with tempfile.TemporaryDirectory() as folder:
        folder = Path(folder)
        first, second = folder / 'first.zip', folder / 'second.zip'
        builder.package(first)
        builder.package(second)
        assert first.read_bytes() == second.read_bytes(), 'ZIP is not deterministic'
        with zipfile.ZipFile(first) as archive:
            names = set(archive.namelist())
            expected = {'STEAM-2.55-RU.md', 'Открыть-SkyCoop.cmd', 'SkyCoop-Launcher.ps1', 'Launcher-Helpers.ps1', 'LAUNCHER-RU.txt', 'REBORN-CHARACTER.md', 'Start-MinimalSurvival.ps1', 'tools/prepare_reborn_project.py', 'tools/unity/SkyCoopCharacterBuild.cs', 'bundle-inventory.json', 'Mods/SkyCoop.dll', 'UserLibs/Steamworks.NET.dll', 'Mods/SkyCoop.pdb', 'TWO-PLAYER-TEST.md',
                        'INSTALL.md', 'DEPENDENCIES.md', 'ASSET-COMPATIBILITY.md', 'DIAGNOSTICS.md',
                        'runtime-audit.json', 'asset-audit.json', 'Install-Experimental.ps1', 'Test-Installation.ps1',
                        'Get-ExternalDependencies.ps1', 'external-dependencies.json',
                        'LICENSE.txt', 'licenses/Steamworks.NET-LICENSE.txt', 'manifest.json', 'SHA256SUMS'}
            assert names == expected, names
            assert archive.read('Mods/SkyCoop.dll') == (ROOT / 'bin/Release/SkyCoop.dll').read_bytes()
            assert archive.read('UserLibs/Steamworks.NET.dll') == (ROOT / 'bin/Release/Steamworks.NET.dll').read_bytes()
            manifest = json.loads(archive.read('manifest.json'))
            assert manifest['runtime_ready'] is False
            assert manifest['required_external_assets'] == []
            assert len(manifest['legacy_full_mode_assets']) == 2
            assert manifest['character_assets_built'] is False
            assert manifest['character_assets_redistributed'] is False
            assert manifest['blockers']
            assert json.loads(archive.read('external-dependencies.json'))['playable'] is False
            for name, digest in manifest['files'].items():
                assert hashlib.sha256(archive.read(name)).hexdigest() == digest, name
            for line in archive.read('SHA256SUMS').decode().splitlines():
                digest, name = line.split('  ', 1)
                assert hashlib.sha256(archive.read(name)).hexdigest() == digest, name
        print('PASS: deterministic ZIP, exact payload/dependency bytes, hashes and explicit missing-asset status')
        original = builder.ROOT
        try:
            builder.ROOT = folder
            try:
                builder.package(folder / 'must-not-exist.zip')
            except RuntimeError:
                pass
            else:
                raise AssertionError('packager accepted missing DLLs')
            assert not (folder / 'must-not-exist.zip').exists()
            print('PASS: missing required DLL prevents package creation')
        finally:
            builder.ROOT = original


if __name__ == '__main__':
    main()
