"""Install the pinned Linux x64 SDK into a workspace-local folder; no shell installer."""
import argparse
import hashlib
import json
from pathlib import Path
import platform
import tarfile
import urllib.request


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--destination', type=Path, default=Path(__file__).resolve().parents[2] / '.dotnet')
    args = parser.parse_args()
    if platform.system() != 'Linux' or platform.machine() not in ('x86_64', 'AMD64'):
        parser.error('This bootstrap is pinned for Linux x64. On Windows install the SDK version in global.json from Microsoft.')
    pin = json.loads(Path(__file__).with_name('sdk-linux-x64.json').read_text())
    destination = args.destination.resolve()
    archive = destination.parent / ('dotnet-sdk-' + pin['version'] + '-linux-x64.tar.gz')
    if not archive.exists():
        urllib.request.urlretrieve(pin['url'], archive)
    with archive.open('rb') as stream:
        actual = hashlib.file_digest(stream, 'sha256').hexdigest()
    if actual != pin['sha256']:
        raise SystemExit('SDK SHA-256 mismatch; archive was not executed or extracted')
    destination.mkdir(parents=True, exist_ok=True)
    with tarfile.open(archive) as bundle:
        bundle.extractall(destination, filter='data')
    print('Verified SDK ' + pin['version'] + ': ' + str(destination / 'dotnet'))


if __name__ == '__main__':
    main()
