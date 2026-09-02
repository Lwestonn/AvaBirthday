using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Turns a downloaded HDRI into a working sky in one click.
///
/// Does the four things that are easy to get wrong by hand:
///   - imports the HDRI as a cubemap with the correct lat-long mapping
///   - caps it at a WebGL-friendly size
///   - builds the skybox material and assigns it to the scene
///   - switches ambient light and reflections over to the sky, which is what
///     actually makes the island look lit rather than flat
///
/// Tools > Birthday > Build Sky
/// </summary>
public static class BirthdaySkyBuilder
{
    private const int MaxSkySize = 2048;   // 4K HDRIs are a needless WebGL download
    private const string MaterialFolder = "Assets/Materials";

    [MenuItem("Tools/Birthday/Build Sky")]
    public static void BuildSky()
    {
        string path = FindHdri();
        if (path == null) return;

        // ---- 1. import as a cubemap
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            EditorUtility.DisplayDialog("Not a texture", $"Could not import:\n{path}", "OK");
            return;
        }

        importer.textureShape = TextureImporterShape.TextureCube;

        // HDRIs are equirectangular, which Unity calls Latitude-Longitude
        // (Cylindrical). Leaving this on Auto is the usual reason a sky comes in
        // looking smeared or pinched at the horizon.
        importer.generateCubemap = TextureImporterGenerateCubemap.Cylindrical;

        importer.maxTextureSize = MaxSkySize;
        importer.mipmapEnabled = true;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.SaveAndReimport();

        var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
        if (cube == null)
        {
            EditorUtility.DisplayDialog("Import failed",
                "Unity did not produce a Cubemap from that file. Is it really an HDRI?", "OK");
            return;
        }

        // ---- 2. skybox material
        var shader = Shader.Find("Skybox/Cubemap");
        if (shader == null)
        {
            EditorUtility.DisplayDialog("Missing shader", "Skybox/Cubemap not found.", "OK");
            return;
        }

        if (!Directory.Exists(MaterialFolder))
        {
            Directory.CreateDirectory(MaterialFolder);
            AssetDatabase.Refresh();
        }

        string matPath = $"{MaterialFolder}/Sky_{Path.GetFileNameWithoutExtension(path)}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        bool isNew = mat == null;

        if (isNew)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, matPath);
        }

        mat.shader = shader;
        mat.SetTexture("_Tex", cube);
        mat.SetFloat("_Exposure", 1f);
        EditorUtility.SetDirty(mat);

        // ---- 3. hand it to the scene
        RenderSettings.skybox = mat;

        // This is the part people miss. Without it the sky is just a backdrop and
        // the island stays lit by a flat grey ambient colour.
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.reflectionIntensity = 1f;

        // ---- 4. gentle distance fog, so the terrain edge fades instead of
        // ending in a hard line against the sky
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.0035f;
        RenderSettings.fogColor = new Color(0.72f, 0.80f, 0.88f);

        DynamicGI.UpdateEnvironment();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Selection.activeObject = mat;
        EditorGUIUtility.PingObject(mat);

        Debug.Log(
            $"<b>[Birthday]</b> Sky built from <b>{Path.GetFileName(path)}</b>.\n" +
            $"  Material: {matPath} ({(isNew ? "created" : "updated")})\n" +
            $"  Cubemap capped at {MaxSkySize}px for WebGL\n" +
            $"  Ambient light and reflections now come from the sky\n" +
            $"  Soft distance fog on (RenderSettings.fogDensity {RenderSettings.fogDensity})\n\n" +
            $"Next: select the material and drag its <b>Rotation</b> slider until the sun in " +
            $"the sky lines up with your Directional Light. Then run " +
            $"Tools > Birthday > Check Sky to see if they agree.");
    }

    /// <summary>
    /// Reports whether the sun in the sky and the Directional Light are pointing
    /// the same way, without you having to eyeball it from inside a hill.
    /// </summary>
    [MenuItem("Tools/Birthday/Check Sky")]
    public static void CheckSky()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<b>[Birthday] Sky report</b>\n");

        if (RenderSettings.skybox == null)
            sb.AppendLine("  No skybox material assigned. Run Build Sky.");
        else
            sb.AppendLine($"  Skybox material: {RenderSettings.skybox.name}" +
                          (RenderSettings.skybox.HasProperty("_Rotation")
                              ? $"  (Rotation {RenderSettings.skybox.GetFloat("_Rotation"):0}deg)"
                              : ""));

        sb.AppendLine($"  Ambient source: {RenderSettings.ambientMode}" +
                      (RenderSettings.ambientMode == AmbientMode.Skybox ? "" : "   <- should be Skybox"));
        sb.AppendLine($"  Reflections:    {RenderSettings.defaultReflectionMode}");
        sb.AppendLine($"  Fog:            {(RenderSettings.fog ? $"on, density {RenderSettings.fogDensity}" : "off")}");

        var sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
            .FirstOrDefault(l => l.type == LightType.Directional);

        if (sun == null)
        {
            sb.AppendLine("\n  No Directional Light in the scene. Nothing is casting shadows.");
        }
        else
        {
            Vector3 e = sun.transform.eulerAngles;
            sb.AppendLine($"\n  Directional Light '{sun.name}'");
            sb.AppendLine($"    rotation  X {e.x:0} (height in the sky)   Y {e.y:0} (compass direction)");
            sb.AppendLine($"    intensity {sun.intensity:0.00}   shadows {sun.shadows}");

            if (e.x < 8f || e.x > 80f)
                sb.AppendLine("    NOTE: X below 8 or above 80 gives you either raking shadows " +
                              "across the whole map or almost none. 35 to 55 usually looks best.");

            if (sun.shadows == LightShadows.None)
                sb.AppendLine("    NOTE: shadows are off, which is most of why a scene looks flat.");
        }

        Debug.Log(sb.ToString());
    }

    // -------------------------------------------------------------------

    private static string FindHdri()
    {
        // Prefer whatever is selected in the Project window.
        if (Selection.activeObject != null)
        {
            string sel = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (IsHdri(sel)) return sel;
        }

        var found = AssetDatabase.GetAllAssetPaths()
            .Where(IsHdri)
            .OrderBy(p => p)
            .ToList();

        if (found.Count == 0)
        {
            EditorUtility.DisplayDialog("No HDRI found",
                "Could not find a .hdr or .exr file in the project.\n\n" +
                "Download one from polyhaven.com/hdris (2K is plenty), unzip it into\n" +
                "Assets/ExternalAssets/PolyHaven/, then run this again.",
                "OK");
            return null;
        }

        if (found.Count == 1) return found[0];

        EditorUtility.DisplayDialog("Which sky?",
            $"Found {found.Count} HDRI files:\n\n" +
            string.Join("\n", found.Take(8).Select(Path.GetFileName)) +
            "\n\nSelect the one you want in the Project window, then run this again.",
            "OK");
        return null;
    }

    private static bool IsHdri(string path)
    {
        if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/")) return false;
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext == ".hdr" || ext == ".exr";
    }
}
