#if !DEDICATED
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using UnityEngine;

namespace SkyCoop
{
    // UI/avatar AssetBundles and ModComponent Addressables item packs are distinct inputs.
    public static class AssetResources
    {
        public static AssetBundle Load()
        {
            string file = "multiplayerstuff.unity3d";
            string config = Path.Combine("UserData", "SkyCoop", "resources.json");
            if (File.Exists(config))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(config));
                if (document.RootElement.GetProperty("schema").GetInt32() != 1)
                    throw new InvalidDataException("Unknown resource manifest schema");
                file = document.RootElement.GetProperty("bundle").GetString();
                if (string.IsNullOrWhiteSpace(file) || file != Path.GetFileName(file) || file.Contains('\\') || file.Contains('/') || file == "." || file == ".." || file.EndsWith(".modcomponent", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("bundle must be a filename under Mods, not a ModComponent item pack");
            }
            string path = Path.Combine("Mods", file);
            if (!File.Exists(path)) { DiagnosticLog.Failure("asset.bundle-missing", path); return null; }
            var bundle = AssetBundle.LoadFromFile(path);
            DiagnosticLog.Report("resources.json", new { bundle = file, loaded = bundle != null,
                assets = bundle == null ? Array.Empty<string>() : bundle.GetAllAssetNames().ToArray(),
                nativeCompatibilityTested = false, itemPackLoadedBy = "external ModComponent Addressables loader" });
            return bundle;
        }

        public static bool LegacyAvatar(GameObject prefab)
        {
            if (prefab == null) return false;
            string[] roots = { "Clothing", "Body", "Extras", "hips", "Speaker" };
            bool valid = prefab.GetComponent<Animator>() != null && prefab.transform.childCount >= roots.Length;
            for (int i = 0; valid && i < roots.Length; i++) valid &= prefab.transform.GetChild(i).name == roots[i];
            int[][] required = {
                new[] {4,0}, new[] {4,1}, new[] {4,2}, new[] {4,3},
                new[] {3,8,0,0,0,1,0,0,0,8,1}, new[] {3,8,0,0,0,2,0,0},
                new[] {3,8,0,0,0,0,0,0}, new[] {3,8,0,0,0,1,0,0}, new[] {3,8,1}, new[] {3,8,2}
            };
            foreach (int[] path in required)
            {
                Transform current = prefab.transform;
                foreach (int index in path)
                {
                    if (current.childCount <= index) { valid = false; break; }
                    current = current.GetChild(index);
                }
            }
            if (!valid) DiagnosticLog.Failure("asset.avatar-contract", "Prefab rejected before attaching legacy components: requires original rig/voice/torch/camera hierarchy. Reborn prefab is not a drop-in replacement.");
            return valid;
        }
    }
}
#endif
