"""Read-only development preflight; this is not game or multiplayer QA."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
FRAMEWORK = Path(os.environ.get('COOP_ANYTHING_ROOT', ROOT.parent / 'coop-anything'))


def main():
    project = ET.parse(ROOT / 'SkyCoop.csproj').getroot()
    packages = [p.attrib['Include'] for p in project.iter('PackageReference')]
    assert not any('coop-anything' in p.lower() for p in packages)
    requests = [
        {'jsonrpc': '2.0', 'id': 1, 'method': 'initialize', 'params': {'protocolVersion': '2025-06-18'}},
        {'jsonrpc': '2.0', 'method': 'notifications/initialized'},
        {'jsonrpc': '2.0', 'id': 2, 'method': 'tools/list'},
        {'jsonrpc': '2.0', 'id': 3, 'method': 'tools/call', 'params': {'name': 'search_directory', 'arguments': {'query': 'The Long Dark'}}},
        {'jsonrpc': '2.0', 'id': 4, 'method': 'tools/call', 'params': {'name': 'get_skill', 'arguments': {'name': 'coop-revive'}}},
    ]
    proc = subprocess.run([sys.executable, str(FRAMEWORK / 'tools/mcp/coop_mcp.py')],
                          input=''.join(json.dumps(r) + '\n' for r in requests),
                          text=True, capture_output=True, timeout=20, check=True)
    replies = {r['id']: r for r in map(json.loads, proc.stdout.splitlines())}
    assert set(replies) == {1, 2, 3, 4}, replies
    assert all('error' not in r and not r['result'].get('isError') for r in replies.values()), replies
    assert len(replies[2]['result']['tools']) == 8
    entries = json.loads(replies[3]['result']['content'][0]['text'])
    assert entries['count'] >= 1
    assert 'Revive an existing co-op mod' in replies[4]['result']['content'][0]['text']
    print('PASS: MCP initialize, tools/list (8), directory search, revive skill; no framework PackageReference')
    print('Target:', next(p.text for p in project.iter('TLDVersion')))
    print('SDK:', shutil.which('dotnet') or (str(ROOT.parent / '.dotnet/dotnet') if (ROOT.parent / '.dotnet/dotnet').is_file() else 'BLOCKED: dotnet not installed'))
    print('Game QA: NOT RUN (requires isolated 2.55 host and real client)')


if __name__ == '__main__':
    main()
