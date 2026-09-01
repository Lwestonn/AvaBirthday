using System.Linq;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Creates the water plane, its material, and all the transparency settings.
///
/// URP transparency is not one checkbox: it needs the surface type, both blend
/// factors, depth write, the render queue and a shader keyword all set to
/// agree with each other. Getting one wrong gives you either an opaque sheet or
/// water that vanishes. This does the whole combination.
///
/// Tools > Birthday > Build Water
/// Safe to re-run. It reuses the existing Water object and material.
/// </summary>
public static class BirthdayWaterBuilder
{
    private const string WaterName = "Water";
    private const string MatPath = "Assets/WaterAssets/Water.mat";

    [MenuItem("Tools/Birthday/Build Water")]
    public static void BuildWater()
    {
        var terrain = Object.FindFirstObjectByType<Terrain>();
        if (terrain == null)
        {
            EditorUtility.DisplayDialog("No terrain",
                "There is no Terrain in the scene. Make the island first.", "OK");
            return;
        }

        var td = terrain.terrainData;
        Vector3 tPos = terrain.transform.position;
        Vector3 tSize = td.size;

        float waterY = GuessWaterLevel(td, tPos.y);

        // ---------------------------------------------------------- the plane
        var existing = GameObject.Find(WaterName);
        GameObject plane;

        if (existing != null)
        {
            plane = existing;
        }
        else
        {
            plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = WaterName;
            Undo.RegisterCreatedObjectUndo(plane, "Create Water");
        }

        Undo.RecordObject(plane.transform, "Position water");

        // Unity's Plane primitive is 10x10 units, so scale is size/10.
        // Overshoot the terrain by 60% so the horizon never shows an edge.
        float span = Mathf.Max(tSize.x, tSize.z) * 1.6f;
        plane.transform.position = new Vector3(tPos.x + tSize.x * 0.5f, waterY, tPos.z + tSize.z * 0.5f);
        plane.transform.localScale = new Vector3(span / 10f, 1f, span / 10f);
        plane.transform.rotation = Quaternion.identity;

        // She should walk INTO water, not onto it.
        foreach (var col in plane.GetComponents<Collider>())
            Undo.DestroyObjectImmediate(col);

        // ------------------------------------------------------- the material
        var mat = BuildMaterial();
        var rend = plane.GetComponent<Renderer>();
        Undo.RecordObject(rend, "Assign water material");
        rend.sharedMaterial = mat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;

        // -------------------------------------------------------- the motion
        var surface = plane.GetComponent<WaterSurface>();
        if (surface == null) surface = Undo.AddComponent<WaterSurface>(plane);

        EditorUtility.SetDirty(plane);
        Selection.activeGameObject = plane;

        Debug.Log($"<b>[Birthday]</b> Water built at y = {waterY:0.##}, {span:0} metres across. " +
                  $"Adjust its Y in the Inspector until the beach looks right, then set " +
                  $"WorldBounds' Kill Plane Y a few metres below it.");
    }

    /// <summary>
    /// Reads the actual heightmap and picks a level near the low end, so the
    /// water lands somewhere sensible instead of a hardcoded guess.
    /// </summary>
    private static float GuessWaterLevel(TerrainData td, float terrainBaseY)
    {
        int res = Mathf.Min(td.heightmapResolution, 129);
        float[,] heights = td.GetHeights(0, 0, res, res);

        var flat = new float[res * res];
        int i = 0;
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
                flat[i++] = heights[y, x];

        System.Array.Sort(flat);

        // 30th percentile: below most of the land, above the carved-out edges.
        float p30 = flat[Mathf.Clamp((int)(flat.Length * 0.30f), 0, flat.Length - 1)];
        float p95 = flat[Mathf.Clamp((int)(flat.Length * 0.95f), 0, flat.Length - 1)];

        // Sit just under the low band so beaches stay visible.
        float normalized = Mathf.Lerp(p30, p95, 0.15f);
        return terrainBaseY + normalized * td.size.y;
    }

    private static Material BuildMaterial()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(MatPath);

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogError("[Birthday] URP Lit shader not found. Is this a URP project?");
            return existing;
        }

        var mat = existing != null ? existing : new Material(shader) { name = "Water" };
        mat.shader = shader;

        // --- textures
        var baseTex = FindTexture("Water_BaseColor");
        var normalTex = FindTexture("Water_Normal", asNormalMap: true);

        if (baseTex != null) mat.SetTexture("_BaseMap", baseTex);
        if (normalTex != null)
        {
            mat.SetTexture("_BumpMap", normalTex);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetFloat("_BumpScale", 0.6f);
        }

        mat.SetColor("_BaseColor", new Color(0.42f, 0.68f, 0.76f, 0.72f));
        mat.SetFloat("_Smoothness", 0.92f);
        mat.SetFloat("_Metallic", 0f);

        // --- transparency, the part that has to be set as a whole
        mat.SetFloat("_Surface", 1f);                       // 0 opaque, 1 transparent
        mat.SetFloat("_Blend", 0f);                         // alpha blending
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_AlphaClip", 0f);

        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.DisableKeyword("_ALPHATEST_ON");

        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        if (existing == null)
        {
            EnsureFolder("Assets/WaterAssets");
            AssetDatabase.CreateAsset(mat, MatPath);
        }

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    /// <summary>
    /// Finds a texture by name anywhere in the project. For the normal map it
    /// also fixes the import setting, which is the single easiest thing to
    /// forget and produces very strange lighting when wrong.
    /// </summary>
    private static Texture2D FindTexture(string name, bool asNormalMap = false)
    {
        string guid = AssetDatabase.FindAssets($"{name} t:Texture2D").FirstOrDefault();
        if (guid == null)
        {
            Debug.LogWarning($"[Birthday] Could not find a texture named '{name}'. " +
                             $"Did the WaterAssets folder get unzipped into Assets?");
            return null;
        }

        string path = AssetDatabase.GUIDToAssetPath(guid);

        if (asNormalMap)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
                Debug.Log($"[Birthday] Set '{name}' to Normal map import type.");
            }
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
