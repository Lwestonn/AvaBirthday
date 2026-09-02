using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Scatters grass across the terrain automatically, using the texture you
/// already painted as the mask. Wherever your grass terrain layer is painted and
/// the ground is not too steep, grass appears. Everywhere else it does not.
///
/// Two modes:
///
///   Terrain Details   The right answer for grass. Unity renders these as a
///                     special batched system, so tens of thousands of tufts
///                     cost almost nothing. They also fade with distance for
///                     free. Use this.
///
///   Prefab Instances  Real GameObjects parented under one holder. Slower and
///                     capped much lower, but it always renders no matter what,
///                     and you can hand-move individual pieces afterwards. Good
///                     fallback, and good for larger plants like ferns.
///
/// Tools > Birthday > Scatter Grass
/// </summary>
public class BirthdayGrassScatter : EditorWindow
{
    private enum Mode { TerrainDetails, PrefabInstances }

    private const string HolderName = "GrassScatter";

    // ---- shared
    private Terrain _terrain;
    private Mode _mode = Mode.TerrainDetails;
    private bool[] _layerMask = new bool[0];
    private float _threshold = 0.45f;
    private float _maxSlope = 32f;
    private float _minHeight = 0f;
    private float _patchScale = 0.035f;
    private float _patchStrength = 0.55f;
    private int _seed = 12345;

    // ---- detail mode
    private GameObject _detailPrefab;
    private Texture2D _detailTexture;
    private bool _useBillboard;
    private int _maxDensity = 5;
    private float _minSize = 0.7f;
    private float _maxSize = 1.4f;
    private Color _healthy = new(0.55f, 0.72f, 0.36f);
    private Color _dry = new(0.72f, 0.70f, 0.38f);
    private bool _swapAxes;
    private float _detailDistance = 160f;
    private bool _useInstancing;

    // ---- prefab mode
    private GameObject _grassPrefab;
    private float _spacing = 2.2f;
    private float _jitter = 0.8f;
    private int _maxInstances = 4000;
    private Vector2 _scaleRange = new(0.8f, 1.35f);
    private bool _alignToNormal;

    private Vector2 _scroll;

    [MenuItem("Tools/Birthday/Scatter Grass")]
    public static void Open()
    {
        var w = GetWindow<BirthdayGrassScatter>(true, "Scatter Grass", true);
        w.minSize = new Vector2(400f, 560f);
        w.AutoFindTerrain();
        w.Show();
    }

    private void AutoFindTerrain()
    {
        if (_terrain == null) _terrain = Terrain.activeTerrain;
        if (_terrain == null) _terrain = FindFirstObjectByType<Terrain>();
        SyncLayerMask();
    }

    private void SyncLayerMask()
    {
        int n = _terrain != null && _terrain.terrainData != null
            ? _terrain.terrainData.terrainLayers.Length
            : 0;

        if (_layerMask.Length == n) return;

        var old = _layerMask;
        _layerMask = new bool[n];
        for (int i = 0; i < n && i < old.Length; i++) _layerMask[i] = old[i];

        // First run: guess anything with "grass" in the name.
        if (old.Length == 0 && _terrain != null)
        {
            var layers = _terrain.terrainData.terrainLayers;
            for (int i = 0; i < n; i++)
                if (layers[i] != null && layers[i].name.ToLowerInvariant().Contains("grass"))
                    _layerMask[i] = true;
        }
    }

    // ===================================================================
    // GUI
    // ===================================================================

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUI.BeginChangeCheck();
        _terrain = (Terrain)EditorGUILayout.ObjectField("Terrain", _terrain, typeof(Terrain), true);
        if (EditorGUI.EndChangeCheck()) { _layerMask = new bool[0]; SyncLayerMask(); }

        if (_terrain == null || _terrain.terrainData == null)
        {
            EditorGUILayout.HelpBox("No terrain assigned. Drag your Terrain object in above.", MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }

        SyncLayerMask();

        EditorGUILayout.Space();
        _mode = (Mode)EditorGUILayout.EnumPopup("Mode", _mode);

        EditorGUILayout.HelpBox(
            _mode == Mode.TerrainDetails
                ? "Terrain Details: cheap, batched, fades with distance. Use this for grass."
                : "Prefab Instances: real objects you can hand-edit. Much heavier, so keep the count low. Good for ferns and bigger plants.",
            MessageType.None);

        // ---- where
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Where grass is allowed", EditorStyles.boldLabel);

        var layers = _terrain.terrainData.terrainLayers;
        if (layers.Length == 0)
        {
            EditorGUILayout.HelpBox("This terrain has no layers painted yet.", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.LabelField("Grass grows on these painted layers:");
            for (int i = 0; i < layers.Length; i++)
            {
                string nm = layers[i] != null ? layers[i].name : $"(missing layer {i})";
                _layerMask[i] = EditorGUILayout.ToggleLeft($"    {nm}", _layerMask[i]);
            }
        }

        _threshold = EditorGUILayout.Slider(
            new GUIContent("Paint threshold", "How strongly the layer must be painted before grass appears. Lower = grass creeps into blended edges."),
            _threshold, 0.05f, 0.95f);

        _maxSlope = EditorGUILayout.Slider(
            new GUIContent("Max slope", "Degrees. Keeps grass off cliffs, which is the main thing that makes auto-scattered grass look fake."),
            _maxSlope, 0f, 90f);

        _minHeight = EditorGUILayout.FloatField(
            new GUIContent("Min world height", "Skip anything below this Y, so grass does not grow underwater."),
            _minHeight);

        // ---- patchiness
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Patchiness", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Uniform grass reads as a lawn. Patchiness breaks it into clumps and thin spots, which is what makes it look natural.", MessageType.None);

        _patchStrength = EditorGUILayout.Slider("Amount", _patchStrength, 0f, 1f);
        _patchScale = EditorGUILayout.Slider(
            new GUIContent("Patch size", "Lower = big slow patches. Higher = small speckly ones."),
            _patchScale, 0.005f, 0.15f);
        _seed = EditorGUILayout.IntField("Seed", _seed);

        // ---- mode specific
        EditorGUILayout.Space();

        if (_mode == Mode.TerrainDetails) DrawDetailOptions();
        else DrawPrefabOptions();

        // ---- buttons
        EditorGUILayout.Space();
        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(!CanScatter()))
        {
            if (GUILayout.Button("Scatter Grass", GUILayout.Height(34f)))
            {
                if (_mode == Mode.TerrainDetails) ScatterDetails();
                else ScatterPrefabs();
            }
        }

        if (!CanScatter())
            EditorGUILayout.HelpBox(MissingReason(), MessageType.Warning);

        EditorGUILayout.Space();

        if (GUILayout.Button(_mode == Mode.TerrainDetails ? "Clear This Detail Layer" : "Clear Scattered Prefabs"))
        {
            if (_mode == Mode.TerrainDetails) ClearDetails();
            else ClearPrefabs();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawDetailOptions()
    {
        DrawHealthChecks();

        EditorGUILayout.LabelField("What to scatter", EditorStyles.boldLabel);

        _useBillboard = EditorGUILayout.Toggle(
            new GUIContent("Billboard texture", "On: a flat always-facing-camera quad from a texture. Off: a real 3D grass mesh prefab. Meshes look better in a third person game."),
            _useBillboard);

        if (_useBillboard)
            _detailTexture = (Texture2D)EditorGUILayout.ObjectField("Grass texture", _detailTexture, typeof(Texture2D), false);
        else
            _detailPrefab = (GameObject)EditorGUILayout.ObjectField("Grass mesh prefab", _detailPrefab, typeof(GameObject), false);

        EditorGUILayout.Space();
        _maxDensity = EditorGUILayout.IntSlider(
            new GUIContent("Density", "Tufts per detail cell at full coverage. 4 to 6 is usually plenty."),
            _maxDensity, 1, 16);

        _minSize = EditorGUILayout.Slider("Min size", _minSize, 0.1f, 3f);
        _maxSize = EditorGUILayout.Slider("Max size", _maxSize, 0.1f, 4f);
        if (_maxSize < _minSize) _maxSize = _minSize;

        _healthy = EditorGUILayout.ColorField("Healthy tint", _healthy);
        _dry = EditorGUILayout.ColorField("Dry tint", _dry);

        EditorGUILayout.Space();
        _detailDistance = EditorGUILayout.Slider(
            new GUIContent("Draw distance", "How far grass is visible. Lower is much faster in WebGL."),
            _detailDistance, 20f, 250f);

        _useInstancing = EditorGUILayout.Toggle(
            new GUIContent("GPU instancing", "Faster, but on URP some detail meshes render as nothing at all with it on. Leave off until you can see grass, then try turning it on."),
            _useInstancing);

        _swapAxes = EditorGUILayout.Toggle(
            new GUIContent("Swap X/Z", "Tick this ONLY if the grass comes out rotated 90 degrees compared to where you painted. Unity's detail map indexing is inconsistent across versions."),
            _swapAxes);

        EditorGUILayout.Space();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Diagnose")) Diagnose();
            if (GUILayout.Button("Preview Mask PNG")) ExportMaskPreview();
        }
    }

    /// <summary>
    /// Writes a top-down picture of exactly where grass will and will not go, and
    /// why each spot was rejected. Far faster than scattering, looking at the
    /// island, and guessing.
    /// </summary>
    private void ExportMaskPreview()
    {
        var td = _terrain.terrainData;
        float[,,] alpha = td.GetAlphamaps(0, 0, td.alphamapWidth, td.alphamapHeight);

        const int res = 512;
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        var px = new Color32[res * res];

        float baseY = _terrain.transform.position.y;
        int pass = 0, byLayer = 0, bySlope = 0, byHeight = 0, byPatch = 0;

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float nx = (x + 0.5f) / res;
                float nz = (y + 0.5f) / res;

                Color c;
                float weight = SampleLayers(alpha, td, nx, nz);

                if (weight < _threshold)
                {
                    c = new Color(0.13f, 0.13f, 0.15f);      // not a grass layer
                    byLayer++;
                }
                else if (td.GetSteepness(nx, nz) > _maxSlope)
                {
                    c = new Color(0.85f, 0.25f, 0.20f);      // too steep
                    bySlope++;
                }
                else if (baseY + td.GetInterpolatedHeight(nx, nz) < _minHeight)
                {
                    c = new Color(0.20f, 0.40f, 0.95f);      // below min height
                    byHeight++;
                }
                else
                {
                    int density = Mathf.RoundToInt(_maxDensity * Mathf.Clamp01(weight) * Patch(nx, nz));

                    if (density <= 0)
                    {
                        c = new Color(0.45f, 0.42f, 0.20f);  // patchiness thinned it to zero
                        byPatch++;
                    }
                    else
                    {
                        float k = density / (float)_maxDensity;
                        c = Color.Lerp(new Color(0.25f, 0.45f, 0.2f), new Color(0.45f, 1f, 0.35f), k);
                        pass++;
                    }
                }

                px[y * res + x] = c;
            }
        }

        tex.SetPixels32(px);
        tex.Apply();

        string path = "Assets/_GrassMaskPreview.png";
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        DestroyImmediate(tex);
        AssetDatabase.Refresh();

        float total = res * res;
        Debug.Log(
            $"<b>[Birthday] Grass mask preview</b>  {path}\n" +
            $"  (north / +Z is up, same as looking straight down in the Scene view)\n\n" +
            $"    <b>GREEN</b>   grass goes here          {pass / total * 100f,5:0.0}%\n" +
            $"    DARK    not a ticked layer         {byLayer / total * 100f,5:0.0}%\n" +
            $"    RED     too steep (over {_maxSlope:0} deg)   {bySlope / total * 100f,5:0.0}%\n" +
            $"    BLUE    below min height {_minHeight:0.0}       {byHeight / total * 100f,5:0.0}%\n" +
            $"    OLIVE   thinned out by patchiness  {byPatch / total * 100f,5:0.0}%\n\n" +
            $"  Compare the green against your island. If green is in the wrong places, the ticked " +
            $"layers are wrong: run Tools > Birthday > Terrain > Export Layer Map PNG to see which " +
            $"layer is actually where.");

        var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }

    /// <summary>
    /// The three settings that silently produce zero visible grass, checked up
    /// front with one-click fixes, because "it says it worked and I see nothing"
    /// is a miserable thing to debug by hand.
    /// </summary>
    private void DrawHealthChecks()
    {
        var td = _terrain.terrainData;

        // ---- 1. detail resolution vs terrain size
        float cell = td.size.x / Mathf.Max(1, td.detailWidth);
        int wanted = RecommendedResolution(td);

        if (cell < 0.25f)
        {
            EditorGUILayout.HelpBox(
                $"Detail Resolution is {td.detailWidth} on a {td.size.x:0}m terrain, so each detail cell is only " +
                $"{cell * 100f:0}cm across. That is far too fine: you get hundreds of thousands of tufts, " +
                $"which murders performance and can render as nothing at all.\n\n" +
                $"Recommended: {wanted}.",
                MessageType.Error);

            if (GUILayout.Button($"Set Detail Resolution to {wanted}  (clears existing grass)"))
            {
                Undo.RegisterCompleteObjectUndo(td, "Detail resolution");
                td.SetDetailResolution(wanted, 16);
                EditorUtility.SetDirty(td);
                Debug.Log($"<b>[Birthday]</b> Detail resolution set to {wanted} " +
                          $"({td.size.x / wanted:0.00}m cells). Scatter again.");
            }
            EditorGUILayout.Space();
        }

#if UNITY_2022_2_OR_NEWER
        // ---- 2. scatter mode
        if (td.detailScatterMode != DetailScatterMode.InstanceCountMode)
        {
            EditorGUILayout.HelpBox(
                "Detail Scatter Mode is Coverage Mode. In that mode the density number means " +
                "'percent of ground covered' out of 255, not 'how many tufts', so a density of 5 " +
                "gives you about 2 percent coverage and looks like nothing happened.\n\n" +
                "Instance Count Mode is what you want.",
                MessageType.Error);

            if (GUILayout.Button("Switch to Instance Count Mode  (clears existing grass)"))
            {
                Undo.RegisterCompleteObjectUndo(td, "Scatter mode");
                td.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
                EditorUtility.SetDirty(td);
                Debug.Log("<b>[Birthday]</b> Switched to Instance Count Mode. Scatter again.");
            }
            EditorGUILayout.Space();
        }
#endif

        // ---- 3. draw distance
        if (_terrain.detailObjectDistance < 60f)
        {
            EditorGUILayout.HelpBox(
                $"Terrain Detail Distance is {_terrain.detailObjectDistance:0}m. Grass only draws within that " +
                $"range of the camera, so if your Scene view is further out than that you will see none of it. " +
                $"Fly down to ground level to check, or raise the Draw distance below.",
                MessageType.Warning);
            EditorGUILayout.Space();
        }

        // ---- 4. the prefab itself
        if (!_useBillboard && _detailPrefab != null && !HasRootMesh(_detailPrefab))
        {
            EditorGUILayout.HelpBox(
                $"'{_detailPrefab.name}' has no MeshFilter and MeshRenderer on its ROOT object. Unity's detail " +
                $"system only renders the root mesh, so a model with its mesh tucked in a child draws nothing.\n\n" +
                $"Expand the model in the Project window and drag the child that actually has the mesh into the " +
                $"slot instead, or use Prefab Instances mode.",
                MessageType.Error);
            EditorGUILayout.Space();
        }
    }

    private static bool HasRootMesh(GameObject go)
    {
        var mf = go.GetComponent<MeshFilter>();
        return mf != null && mf.sharedMesh != null && go.GetComponent<MeshRenderer>() != null;
    }

    private static int RecommendedResolution(TerrainData td)
    {
        // Aim for detail cells roughly half a metre across.
        int r = Mathf.NextPowerOfTwo(Mathf.RoundToInt(td.size.x / 0.5f));
        return Mathf.Clamp(r, 32, 1024);
    }

    private void Diagnose()
    {
        var td = _terrain.terrainData;
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("<b>[Birthday] Grass diagnosis</b>\n");
        sb.AppendLine($"  Terrain size      {td.size.x:0} x {td.size.z:0} m");
        sb.AppendLine($"  Detail resolution {td.detailWidth} x {td.detailHeight}  " +
                      $"({td.size.x / Mathf.Max(1, td.detailWidth):0.00} m per cell, recommended {RecommendedResolution(td)})");
#if UNITY_2022_2_OR_NEWER
        sb.AppendLine($"  Scatter mode      {td.detailScatterMode}" +
                      (td.detailScatterMode == DetailScatterMode.InstanceCountMode ? "" : "   <- this is why you see nothing"));
#endif
        sb.AppendLine($"  Draw foliage      {_terrain.drawTreesAndFoliage}");
        sb.AppendLine($"  Detail distance   {_terrain.detailObjectDistance:0} m  (grass only draws within this of the camera)");
        sb.AppendLine($"  Detail density    {_terrain.detailObjectDensity:0.00}");

        var protos = td.detailPrototypes;
        sb.AppendLine($"\n  {protos.Length} prototype(s):");

        for (int i = 0; i < protos.Length; i++)
        {
            var p = protos[i];
            string what = p.usePrototypeMesh
                ? $"mesh '{(p.prototype != null ? p.prototype.name : "NONE")}'"
                : $"texture '{(p.prototypeTexture != null ? p.prototypeTexture.name : "NONE")}'";

            long sum = 0;
            int nonZero = 0;
            var map = td.GetDetailLayer(0, 0, td.detailWidth, td.detailHeight, i);
            foreach (int v in map) { sum += v; if (v > 0) nonZero++; }

            sb.AppendLine($"    {i}. {what}");
            sb.AppendLine($"       render mode {p.renderMode}, instancing {p.useInstancing}");
            sb.AppendLine($"       size {p.minWidth:0.0}-{p.maxWidth:0.0} wide, {p.minHeight:0.0}-{p.maxHeight:0.0} tall");
            sb.AppendLine($"       map: {nonZero:N0} cells written, {sum:N0} total");

            if (p.usePrototypeMesh && p.prototype != null && !HasRootMesh(p.prototype))
                sb.AppendLine($"       PROBLEM: no MeshFilter/MeshRenderer on the root of this model");

            if (sum == 0)
                sb.AppendLine($"       PROBLEM: nothing written to this layer");
        }

        Debug.Log(sb.ToString());
    }

    private void DrawPrefabOptions()
    {
        EditorGUILayout.LabelField("What to scatter", EditorStyles.boldLabel);
        _grassPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab", _grassPrefab, typeof(GameObject), false);

        EditorGUILayout.Space();
        _spacing = EditorGUILayout.Slider(
            new GUIContent("Spacing", "Metres between candidate positions."),
            _spacing, 0.4f, 12f);

        _jitter = EditorGUILayout.Slider(
            new GUIContent("Jitter", "Random offset, as a fraction of spacing. Without this you get visible grid rows."),
            _jitter, 0f, 1f);

        _scaleRange = EditorGUILayout.Vector2Field("Scale range", _scaleRange);

        _alignToNormal = EditorGUILayout.Toggle(
            new GUIContent("Tilt with the ground", "Leans plants with the slope. Looks good on hills, odd on flat ground."),
            _alignToNormal);

        _maxInstances = EditorGUILayout.IntField(
            new GUIContent("Hard cap", "Stops before this many, so you cannot accidentally spawn 200,000 objects."),
            _maxInstances);
    }

    private bool CanScatter()
    {
        if (_terrain == null || _terrain.terrainData == null) return false;
        if (!_layerMask.Any(b => b)) return false;
        if (_mode == Mode.TerrainDetails)
            return _useBillboard ? _detailTexture != null : _detailPrefab != null;
        return _grassPrefab != null;
    }

    private string MissingReason()
    {
        if (!_layerMask.Any(b => b)) return "Tick at least one terrain layer above, so the tool knows where your grass is painted.";
        if (_mode == Mode.TerrainDetails)
            return _useBillboard ? "Assign a grass texture." : "Assign a grass mesh prefab.";
        return "Assign a prefab.";
    }

    // ===================================================================
    // Terrain details
    // ===================================================================

    private void ScatterDetails()
    {
        var td = _terrain.terrainData;

#if UNITY_2022_2_OR_NEWER
        // Coverage Mode reinterprets the density number as a percentage out of 255,
        // so a density of 5 covers 2 percent of the ground and reads as "nothing
        // happened". Force the mode that means what it says. This clears detail
        // data, so it has to happen before anything is written.
        if (td.detailScatterMode != DetailScatterMode.InstanceCountMode)
        {
            td.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
            Debug.Log("<b>[Birthday]</b> Switched the terrain to Instance Count Mode first.");
        }
#endif

        int layerIndex = EnsurePrototype(td);
        if (layerIndex < 0) return;

        int dw = td.detailWidth, dh = td.detailHeight;

        // A 120m terrain at 1024 resolution gives 12cm cells, and a density of 5
        // per cell then means three quarters of a million tufts. Refuse rather
        // than let someone wonder why the editor locked up.
        float cellSize = td.size.x / Mathf.Max(1, dw);
        if (cellSize < 0.25f)
        {
            int wanted = RecommendedResolution(td);
            if (!EditorUtility.DisplayDialog("Detail resolution is very high",
                    $"Each detail cell is only {cellSize * 100f:0}cm across, so this will place " +
                    $"hundreds of thousands of tufts.\n\n" +
                    $"Set Detail Resolution to {wanted} first (there is a button for it at the top " +
                    $"of this window).",
                    "Scatter anyway", "Stop"))
                return;
        }
        if (dw <= 0 || dh <= 0)
        {
            EditorUtility.DisplayDialog("No detail resolution",
                "This terrain has a detail resolution of zero.\n\n" +
                "Terrain Inspector > gear icon > Detail Resolution. Try 1024 with 16 per patch.",
                "OK");
            return;
        }

        float[,,] alpha = td.GetAlphamaps(0, 0, td.alphamapWidth, td.alphamapHeight);
        var map = new int[dw, dh];

        Undo.RegisterCompleteObjectUndo(td, "Scatter grass");

        float baseY = _terrain.transform.position.y;
        int placed = 0;

        for (int i = 0; i < dw; i++)
        {
            for (int j = 0; j < dh; j++)
            {
                // i runs along one axis of the detail map, j the other. Which of
                // them is world X is the one thing Unity is inconsistent about
                // between versions, hence the Swap X/Z toggle.
                float fi = (i + 0.5f) / dw;
                float fj = (j + 0.5f) / dh;

                float nx = _swapAxes ? fj : fi;
                float nz = _swapAxes ? fi : fj;

                float weight = SampleLayers(alpha, td, nx, nz);
                if (weight < _threshold) continue;

                if (td.GetSteepness(nx, nz) > _maxSlope) continue;
                if (baseY + td.GetInterpolatedHeight(nx, nz) < _minHeight) continue;

                float patch = Patch(nx, nz);
                int density = Mathf.RoundToInt(_maxDensity * Mathf.Clamp01(weight) * patch);

                if (density <= 0) continue;

                map[i, j] = density;
                placed += density;
            }
        }

        td.SetDetailLayer(0, 0, layerIndex, map);

        _terrain.detailObjectDistance = _detailDistance;
        _terrain.detailObjectDensity = 1f;
        _terrain.drawTreesAndFoliage = true;

        EditorUtility.SetDirty(td);
        EditorUtility.SetDirty(_terrain);

        Debug.Log($"<b>[Birthday]</b> Scattered roughly <b>{placed:N0}</b> grass tufts across " +
                  $"{dw}x{dh} detail cells, draw distance {_detailDistance:0}m.\n" +
                  $"If the grass came out rotated 90 degrees from where you painted, tick Swap X/Z and run it again.");
    }

    /// <summary>Adds the grass prototype to the terrain if it is not already there.</summary>
    private int EnsurePrototype(TerrainData td)
    {
        var protos = td.detailPrototypes.ToList();

        for (int i = 0; i < protos.Count; i++)
        {
            if (!_useBillboard && protos[i].usePrototypeMesh && protos[i].prototype == _detailPrefab) return i;
            if (_useBillboard && !protos[i].usePrototypeMesh && protos[i].prototypeTexture == _detailTexture) return i;
        }

        var p = new DetailPrototype
        {
            minWidth = _minSize,
            maxWidth = _maxSize,
            minHeight = _minSize,
            maxHeight = _maxSize,
            healthyColor = _healthy,
            dryColor = _dry,
            noiseSpread = 0.4f,
        };

        if (_useBillboard)
        {
            p.usePrototypeMesh = false;
            p.prototypeTexture = _detailTexture;
            p.renderMode = DetailRenderMode.GrassBillboard;
        }
        else
        {
            p.usePrototypeMesh = true;
            p.prototype = _detailPrefab;

            // VertexLit is the mode that supports GPU instancing. Instancing is
            // faster, but on URP some meshes render as nothing with it enabled,
            // so it is off by default: see it working first, optimise second.
            p.renderMode = DetailRenderMode.VertexLit;
            p.useInstancing = _useInstancing;
        }

        protos.Add(p);
        td.detailPrototypes = protos.ToArray();

        return protos.Count - 1;
    }

    private void ClearDetails()
    {
        var td = _terrain.terrainData;
        if (td.detailPrototypes.Length == 0) return;

        Undo.RegisterCompleteObjectUndo(td, "Clear grass");

        var empty = new int[td.detailWidth, td.detailHeight];
        for (int i = 0; i < td.detailPrototypes.Length; i++)
            td.SetDetailLayer(0, 0, i, empty);

        EditorUtility.SetDirty(td);
        Debug.Log("<b>[Birthday]</b> Cleared every detail layer on the terrain.");
    }

    // ===================================================================
    // Prefab instances
    // ===================================================================

    private void ScatterPrefabs()
    {
        var td = _terrain.terrainData;

        var holder = GameObject.Find(HolderName);
        if (holder == null)
        {
            holder = new GameObject(HolderName);
            Undo.RegisterCreatedObjectUndo(holder, "Create grass holder");
        }

        float[,,] alpha = td.GetAlphamaps(0, 0, td.alphamapWidth, td.alphamapHeight);

        Vector3 origin = _terrain.transform.position;
        Vector3 size = td.size;

        var rng = new System.Random(_seed);
        int placed = 0;
        int stepsX = Mathf.Max(1, Mathf.FloorToInt(size.x / _spacing));
        int stepsZ = Mathf.Max(1, Mathf.FloorToInt(size.z / _spacing));

        try
        {
            for (int ix = 0; ix < stepsX && placed < _maxInstances; ix++)
            {
                if (ix % 8 == 0 && EditorUtility.DisplayCancelableProgressBar(
                        "Scattering grass", $"{placed:N0} placed", ix / (float)stepsX))
                    break;

                for (int iz = 0; iz < stepsZ && placed < _maxInstances; iz++)
                {
                    float jx = ((float)rng.NextDouble() - 0.5f) * _jitter * _spacing;
                    float jz = ((float)rng.NextDouble() - 0.5f) * _jitter * _spacing;

                    float wx = origin.x + ix * _spacing + _spacing * 0.5f + jx;
                    float wz = origin.z + iz * _spacing + _spacing * 0.5f + jz;

                    float nx = Mathf.Clamp01((wx - origin.x) / size.x);
                    float nz = Mathf.Clamp01((wz - origin.z) / size.z);

                    float weight = SampleLayers(alpha, td, nx, nz);
                    if (weight < _threshold) continue;
                    if (td.GetSteepness(nx, nz) > _maxSlope) continue;

                    float y = _terrain.SampleHeight(new Vector3(wx, 0f, wz)) + origin.y;
                    if (y < _minHeight) continue;

                    if (rng.NextDouble() > Patch(nx, nz) * weight) continue;

                    var go = (GameObject)PrefabUtility.InstantiatePrefab(_grassPrefab, holder.transform);
                    if (go == null) continue;

                    Quaternion rot = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                    if (_alignToNormal)
                    {
                        Vector3 n = td.GetInterpolatedNormal(nx, nz);
                        rot = Quaternion.FromToRotation(Vector3.up, n) * rot;
                    }

                    go.transform.SetPositionAndRotation(new Vector3(wx, y, wz), rot);

                    float s = Mathf.Lerp(_scaleRange.x, _scaleRange.y, (float)rng.NextDouble());
                    go.transform.localScale = Vector3.one * s;

                    GameObjectUtility.SetStaticEditorFlags(go,
                        StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);

                    Undo.RegisterCreatedObjectUndo(go, "Scatter grass");
                    placed++;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Debug.Log($"<b>[Birthday]</b> Placed <b>{placed:N0}</b> prefab(s) under '{HolderName}'." +
                  (placed >= _maxInstances ? "  Hit the hard cap, so raise it or increase spacing." : ""));
    }

    private void ClearPrefabs()
    {
        var holder = GameObject.Find(HolderName);
        if (holder == null) return;

        int n = holder.transform.childCount;
        Undo.DestroyObjectImmediate(holder);
        Debug.Log($"<b>[Birthday]</b> Removed '{HolderName}' and its {n} child object(s).");
    }

    // ===================================================================
    // Sampling
    // ===================================================================

    /// <summary>Total painted weight of the ticked terrain layers at a normalised position.</summary>
    private float SampleLayers(float[,,] alpha, TerrainData td, float nx, float nz)
    {
        int ax = Mathf.Clamp(Mathf.RoundToInt(nx * (td.alphamapWidth - 1)), 0, td.alphamapWidth - 1);
        int az = Mathf.Clamp(Mathf.RoundToInt(nz * (td.alphamapHeight - 1)), 0, td.alphamapHeight - 1);

        float sum = 0f;
        int layers = Mathf.Min(_layerMask.Length, alpha.GetLength(2));

        // Alphamaps are indexed [y, x, layer], where y is the world Z axis.
        for (int l = 0; l < layers; l++)
            if (_layerMask[l]) sum += alpha[az, ax, l];

        return Mathf.Clamp01(sum);
    }

    /// <summary>Perlin clumping, so the grass is not a uniform carpet.</summary>
    private float Patch(float nx, float nz)
    {
        if (_patchStrength <= 0f) return 1f;

        float f = 1f / Mathf.Max(0.0001f, _patchScale);
        float n = Mathf.PerlinNoise(nx * f + _seed * 0.017f, nz * f + _seed * 0.031f);

        return Mathf.Clamp01(Mathf.Lerp(1f, n * 1.4f, _patchStrength));
    }
}
