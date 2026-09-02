using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Terrain layers are separate assets with unhelpful default names, and the
/// paint palette shows you a swatch but not which file it came from. These two
/// items connect the dots.
///
/// Tools > Birthday > Terrain > List Layers
/// Tools > Birthday > Terrain > Rename Layers From Textures
/// </summary>
public static class BirthdayLayerTools
{
    [MenuItem("Tools/Birthday/Terrain/List Layers")]
    public static void ListLayers()
    {
        var terrain = GetTerrain();
        if (terrain == null) return;

        var layers = terrain.terrainData.terrainLayers;
        var sb = new System.Text.StringBuilder();

        sb.AppendLine($"<b>[Birthday] Terrain layers on '{terrain.name}'</b>");
        sb.AppendLine($"  These appear in the paint palette in this order, left to right.\n");

        if (layers.Length == 0)
        {
            sb.AppendLine("  None. Nothing has been painted yet.");
        }
        else
        {
            for (int i = 0; i < layers.Length; i++)
            {
                var l = layers[i];
                if (l == null) { sb.AppendLine($"  {i}.  (missing layer asset)"); continue; }

                string tex = l.diffuseTexture != null ? l.diffuseTexture.name : "no texture";
                string path = AssetDatabase.GetAssetPath(l);

                sb.AppendLine($"  {i}.  <b>{l.name}</b>");
                sb.AppendLine($"       texture: {tex}");
                sb.AppendLine($"       tile:    {l.tileSize.x:0.#} x {l.tileSize.y:0.#}");
                sb.AppendLine($"       file:    {path}\n");
            }

            sb.AppendLine("  Rename any of them by clicking the file in the Project window and pressing F2.");
            sb.AppendLine("  Or run Tools > Birthday > Terrain > Rename Layers From Textures to do it automatically.");
        }

        Debug.Log(sb.ToString());

        // Select them all so you can see them in the Project window immediately.
        Selection.objects = layers.Where(l => l != null).Cast<Object>().ToArray();
    }

    /// <summary>
    /// Writes a top-down PNG of the terrain painted in flat colours, one colour
    /// per layer. Open it next to a screenshot of your island and you can see at
    /// a glance which layer is actually where, instead of guessing from a blended
    /// texture that all looks like "green".
    /// </summary>
    [MenuItem("Tools/Birthday/Terrain/Export Layer Map PNG")]
    public static void ExportLayerMap()
    {
        var terrain = GetTerrain();
        if (terrain == null) return;

        var td = terrain.terrainData;
        var layers = td.terrainLayers;

        if (layers.Length == 0)
        {
            EditorUtility.DisplayDialog("No layers", "Nothing painted yet.", "OK");
            return;
        }

        int w = td.alphamapWidth, h = td.alphamapHeight;
        float[,,] alpha = td.GetAlphamaps(0, 0, w, h);

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var pixels = new Color32[w * h];
        var coverage = new float[layers.Length];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int best = 0;
                float bestW = -1f;

                for (int l = 0; l < layers.Length; l++)
                {
                    float v = alpha[y, x, l];
                    if (v > bestW) { bestW = v; best = l; }
                }

                coverage[best] += 1f;

                Color c = Palette(best);

                // Dim anywhere no single layer clearly wins, so blended edges read
                // as muddy rather than as a confident colour.
                if (bestW < 0.6f) c *= 0.55f;

                pixels[y * w + x] = c;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        string path = "Assets/_TerrainLayerMap.png";
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.Refresh();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<b>[Birthday] Layer map exported</b>");
        sb.AppendLine($"  {path}   (north / +Z is up, same as looking straight down in the Scene view)\n");
        sb.AppendLine("  Legend, and how much of the terrain each layer dominates:\n");

        float total = w * h;
        for (int l = 0; l < layers.Length; l++)
        {
            string nm = layers[l] != null ? layers[l].name : $"(missing {l})";
            sb.AppendLine($"    {l}.  {PaletteName(l),-8}  {coverage[l] / total * 100f,5:0.0}%   {nm}");
        }

        sb.AppendLine("\n  Find the colour covering your big grassy areas, then tick THAT layer in Scatter Grass.");

        Debug.Log(sb.ToString());

        var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }

    private static Color Palette(int i) => (i % 8) switch
    {
        0 => new Color(0.20f, 0.85f, 0.25f),   // green
        1 => new Color(1.00f, 0.35f, 0.30f),   // red
        2 => new Color(0.25f, 0.55f, 1.00f),   // blue
        3 => new Color(1.00f, 0.90f, 0.25f),   // yellow
        4 => new Color(0.85f, 0.30f, 0.95f),   // magenta
        5 => new Color(0.20f, 0.90f, 0.90f),   // cyan
        6 => new Color(1.00f, 0.60f, 0.15f),   // orange
        _ => new Color(0.95f, 0.95f, 0.95f),   // white
    };

    private static string PaletteName(int i) => (i % 8) switch
    {
        0 => "GREEN", 1 => "RED", 2 => "BLUE", 3 => "YELLOW",
        4 => "MAGENTA", 5 => "CYAN", 6 => "ORANGE", _ => "WHITE",
    };

    [MenuItem("Tools/Birthday/Terrain/Rename Layers From Textures")]
    public static void RenameFromTextures()
    {
        var terrain = GetTerrain();
        if (terrain == null) return;

        var layers = terrain.terrainData.terrainLayers;
        if (layers.Length == 0)
        {
            EditorUtility.DisplayDialog("No layers", "This terrain has no layers yet.", "OK");
            return;
        }

        var preview = new List<string>();
        var work = new List<(string path, string newName)>();
        var used = new HashSet<string>();

        foreach (var l in layers)
        {
            if (l == null) continue;

            string path = AssetDatabase.GetAssetPath(l);
            if (string.IsNullOrEmpty(path)) continue;

            if (l.diffuseTexture == null)
            {
                preview.Add($"{l.name}  ->  skipped, no texture assigned");
                continue;
            }

            string name = Clean(l.diffuseTexture.name);
            if (string.IsNullOrEmpty(name)) continue;

            // Keep names unique, since two layers can share a texture.
            string final = name;
            int n = 2;
            while (used.Contains(final)) final = $"{name}_{n++}";
            used.Add(final);

            if (l.name == final)
            {
                preview.Add($"{l.name}  ->  already correct");
                continue;
            }

            preview.Add($"{l.name}  ->  {final}");
            work.Add((path, final));
        }

        if (work.Count == 0)
        {
            EditorUtility.DisplayDialog("Nothing to rename",
                "Every layer already matches its texture name.\n\n" + string.Join("\n", preview), "OK");
            return;
        }

        bool go = EditorUtility.DisplayDialog("Rename terrain layers",
            "Renaming the layer asset files. This is safe: Unity tracks them by ID, so " +
            "your painting will not change.\n\n" + string.Join("\n", preview),
            "Rename", "Cancel");

        if (!go) return;

        int done = 0;
        foreach (var (path, newName) in work)
        {
            string err = AssetDatabase.RenameAsset(path, newName);
            if (string.IsNullOrEmpty(err)) done++;
            else Debug.LogWarning($"[Birthday] Could not rename {path}: {err}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<b>[Birthday]</b> Renamed {done} terrain layer(s).\n" +
                  string.Join("\n", preview) +
                  "\n\nNow reopen Tools > Birthday > Scatter Grass and the layer list will make sense.");
    }

    // -------------------------------------------------------------------

    /// <summary>Turns "Grass_Albedo_01" into "Grass".</summary>
    private static string Clean(string raw)
    {
        string s = raw;

        foreach (string junk in new[]
                 {
                     "_Albedo", "_albedo", "_BaseColor", "_basecolor", "_Diffuse", "_diffuse",
                     "_Color", "_color", "_Texture", "_tex", "_2K", "_1K", "_4K", "_diff",
                 })
            s = s.Replace(junk, "");

        s = s.Trim('_', '-', ' ');
        return string.IsNullOrWhiteSpace(s) ? raw : s;
    }

    private static Terrain GetTerrain()
    {
        var t = Terrain.activeTerrain ?? Object.FindFirstObjectByType<Terrain>();
        if (t == null || t.terrainData == null)
            EditorUtility.DisplayDialog("No terrain", "Could not find a Terrain in the open scene.", "OK");
        return t;
    }
}
