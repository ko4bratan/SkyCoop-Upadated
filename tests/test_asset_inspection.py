import struct
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'tools'))
from inspect_assets import bundle_identity, inspect


class AssetInspectionTests(unittest.TestCase):
    def test_unity_header_identity_without_loading_native_code(self):
        # Explicit concatenation avoids interpreting the separator as an octal escape.
        header = b'UnityFS\0' + struct.pack('>I', 6) + b'5.x.x\0' + b'2018.4.2f1\0' + b'payload'
        self.assertEqual(bundle_identity(header)['editor'], '2018.4.2f1')
        with self.assertRaises(ValueError):
            bundle_identity(b'not a Unity bundle')

    def test_unpinned_archive_is_rejected_before_parsing(self):
        with tempfile.TemporaryDirectory() as directory:
            file = Path(directory) / 'fake.zip'
            file.write_bytes(b'wrong archive')
            with self.assertRaisesRegex(ValueError, 'hash mismatch'):
                inspect(file)

    def test_actual_component_has_second_bundle_and_format_blocker(self):
        report = inspect(ROOT / 'local/SkyCoop-v13.2.zip')
        self.assertEqual(report['multiplayer_bundle']['editor'], '2018.4.21f1')
        component = report['component']
        self.assertEqual(component['json_files_parsed'], 26)
        self.assertEqual(component['embedded_bundles'][0]['path'], 'auto-mapped/scitems.unity3d')
        self.assertEqual(component['embedded_bundles'][0]['editor'], '2018.4.2f1')
        self.assertEqual(component['addressables_catalogs'], [])


if __name__ == '__main__':
    unittest.main()
