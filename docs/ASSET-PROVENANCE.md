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
