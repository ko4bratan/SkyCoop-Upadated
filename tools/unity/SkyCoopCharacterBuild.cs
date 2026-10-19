// Runs in a separate official Reborn Unity project, never in SkyCoop.dll.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

public static class SkyCoopCharacterBuild
{
    private const string Generated = "Assets/SkyCoopGenerated";
    private const string Source = "Assets/Prefabs/SkyCoopPlayer.prefab";
    public static void Build()
    {
        if (Application.unityVersion != "6000.0.60f1") throw new Exception("Reviewed profile requires Unity6000.0.60f1; verify actual game runtime before changing profile");
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-skycoopAssetOutput");
        if (index < 0 || index + 1 >= args.Length) throw new Exception("-skycoopAssetOutput <absolute output folder> required");
        string output = Path.GetFullPath(args[index + 1]); Directory.CreateDirectory(output);
        // Delete only the known generated directory in this disposable source checkout.
        if (AssetDatabase.IsValidFolder(Generated)) AssetDatabase.DeleteAsset(Generated);
        AssetDatabase.CreateFolder("Assets", "SkyCoopGenerated");
        GameObject player = PrefabUtility.LoadPrefabContents(Source);
        try
        {
            player.SetActive(false); player.name = "SkyCoopRemote";
            player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (Transform child in player.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = 0; child.gameObject.tag = "Untagged";
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
            }
            foreach (Component component in player.GetComponentsInChildren<Component>(true))
            {
                if (component is Transform || component is Animator || component is SkinnedMeshRenderer || component is MeshRenderer || component is MeshFilter) continue;
                UnityEngine.Object.DestroyImmediate(component);
            }
            // One fixed outfit; no game gear, Addressables lookups, inventory or clothing system.
            var outfit = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                "Body_mesh", "Hair_mesh", "Eyebrows_mesh", "Beard_mesh", "Dress_shirt_mesh", "cargo_pants_mesh", "work_boots_mesh"
            };
            var renderInventory = new List<string>();
            int visible = 0;
            foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>(true))
            {
                bool selected = outfit.Contains(renderer.gameObject.name);
                renderer.enabled = selected;
                if (!selected) continue;
                for (Transform t = renderer.transform; t != null && t != player.transform; t = t.parent) t.gameObject.SetActive(true);
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null || material.shader == null) throw new Exception("Missing material/shader on " + renderer.name);
                    renderInventory.Add(renderer.name + " | material=" + AssetDatabase.GetAssetPath(material) + " | shader=" + material.shader.name);
                    foreach (string property in material.GetTexturePropertyNames())
                    {
                        Texture texture = material.GetTexture(property);
                        renderInventory.Add("  " + property + "=" + (texture == null ? "<none>" : AssetDatabase.GetAssetPath(texture)));
                    }
                }
                visible++;
            }
            if (visible < 3 || !player.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(r => r.name == "Body_mesh"))
                throw new Exception("Fixed outfit contract changed; inspect imported FBX, do not export invisible character");
            File.WriteAllLines(Path.Combine(output, "character-render-inventory.txt"), renderInventory);
            Animator[] animators = player.GetComponentsInChildren<Animator>(true);
            if (animators.Length != 1) throw new Exception("One Animator required");
            Animator animator = animators[0];
            var controller = animator.runtimeAnimatorController as AnimatorController;
            if (controller == null || animator.avatar == null || !animator.avatar.isValid) throw new Exception("Missing valid original controller/avatar");
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(controller)))
                if (asset is StateMachineBehaviour) throw new Exception("Controller has executable state behaviour; review it before exporting");
            // Keep original controller/rig, override clips with event-free local copies.
            var overrides = new AnimatorOverrideController(controller);
            AssetDatabase.CreateAsset(overrides, Generated + "/Player.overrideController");
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>(); overrides.GetOverrides(pairs);
            for (int i = 0; i < pairs.Count; i++)
            {
                AnimationClip clip = UnityEngine.Object.Instantiate(pairs[i].Key);
                clip.name = "RemoteClip_" + i;
                AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
                AssetDatabase.CreateAsset(clip, Generated + "/Clip" + i + ".anim");
                pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[i].Key, clip);
            }
            overrides.ApplyOverrides(pairs); animator.runtimeAnimatorController = overrides;
            animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            PrefabUtility.SaveAsPrefabAsset(player, Generated + "/SkyCoopRemote.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }
        AssetDatabase.SaveAssets();
        string prefab = Generated + "/SkyCoopRemote.prefab";
        string[] dependencies = AssetDatabase.GetDependencies(prefab, true);
        File.WriteAllLines(Path.Combine(output, "character-dependencies.txt"), dependencies.OrderBy(p => p));
        var build = new AssetBundleBuild { assetBundleName = "skycoop-character", assetNames = new[] { prefab } };
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
        var result = BuildPipeline.BuildAssetBundles(output, new[] { build }, BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode,
            BuildTarget.StandaloneWindows64);
        if (result == null) throw new Exception("Unity AssetBundle build failed");
        string hash;
        using (var stream = File.OpenRead(Path.Combine(output, "skycoop-character")))
        using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        File.WriteAllText(Path.Combine(output, "character.json"), "{\n  \"schema\": 1,\n  \"targetGame\": \"2.55\",\n  \"editorVersion\": \"" + Application.unityVersion
            + "\",\n  \"sha256\": \"" + hash + "\",\n  \"graphicsApi\": \"Direct3D11\",\n  \"sourceCommit\": \"7b0616e24173950de66d99c34d2f1a871de56161\",\n  \"redistribution\": \"not authorized by this build tool\"\n}\n");
        Debug.Log("SkyCoop: local character build finished. Native game compatibility and licensing still require review.");
    }
}
