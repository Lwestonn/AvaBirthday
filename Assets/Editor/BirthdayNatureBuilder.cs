using System.IO;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Creates the placeholder flower and tree prefabs, hooks up the flower trail
/// and the growth finale, and normalises the memory orbs so hand-made ones
/// match generated ones.
///
/// Tools > Birthday > Build Flowers and Tree
/// Tools > Birthday > Normalize Orbs
/// </summary>
public static class BirthdayNatureBuilder
{
    private const string PrefabFolder = "Assets/Prefabs";

    // ------------------------------------------------------- flowers + tree

    [MenuItem("Tools/Birthday/Build Flowers and Tree")]
    public static void BuildNature()
    {
        EnsureFolder(PrefabFolder);

        var flower = BuildFlowerPrefab();
        var tree = BuildTreePrefab();

        // --- flower trail on the player
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            var lockComp = Object.FindFirstObjectByType<PlayerControlLock>();
            player = lockComp != null ? lockComp.gameObject : null;
        }

        FlowerTrail trail = null;
        if (player != null)
        {
            trail = player.GetComponent<FlowerTrail>() ?? Undo.AddComponent<FlowerTrail>(player);
            Undo.RecordObject(trail, "Wire flower trail");
            trail.flowerPrefab = flower;
            trail.carrier = player.GetComponent<HeadCarrier>();
            EditorUtility.SetDirty(trail);
        }
        else
        {
            Debug.LogWarning("[Birthday] No player found, skipped the flower trail.");
        }

        // --- growth finale on the body
        var body = Object.FindFirstObjectByType<BodyReattach>(FindObjectsInactive.Include);
        if (body != null)
        {
            var growth = body.GetComponent<FinaleGrowth>() ?? Undo.AddComponent<FinaleGrowth>(body.gameObject);
            Undo.RecordObject(growth, "Wire finale growth");
            growth.treePrefab = tree;
            growth.flowers = trail;
            EditorUtility.SetDirty(growth);
        }
        else
        {
            Debug.LogWarning("[Birthday] No BodyReattach found, skipped the finale growth.");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("<b>[Birthday]</b> Flower and tree prefabs built in Assets/Prefabs. " +
                  "Carry the head and walk to see flowers bloom behind you.");
    }

    private static GameObject BuildFlowerPrefab()
    {
        string path = $"{PrefabFolder}/Flower.prefab";

        var root = new GameObject("Flower");

        var stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stem.name = "Stem";
        stem.transform.SetParent(root.transform, false);
        stem.transform.localPosition = new Vector3(0f, 0.16f, 0f);
        stem.transform.localScale = new Vector3(0.03f, 0.16f, 0.03f);
        Object.DestroyImmediate(stem.GetComponent<Collider>());
        Paint(stem, new Color(0.36f, 0.62f, 0.32f));

        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Bloom";
        head.transform.SetParent(root.transform, false);
        head.transform.localPosition = new Vector3(0f, 0.34f, 0f);
        head.transform.localScale = new Vector3(0.16f, 0.09f, 0.16f);
        Object.DestroyImmediate(head.GetComponent<Collider>());
        Paint(head, new Color(1f, 0.72f, 0.84f));

        var centre = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        centre.name = "Centre";
        centre.transform.SetParent(root.transform, false);
        centre.transform.localPosition = new Vector3(0f, 0.37f, 0f);
        centre.transform.localScale = Vector3.one * 0.055f;
        Object.DestroyImmediate(centre.GetComponent<Collider>());
        Paint(centre, new Color(1f, 0.92f, 0.55f));

        // No colliders anywhere: hundreds of these must not touch physics.
        root.AddComponent<GrowIn>();

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject BuildTreePrefab()
    {
        string path = $"{PrefabFolder}/Tree.prefab";

        var root = new GameObject("Tree");

        var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "Trunk";
        trunk.transform.SetParent(root.transform, false);
        trunk.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        trunk.transform.localScale = new Vector3(0.38f, 1.6f, 0.38f);
        Object.DestroyImmediate(trunk.GetComponent<Collider>());
        Paint(trunk, new Color(0.42f, 0.30f, 0.22f));

        // Three overlapping spheres read as a canopy far better than one.
        AddCanopy(root.transform, new Vector3(0f, 3.7f, 0f), 2.6f, new Color(0.45f, 0.72f, 0.42f));
        AddCanopy(root.transform, new Vector3(1.1f, 3.2f, 0.5f), 1.9f, new Color(0.52f, 0.78f, 0.46f));
        AddCanopy(root.transform, new Vector3(-1.0f, 3.3f, -0.4f), 1.8f, new Color(0.40f, 0.66f, 0.38f));

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void AddCanopy(Transform parent, Vector3 pos, float size, Color c)
    {
        var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        s.name = "Canopy";
        s.transform.SetParent(parent, false);
        s.transform.localPosition = pos;
        s.transform.localScale = Vector3.one * size;
        Object.DestroyImmediate(s.GetComponent<Collider>());
        Paint(s, c);
    }

    // ----------------------------------------------------------- orb repair

    /// <summary>
    /// Makes every orb identical in structure, so a hand-made one behaves exactly
    /// like a generated one: trigger collider, Visual child, glow light, its own
    /// emissive material, and a MemoryData with a distinct colour.
    /// </summary>
    [MenuItem("Tools/Birthday/Normalize Orbs")]
    public static void NormalizeOrbs()
    {
        var pickups = Object.FindObjectsByType<MemoryPickup>(FindObjectsSortMode.None);
        if (pickups.Length == 0) { Debug.LogWarning("[Birthday] No orbs found."); return; }

        System.Array.Sort(pickups, (a, b) => string.CompareOrdinal(a.name, b.name));

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        int fixedUp = 0;

        for (int i = 0; i < pickups.Length; i++)
        {
            var p = pickups[i];
            var go = p.gameObject;

            // --- trigger collider
            var col = go.GetComponent<SphereCollider>();
            if (col == null) col = Undo.AddComponent<SphereCollider>(go);
            Undo.RecordObject(col, "Normalize collider");
            col.isTrigger = true;
            col.radius = 1.5f;
            col.center = Vector3.zero;

            // Strip any stray non-trigger colliders left over from a primitive.
            foreach (var extra in go.GetComponents<Collider>())
                if (extra != col) Undo.DestroyObjectImmediate(extra);

            // --- colour. Memory_01 is the red one, the rest spread round the wheel.
            Color c = go.name.EndsWith("01")
                ? new Color(1f, 0.34f, 0.36f)
                : Color.HSVToRGB(Mathf.Repeat(0.08f + i / (float)Mathf.Max(1, pickups.Length), 1f), 0.4f, 1f);

            if (p.memory != null)
            {
                Undo.RecordObject(p.memory, "Set glow colour");
                p.memory.glowColor = c;
                EditorUtility.SetDirty(p.memory);
            }

            // --- Visual child
            Transform vis = p.visual;
            if (vis == null && go.transform.childCount > 0)
            {
                foreach (Transform child in go.transform)
                    if (child.GetComponent<MeshRenderer>() != null) { vis = child; break; }
            }

            if (vis == null)
            {
                var made = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                made.name = "Visual";
                made.transform.SetParent(go.transform, false);
                Undo.RegisterCreatedObjectUndo(made, "Create Visual");
                vis = made.transform;
            }

            vis.localPosition = Vector3.zero;
            vis.localScale = Vector3.one * 0.55f;
            foreach (var vc in vis.GetComponents<Collider>()) Undo.DestroyObjectImmediate(vc);

            // --- its own material, never shared
            var r = vis.GetComponent<Renderer>();
            if (r != null)
            {
                var mat = new Material(shader) { name = go.name + "_Mat" };
                mat.color = c;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.SetColor("_EmissionColor", c * 3f);

                Undo.RecordObject(r, "Assign orb material");
                r.sharedMaterial = mat;
                EditorUtility.SetDirty(r);
            }

            // --- glow light
            var glowT = go.transform.Find("Glow");
            Light light;
            if (glowT == null)
            {
                var glowGo = new GameObject("Glow");
                glowGo.transform.SetParent(go.transform, false);
                Undo.RegisterCreatedObjectUndo(glowGo, "Create Glow");
                light = glowGo.AddComponent<Light>();
            }
            else
            {
                light = glowT.GetComponent<Light>() ?? glowT.gameObject.AddComponent<Light>();
            }

            Undo.RecordObject(light, "Normalize glow");
            light.type = LightType.Point;
            light.color = c;
            light.range = 5f;
            light.intensity = 2.2f;
            light.shadows = LightShadows.None;
            light.bounceIntensity = 0f;   // silences the realtime-GI warning

            Undo.RecordObject(p, "Normalize pickup");
            p.visual = vis;
            EditorUtility.SetDirty(p);

            fixedUp++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"<b>[Birthday]</b> {fixedUp} orbs normalized. Memory_01 is now the red one and matches the rest.");
    }

    // -------------------------------------------------------------- helpers

    private static void Paint(GameObject go, Color c)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) return;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader) { name = go.name + "_Mat" };
        mat.color = c;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        r.sharedMaterial = mat;
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
