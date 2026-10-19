# Official legacy assets inspected

Source: https://github.com/Filigrani/SkyCoop/releases/download/v13.2/SkyCoopRelease.zip

Release date: 2024-08-31. Archive inspected in ignored local/; no downloaded code was executed. SHA-256:

```
6fee0723ebd72af22c5362aa7de966881875962305599dc38f321118e0b661b2  SkyCoopRelease.zip
d1ea1fe36b248609ee7937f2477597b9e6a279f808f16e39f1793d81bc318399  multiplayerstuff.unity3d
0083a24f42658c1e545c411b8c9f6d287ae491e7c9d80992cb591cbef601e369  SkyCoop.modcomponent
```

The bundle's UnityFS header identifies **Unity 2018.4.21f1**. The port README claims The Long Dark 2.55 uses Unity 6000.0.60f1; actual game identity is still unavailable here. The header mismatch alone is not a live compatibility test, and the legacy bundle is **not validated for 2.55**. No matching Unity asset project or rebuilt bundle has been found in the selected fork. Third-party asset redistribution rights also need verification independently of the source MIT license.

The legacy archive stays outside Git and outside the prepared test ZIP. Do not install its old SkyCoop.dll over this build. A controlled test with a verified 2.55 installation is required to determine whether legacy assets load; if they fail, the asset project or a compatible rebuild is needed. Do not change a bundle's version header and claim it was rebuilt.

## Nested item-pack audit

SkyCoop.modcomponent is a ZIP with **32 members**, including **26 parseable JSON files**, two gear-spawn text files and the embedded `auto-mapped/scitems.unity3d`. Its BuildInfo identifies **Sky Co-op LTS 0.12.2** even though the containing release is v13.2. The embedded bundle is UnityFS format 6, editor **2018.4.2f1**, SHA-256 `0d05de2194a3eec00e685cc2af66a365580e44db36587176647376af359214af`. No Addressables catalog is present.

There is a confirmed **format incompatibility with ModComponent 7.0.0**: its [ZipFileLoader.TryHandleUnity3d](https://github.com/dommrogers/ModComponent/blob/7.0.0/ModComponent/Mapper/ZipFileLoader.cs) only accepts `bundle/` entries. For the old path it calls SetItemPackNotWorking with “Asset bundle not in the bundle folder” and returns false. Its [AssetBundleProcessor](https://github.com/dommrogers/ModComponent/blob/7.0.0/ModComponent/Utils/AssetBundleProcessor.cs) loads Addressables catalogs. This is a concrete blocker independently of whether Unity can read the old serialized bundle. Moving one entry does not recreate the required catalogs or validate item registration.

ModComponent 7.0.0 itself targets TLD2.51; its ModSettings1.9.0 dependency targets an older game/loader. Neither is certified for2.55. Obsolete ds5678 net472/Unhollower releases are not a valid substitute for this net6 port. The experimental package therefore provides verified official download instructions and blocks any claim that this item-pack stack is ready.

`python3 tools/inspect_assets.py` reproduces the archive/hash/header/JSON inspection and emits asset-audit.json, included in the installation ZIP. This script does not execute game code or perform native Unity load validation. Required real-game resource gates: outer bundle loads with0.7.3, all needed UI/player prefabs resolve, materials/shaders/animations and MonoBehaviour scripts bind, migrated catalogs/item definitions register, and both seats agree on item IDs and serialized state.
