using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

/// <summary>
/// The three chores that eat all your time when you import downloaded props:
/// fixing pink materials, adding colliders, and marking things static.
///
/// Select props in the Hierarchy, run one menu item, done.
///
/// Tools > Birthday > Props > ...
/// </summary>
public static class BirthdayPropTools
{
    // -------------------------------------------------------------------
    // Menu items
    // -------------------------------------------------------------------

    [MenuItem("Tools/Birthday/Props/Prep Selected (Box Collider)")]
    public static void PrepBox() => Prep(false);

    [MenuItem("Tools/Birthday/Props/Prep Selected (Tree Trunk Collider)")]
    public static void PrepTrunk() => Prep(true);

    [MenuItem("Tools/Birthday/Props/Convert Selected Materials To URP")]
    public static void ConvertSelectionToUrp()
    {
        var mats = GatherMaterials(Selection.gameObjects);

        // Also allow selecting materials directly in the Project window.
        mats.AddRange(Selection.objects.OfType<Material>());

        int n = ConvertMaterials(mats);
        AssetDatabase.SaveAssets();

        Debug.Log($"<b>[Birthday]</b> Converted {n} material(s) to URP Lit.");
    }

    [MenuItem("Tools/Birthday/Props/Remove Colliders From Selected")]
    public static void StripColliders()
    {
        int n = 0;
        foreach (var go in Selection.gameObjects)
        {
            foreach (var c in go.GetComponents<Collider>())
            {
                Undo.DestroyObjectImmediate(c);
                n++;
            }
        }
        Debug.Log($"<b>[Birthday]</b> Removed {n} collider(s). Run a Prep item to rebuild them.");
    }

    // -------------------------------------------------------------------

    private static void Prep(bool trunk)
    {
        var selection = Selection.gameObjects
            .Where(g => g.scene.IsValid())
            .ToArray();

        if (selection.Length == 0)
        {
            EditorUtility.DisplayDialog("Nothing selected",
                "Select the props in the Hierarchy first.\n\n" +
                "This works on objects in the scene, not on prefabs in the Project window.",
                "OK");
            return;
        }

        int collided = 0, madeStatic = 0, skipped = 0;

        foreach (var go in selection)
        {
            Undo.RegisterCompleteObjectUndo(go, "Prep prop");

            // ---- collider
            if (go.GetComponentInChildren<Collider>() != null)
            {
                skipped++;
            }
            else if (LocalBounds(go, out Bounds b))
            {
                if (trunk)
                {
                    var cap = Undo.AddComponent<CapsuleCollider>(go);
                    cap.direction = 1;   // Y

                    // A box around a whole tree means she cannot walk anywhere near it.
                    // A narrow column at the trunk is what you actually want. This is a
                    // guess based on the canopy width, so widen it by hand on fat trunks.
                    float horiz = Mathf.Min(b.extents.x, b.extents.z);
                    cap.radius = Mathf.Max(0.08f, horiz * 0.28f);
                    cap.height = Mathf.Max(cap.radius * 2f, b.size.y);
                    cap.center = b.center;
                }
                else
                {
                    var box = Undo.AddComponent<BoxCollider>(go);
                    box.center = b.center;
                    box.size = b.size;
                }
                collided++;
            }

            // ---- static batching, the single biggest WebGL win for scenery
            if (go.GetComponent<Rigidbody>() == null && go.GetComponent<Animator>() == null)
            {
                GameObjectUtility.SetStaticEditorFlags(go,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
                madeStatic++;
            }
        }

        // ---- materials
        int mats = ConvertMaterials(GatherMaterials(selection));
        AssetDatabase.SaveAssets();

        Debug.Log($"<b>[Birthday]</b> Prepped {selection.Length} prop(s). " +
                  $"{collided} collider(s) added, {skipped} already had one, " +
                  $"{madeStatic} marked static, {mats} material(s) converted to URP.\n" +
                  $"Now press Ctrl+Shift+D to drop them onto the terrain.");
    }

    // -------------------------------------------------------------------
    // Materials
    // -------------------------------------------------------------------

    private static List<Material> GatherMaterials(IEnumerable<GameObject> objects)
    {
        var list = new List<Material>();
        foreach (var go in objects)
        {
            if (go == null) continue;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                list.AddRange(r.sharedMaterials);
        }
        return list;
    }

    /// <summary>
    /// Swaps Built-in Standard shaders for URP Lit and carries the maps across.
    /// This is what fixes the bright pink. Doing it by hand loses the textures,
    /// because URP reads _BaseMap and _BaseColor while Standard wrote _MainTex
    /// and _Color.
    /// </summary>
    private static int ConvertMaterials(IEnumerable<Material> materials)
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
        {
            Debug.LogWarning("[Birthday] URP Lit shader not found. Is this project actually on URP?");
            return 0;
        }

        int n = 0;

        foreach (var m in materials.Where(x => x != null).Distinct())
        {
            if (m.shader != null && m.shader.name.StartsWith("Universal Render Pipeline/")) continue;

            // Read everything BEFORE swapping the shader, because assigning a new
            // shader can drop properties the new one does not declare.
            Texture main = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
            Vector2 scale = main != null ? m.GetTextureScale("_MainTex") : Vector2.one;
            Vector2 offset = main != null ? m.GetTextureOffset("_MainTex") : Vector2.zero;
            Color col = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            Texture bump = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
            Texture emis = m.HasProperty("_EmissionMap") ? m.GetTexture("_EmissionMap") : null;
            Color emisCol = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
            float metallic = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0f;
            float gloss = m.HasProperty("_Glossiness") ? m.GetFloat("_Glossiness") : 0.35f;

            Undo.RecordObject(m, "Convert to URP");
            m.shader = lit;

            if (main != null)
            {
                m.SetTexture("_BaseMap", main);
                m.SetTextureScale("_BaseMap", scale);
                m.SetTextureOffset("_BaseMap", offset);
            }

            m.SetColor("_BaseColor", col);

            if (bump != null)
            {
                m.SetTexture("_BumpMap", bump);
                m.EnableKeyword("_NORMALMAP");
            }

            if (emis != null || emisCol.maxColorComponent > 0.001f)
            {
                if (emis != null) m.SetTexture("_EmissionMap", emis);
                m.SetColor("_EmissionColor", emisCol);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", gloss);

            EditorUtility.SetDirty(m);
            n++;
        }

        return n;
    }

    // -------------------------------------------------------------------
    // Bounds
    // -------------------------------------------------------------------

    /// <summary>
    /// Bounds of every child mesh, expressed in the root's own local space.
    /// Renderer.bounds is world-space and axis-aligned, so using it directly
    /// gives you a collider that is too big the moment a prop is rotated.
    /// </summary>
    private static bool LocalBounds(GameObject go, out Bounds bounds)
    {
        bounds = new Bounds();
        bool any = false;

        var toLocal = go.transform.worldToLocalMatrix;

        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var mf = r.GetComponent<MeshFilter>();
            Mesh mesh = mf != null ? mf.sharedMesh : null;
            if (mesh == null) continue;

            Bounds mb = mesh.bounds;
            Matrix4x4 m = toLocal * r.transform.localToWorldMatrix;

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = mb.center + Vector3.Scale(mb.extents, Corner(i));
                Vector3 p = m.MultiplyPoint3x4(corner);

                if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                else bounds.Encapsulate(p);
            }
        }

        return any;
    }

    private static Vector3 Corner(int i) => new(
        (i & 1) == 0 ? -1f : 1f,
        (i & 2) == 0 ? -1f : 1f,
        (i & 4) == 0 ? -1f : 1f);
}
