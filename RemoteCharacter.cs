#if !DEDICATED
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using UnityEngine;

namespace SkyCoop
{
    // Rendering only. No legacy MultiplayerPlayer or Reborn NetworkPlayer component is attached.
    public sealed class RemoteCharacter
    {
        private AssetBundle bundle;
        private GameObject instance;
        private Animator animator;
        private bool attempted, fallback;
        public bool MarkerOnly { get; set; }
        public bool IsModel => instance != null && animator != null && !fallback;
        public string Status => IsModel ? "Reborn render (unverified)" : "fallback marker";
        public void Show(bool show) { if (instance != null) instance.SetActive(show); }

        private void Create()
        {
            if (instance != null) return;
            if (MarkerOnly) attempted = true;
            if (!attempted)
            {
                attempted = true;
                try
                {
                    const string directory = "Mods/SkyCoopResources";
                    using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "character.json")));
                    var root = manifest.RootElement;
                    if (root.GetProperty("schema").GetInt32() != 1 || root.GetProperty("targetGame").GetString() != "2.55"
                        || root.GetProperty("editorVersion").GetString() != Application.unityVersion
                        || root.GetProperty("graphicsApi").GetString() != SystemInfo.graphicsDeviceType.ToString())
                        throw new InvalidDataException("Character manifest/game Unity runtime mismatch: runtime=" + Application.unityVersion);
                    string file = Path.Combine(directory, "skycoop-character");
                    using var stream = File.OpenRead(file); using var hash = SHA256.Create();
                    string actual = Convert.ToHexString(hash.ComputeHash(stream)).ToLowerInvariant();
                    if (root.GetProperty("sha256").GetString() != actual) throw new InvalidDataException("Character bundle SHA256 mismatch");
                    bundle = AssetBundle.LoadFromFile(file);
                    if (bundle == null) throw new InvalidDataException("Unity rejected character bundle; inspect Player.log for serialized version/shader/dependency errors");
                    GameObject prefab = bundle.LoadAsset<GameObject>("assets/skycoopgenerated/skycoopremote.prefab");
                    if (prefab == null) throw new InvalidDataException("Sanitized SkyCoopRemote prefab missing; raw Reborn bundle is not accepted");
                    // Reject scripts/physics before instantiation; disabled MonoBehaviours can still run Awake.
                    Validate(prefab);
                    instance = GameObject.Instantiate(prefab); instance.SetActive(false);
                    instance.name = "SkyCoop_Reborn_Remote_RENDER_ONLY";
                    foreach (Transform child in instance.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 0;
                    animator = instance.GetComponentInChildren<Animator>(true);
                    animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    animator.SetInteger("Action", 0); animator.SetInteger("Gear", 0); animator.SetBool("Vehicle", false);
                    DiagnosticLog.Event("character.loaded", "sanitized Reborn prefab;sha256=" + actual + ";unity=" + Application.unityVersion + ";renderOnly=true");
                    return;
                }
                catch (Exception error) { DiagnosticLog.Error("character.load-fallback", error); CleanupInstance(); }
            }
            fallback = true;
            instance = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            instance.name = "SkyCoop_Minimal_RemoteMarker_NO_RIG";
            var collider = instance.GetComponent<Collider>(); if (collider != null) { collider.enabled = false; UnityEngine.Object.Destroy(collider); }
            DiagnosticLog.Event("character.fallback", "connection retained; install locally rebuilt character bundle and manifest, then restart; no game assets supplied");
        }
        private static void Validate(GameObject prefab)
        {
            foreach (Component component in prefab.GetComponentsInChildren<Component>(true))
            {
                if (component == null || !(component.TryCast<Transform>() != null || component.TryCast<Animator>() != null || component.TryCast<SkinnedMeshRenderer>() != null || component.TryCast<MeshRenderer>() != null || component.TryCast<MeshFilter>() != null))
                    throw new InvalidDataException("Character contains a forbidden component or missing script; rebuild with the supplied sanitizer");
            }
            var animators = prefab.GetComponentsInChildren<Animator>(true);
            if (animators.Length != 1 || animators[0].runtimeAnimatorController == null || animators[0].avatar == null || !animators[0].avatar.isValid)
                throw new InvalidDataException("Character requires one valid rig, avatar and controller");
            var expected = new System.Collections.Generic.Dictionary<string, AnimatorControllerParameterType> {
                {"IsMoving", AnimatorControllerParameterType.Bool}, {"Crouch", AnimatorControllerParameterType.Bool},
                {"Vehicle", AnimatorControllerParameterType.Bool}, {"DirectionX", AnimatorControllerParameterType.Float},
                {"DirectionY", AnimatorControllerParameterType.Float}, {"Speed", AnimatorControllerParameterType.Float},
                {"Action", AnimatorControllerParameterType.Int}, {"Gear", AnimatorControllerParameterType.Int} };
            foreach (AnimatorControllerParameter parameter in animators[0].parameters)
                if (expected.TryGetValue(parameter.name, out var type) && type == parameter.type) expected.Remove(parameter.name);
            if (expected.Count != 0) throw new InvalidDataException("Character Animator parameter contract mismatch");
            int renderers = 0;
            foreach (SkinnedMeshRenderer renderer in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeSelf) continue;
                if (renderer.sharedMesh == null) throw new InvalidDataException("Character mesh missing");
                foreach (Material material in renderer.sharedMaterials)
                    if (material == null || material.shader == null || !material.shader.isSupported || material.shader.name.Contains("InternalErrorShader"))
                        throw new InvalidDataException("Unsupported/missing character material or shader");
                renderers++;
            }
            if (renderers == 0) throw new InvalidDataException("Character has no visible skinned meshes");
        }
        public void Render(RemoteMotion.Sample sample, bool crouch, bool run)
        {
            try
            {
                Create(); instance.SetActive(true);
                instance.transform.position = new Vector3(sample.X, sample.Y + (fallback ? 0.9f : 0), sample.Z);
                instance.transform.rotation = Quaternion.Euler(0, sample.Yaw, 0);
                if (animator == null) return;
                Vector3 direction = instance.transform.InverseTransformDirection(new Vector3(sample.VX, 0, sample.VZ));
                animator.SetBool("IsMoving", sample.Speed > 0.1f); animator.SetBool("Crouch", crouch);
                animator.SetFloat("DirectionX", Mathf.Clamp(direction.x, -1, 1));
                animator.SetFloat("DirectionY", Mathf.Clamp(direction.z, -1, 1));
                // Reborn has a speed-scaled Walk state, not a separate authored Run clip.
                animator.SetFloat("Speed", sample.Speed <= 0.1f ? 1 : Mathf.Clamp(crouch ? sample.Speed : sample.Speed / 4, run ? 1.2f : 0.2f, 2));
            }
            catch (Exception error)
            {
                DiagnosticLog.Error("character.render-fallback", error); CleanupInstance(); attempted = true;
                // Retry as marker next frame. Asset errors never escape into the connection state machine.
            }
        }
        private void CleanupInstance()
        {
            if (instance != null) { instance.SetActive(false); UnityEngine.Object.Destroy(instance); }
            instance = null; animator = null;
        }
        public void Dispose() { CleanupInstance(); if (bundle != null) bundle.Unload(true); bundle = null; }
    }
}
#endif
