"""Extract the real BundleAsset method for isolated managed behavior checks.

The game/Unity native runtime is replaced by a controlled AssetBundle facade.
This tests the production method body, not native bundle compatibility.
"""
from pathlib import Path
import argparse


def extract(source):
    start = source.index('public static T BundleAsset<T>')
    opening = source.index('{', start)
    depth = 1
    end = opening + 1
    while depth:
        if source[end] == '{':
            depth += 1
        elif source[end] == '}':
            depth -= 1
        end += 1
    return source[start:end]


def main():
    root = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser()
    parser.add_argument('--source', type=Path, default=root / 'MyModMain.cs')
    args = parser.parse_args()
    method = extract(args.source.read_text(encoding='utf-8-sig'))
    code = '''using System.Collections.Generic;
namespace SkyCoop {
public static class MyMod {
public static FakeBundle LoadedBundle;
public static readonly HashSet<string> ReportedMissingAssets = new();
''' + method + '\n}}\n'
    output = root / 'tests/Regression/obj/BundleAssetUnderTest.g.cs'
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(code)
    print('Prepared production BundleAsset<T> body for isolated regression tests')


if __name__ == '__main__':
    main()
