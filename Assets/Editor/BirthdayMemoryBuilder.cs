using System.IO;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Creates the memory orbs and their MemoryData assets in one go.
///
/// Menu: Tools > Birthday > Create Memory Orbs
///
/// NON-DESTRUCTIVE. It only fills in what is missing:
///   - an orb named Memory_01..Memory_10 that already exists is left alone
///   - a MemoryData asset that already exists is never overwritten
/// So running it after you have written your real notes is safe.
/// </summary>
public static class BirthdayMemoryBuilder
{
    private const int Count = 10;
    private const string AssetFolder = "Assets/Memories";
    private const string ParentName = "Memories";

    [MenuItem("Tools/Birthday/Create Memory Orbs")]
    public static void CreateOrbs()
    {
        EnsureFolder(AssetFolder);

        var parent = GameObject.Find(ParentName);
        if (parent == null)
        {
            parent = new GameObject(ParentName);
            Undo.RegisterCreatedObjectUndo(parent, "Create Memories parent");
        }

        Vector3 center = Vector3.zero;
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) center = player.transform.position;
        center.y = 0f;

        int madeOrbs = 0, madeAssets = 0, reused = 0;

        for (int i = 1; i <= Count; i++)
        {
            string id = $"Memory_{i:00}";

            // --- the data asset
            string path = $"{AssetFolder}/{id}.asset";
            var data = AssetDatabase.LoadAssetAtPath<MemoryData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<MemoryData>();
                data.title = $"Memory {i}";
                data.note = "Replace this with the real one.";
                data.glowColor = Color.HSVToRGB((i - 1) / (float)Count, 0.35f, 1f);
                AssetDatabase.CreateAsset(data, path);
                madeAssets++;
            }

            // --- the orb in the scene
            var existing = GameObject.Find(id);
            if (existing != null)
            {
                // Already placed by hand. Just make sure it has data and move on.
                var pk = existing.GetComponent<MemoryPickup>();
                if (pk != null && pk.memory == null)
                {
                    Undo.RecordObject(pk, "Assign memory");
                    pk.memory = data;
                    EditorUtility.SetDirty(pk);
                }
                if (existing.transform.parent == null)
                    Undo.SetTransformParent(existing.transform, parent.transform, "Reparent memory");

                reused++;
                continue;
            }

            BuildOrb(id, data, parent.transform, PositionFor(i, center));
            madeOrbs++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeGameObject = parent;
        Debug.Log($"<b>[Birthday]</b> {madeOrbs} orbs created, {madeAssets} MemoryData assets created, {reused} existing orbs left alone. " +
                  $"Assets are in {AssetFolder}. Reposition the orbs, then fill in the notes and photos.");
    }

    private static Vector3 PositionFor(int index, Vector3 center)
    {
        // Ring around the player so nothing spawns inside her. Reposition freely.
        float angle = (index - 1) / (float)Count * Mathf.PI * 2f;
        float radius = 16f;
        return center + new Vector3(Mathf.Sin(angle) * radius, 1.2f, Mathf.Cos(angle) * radius);
    }

    private static void BuildOrb(string id, MemoryData data, Transform parent, Vector3 pos)
    {
        var root = new GameObject(id);
        Undo.RegisterCreatedObjectUndo(root, "Create " + id);
        root.transform.SetParent(parent, false);
        root.transform.position = pos;

        var col = root.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.5f;

        // Visual is a CHILD so the bob/spin never moves the trigger.
        var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.name = "Visual";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localScale = Vector3.one * 0.55f;
        Object.DestroyImmediate(visual.GetComponent<SphereCollider>());

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader) { name = id + "_Mat" };
        Color c = data.glowColor;
        mat.color = c;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        ApplyEmission(mat, c);
        visual.GetComponent<Renderer>().sharedMaterial = mat;

        // A soft light sells the "magical" read more than the emission alone.
        var lightGo = new GameObject("Glow");
        lightGo.transform.SetParent(root.transform, false);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = c;
        light.range = 5f;
        light.intensity = 2.2f;
        light.shadows = LightShadows.None;   // ten shadow-casting lights would tank WebGL

        var pickup = root.AddComponent<MemoryPickup>();
        pickup.memory = data;
        pickup.visual = visual.transform;
    }

    /// <summary>
    /// Turns emission on properly. Enabling the "_EMISSION" keyword alone is NOT
    /// enough on URP: BaseShaderGUI re-derives that keyword from the material's
    /// globalIlluminationFlags, so without setting the flags your emission gets
    /// silently switched back off and the Inspector checkbox stays unticked.
    /// </summary>
    private static void ApplyEmission(Material mat, Color color, float intensity = 3f)
    {
        if (!mat.HasProperty("_EmissionColor")) return;

        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        mat.SetColor("_EmissionColor", color * intensity);
        EditorUtility.SetDirty(mat);
    }

    /// <summary>
    /// Repairs emission across the scene, in two passes.
    ///
    /// The bug this exists to undo: a primitive created with GameObject > 3D Object
    /// uses Unity's DEFAULT Lit material, which the ground plane also uses. Writing
    /// emission to that sharedMaterial lights up every object in the scene at once.
    ///
    /// Pass 1 strips emission from everything that is not an orb.
    /// Pass 2 gives each orb its OWN brand new material, so nothing is ever shared.
    /// </summary>
    [MenuItem("Tools/Birthday/Repair Emission")]
    public static void RepairEmission()
    {
        var pickups = Object.FindObjectsByType<MemoryPickup>(FindObjectsSortMode.None);
        if (pickups.Length == 0)
        {
            Debug.LogWarning("[Birthday] No MemoryPickup objects found in the scene.");
            return;
        }

        // Which renderers belong to orbs, so pass 1 leaves them alone.
        var orbRenderers = new System.Collections.Generic.HashSet<Renderer>();
        foreach (var p in pickups)
        {
            var vis = VisualOf(p);
            if (vis == null) continue;
            var r = vis.GetComponent<Renderer>();
            if (r != null) orbRenderers.Add(r);
        }

        // ---- Pass 1: un-glow the world -------------------------------------
        int cleared = 0;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (orbRenderers.Contains(r)) continue;
            if (r.GetComponent<TMPro.TMP_Text>() != null) continue;   // text shaders are their own world

            foreach (var m in r.sharedMaterials)
            {
                if (m == null || !m.HasProperty("_EmissionColor")) continue;
                if (m.globalIlluminationFlags == MaterialGlobalIlluminationFlags.EmissiveIsBlack) continue;

                Undo.RecordObject(m, "Clear emission");
                m.DisableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                m.SetColor("_EmissionColor", Color.black);
                EditorUtility.SetDirty(m);
                cleared++;
            }
        }

        // ---- Pass 2: give every orb a private, correctly emissive material --
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        int rebuilt = 0;

        foreach (var p in pickups)
        {
            var vis = VisualOf(p);
            if (vis == null) continue;

            var r = vis.GetComponent<Renderer>();
            if (r == null) continue;

            Color c = p.memory != null ? p.memory.glowColor : Color.white;

            // A FRESH material every time. Never mutate whatever was there, because
            // it may be shared with the ground, the head, or half the scene.
            var mat = new Material(shader) { name = p.name + "_Mat" };
            mat.color = c;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            ApplyEmission(mat, c);

            Undo.RecordObject(r, "Assign orb material");
            r.sharedMaterial = mat;
            EditorUtility.SetDirty(r);
            rebuilt++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"<b>[Birthday]</b> Emission cleared on {cleared} non-orb material(s), {rebuilt} orbs given their own material. " +
                  $"Only the orbs should glow now.");
    }

    private static Transform VisualOf(MemoryPickup p)
    {
        if (p.visual != null) return p.visual;
        return p.transform.childCount > 0 ? p.transform.GetChild(0) : null;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
