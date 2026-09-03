using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// Builds the start screen: black background, a copy of Ava turning slowly on
/// the left, Play and Quit on the right.
///
/// How it works, so the Hierarchy makes sense later:
///
///   StartStage          parked far above the island, out of everyone's way
///     MenuCamera        clears to solid black, sees ONLY the MenuStage layer,
///                       and draws on top of the game camera
///     KeyLight/FillLight  point lights, short range, so they cannot possibly
///                       spill onto the actual island
///     AvaDisplay        a stripped copy of the player with TurntableSpin
///
/// The whole StartStage switches off the instant she presses Play, so it costs
/// nothing while she is playing.
///
/// Tools > Birthday > Build Start Screen
/// </summary>
public static class BirthdayStartScreenBuilder
{
    private const string LayerName = "MenuStage";
    private const string StageName = "StartStage";
    private const string PanelName = "StartPanel";

    private static readonly Vector3 StageOrigin = new(0f, 3000f, 0f);

    private const float ButtonW = 340f;
    private const float ButtonH = 96f;
    private const float ButtonGap = 26f;
    private const float RightMargin = 130f;
    private const int SliceBorder = 40;

    [MenuItem("Tools/Birthday/Build Start Screen")]
    public static void Build()
    {
        if (TMP_Settings.instance == null)
        {
            EditorUtility.DisplayDialog("TextMeshPro not set up",
                "Window > TextMeshPro > Import TMP Essential Resources, then run this again.", "OK");
            return;
        }

        int layer = EnsureLayer(LayerName);
        if (layer < 0)
        {
            EditorUtility.DisplayDialog("No free layer",
                "All 32 Unity layers are used. Free one, or make a layer called 'MenuStage' by hand.", "OK");
            return;
        }

        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("No Canvas", "Run Tools > Birthday > Build UI first.", "OK");
            return;
        }

        var menus = Object.FindFirstObjectByType<GameMenus>(FindObjectsInactive.Include);
        if (menus == null)
        {
            EditorUtility.DisplayDialog("No GameMenus",
                "Could not find the GameMenus component. Run Tools > Birthday > Build UI first.", "OK");
            return;
        }

        var player = FindPlayer();

        var stage = BuildStage(layer, player, out bool gotCharacter);
        var panel = BuildPanel(canvas, out Button play, out Button quit, out TMP_Text title);

        Undo.RecordObject(menus, "Wire start screen");
        menus.startStage = stage;
        menus.startPanel = panel;
        menus.playButton = play;
        menus.quitButton = quit;
        menus.titleLabel = title;
        EditorUtility.SetDirty(menus);

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = stage;

        Debug.Log(
            $"<b>[Birthday]</b> Start screen built.\n" +
            $"  Layer '{LayerName}' = {layer}\n" +
            $"  Stage parked at {StageOrigin}\n" +
            $"  Character: {(gotCharacter ? "copied from your player" : "NOT FOUND, see below")}\n\n" +
            (gotCharacter
                ? "  To reposition her, select AvaDisplay under StartStage and move it in the Scene view. " +
                  "Left is negative X. Spin speed is on its TurntableSpin component.\n"
                : "  I could not find your player. Drag your character prefab in as a child of StartStage, " +
                  "set its layer to MenuStage (including children), and add a TurntableSpin component.\n") +
            "  Press Play to test. She should be lit, turning, and on the left, with the buttons on the right.");
    }

    // ===================================================================
    // Stage
    // ===================================================================

    private static GameObject BuildStage(int layer, GameObject player, out bool gotCharacter)
    {
        var old = GameObject.Find(StageName);
        if (old != null) Undo.DestroyObjectImmediate(old);

        var stage = new GameObject(StageName);
        stage.transform.position = StageOrigin;
        Undo.RegisterCreatedObjectUndo(stage, "Create start stage");

        // ---- camera
        var camGo = new GameObject("MenuCamera");
        camGo.transform.SetParent(stage.transform, false);
        camGo.transform.localPosition = new Vector3(0f, 0f, -4.2f);
        camGo.transform.localRotation = Quaternion.identity;
        camGo.layer = layer;

        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.cullingMask = 1 << layer;      // nothing but the stage
        cam.fieldOfView = 32f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 40f;
        cam.useOcclusionCulling = false;
        cam.allowHDR = false;

        // Higher depth means it draws after, and on top of, the gameplay camera.
        cam.depth = 100f;

        // No AudioListener here on purpose: two in a scene produces a warning and
        // unpredictable audio.

        // ---- lights
        // Point lights, not directional. A directional light would light the
        // entire island as a second sun even from 3000 units up.
        MakeLight(stage, layer, "KeyLight", new Vector3(-0.3f, 1.4f, -2.0f),
                  new Color(1f, 0.97f, 0.93f), 3.2f, 7f);
        MakeLight(stage, layer, "FillLight", new Vector3(-2.6f, 0.4f, -1.4f),
                  new Color(0.85f, 0.88f, 1f), 1.3f, 6f);
        MakeLight(stage, layer, "RimLight", new Vector3(-0.4f, 1.2f, 2.2f),
                  new Color(1f, 0.85f, 0.92f), 1.8f, 6f);

        // ---- character
        gotCharacter = false;

        if (player != null)
        {
            var display = Object.Instantiate(player);
            display.name = "AvaDisplay";

            if (PrefabUtility.IsPartOfPrefabInstance(display))
                PrefabUtility.UnpackPrefabInstance(display, PrefabUnpackMode.Completely,
                                                  InteractionMode.AutomatedAction);

            StripForDisplay(display);
            SetLayerRecursive(display, layer);

            display.transform.SetParent(stage.transform, false);

            // Camera sits at x = 0 looking forward, so negative X puts her left.
            display.transform.localPosition = new Vector3(-1.05f, -0.95f, 0f);
            display.transform.localRotation = Quaternion.Euler(0f, 20f, 0f);
            display.transform.localScale = Vector3.one;

            var spin = display.AddComponent<TurntableSpin>();
            spin.speed = 20f;
            spin.bobHeight = 0.03f;
            spin.bobSpeed = 0.45f;

            gotCharacter = true;
        }

        return stage;
    }

    private static void MakeLight(GameObject stage, int layer, string name,
                                  Vector3 localPos, Color color, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(stage.transform, false);
        go.transform.localPosition = localPos;
        go.layer = layer;

        var l = go.AddComponent<Light>();
        l.type = LightType.Point;          // range-limited, cannot reach the island
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;     // one less thing to go wrong in WebGL
        l.cullingMask = 1 << layer;
    }

    /// <summary>
    /// Turns a live player into a mannequin. Everything that moves it, collides,
    /// listens to input or makes noise has to go, or it will try to play the game
    /// on the start screen. The Animator stays, so her idle animation plays while
    /// she turns.
    /// </summary>
    private static void StripForDisplay(GameObject root)
    {
        foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            if (mb != null) Object.DestroyImmediate(mb);

        foreach (var c in root.GetComponentsInChildren<Collider>(true))
            if (c != null) Object.DestroyImmediate(c);

        foreach (var cc in root.GetComponentsInChildren<CharacterController>(true))
            if (cc != null) Object.DestroyImmediate(cc);

        foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true))
            if (rb != null) Object.DestroyImmediate(rb);

        foreach (var al in root.GetComponentsInChildren<AudioListener>(true))
            if (al != null) Object.DestroyImmediate(al);

        foreach (var a in root.GetComponentsInChildren<AudioSource>(true))
            if (a != null) Object.DestroyImmediate(a);

        foreach (var c in root.GetComponentsInChildren<Camera>(true))
            if (c != null) Object.DestroyImmediate(c.gameObject);

        foreach (var l in root.GetComponentsInChildren<Light>(true))
            if (l != null) Object.DestroyImmediate(l.gameObject);

        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            if (ps != null) Object.DestroyImmediate(ps.gameObject);
    }

    // ===================================================================
    // Panel
    // ===================================================================

    private static GameObject BuildPanel(Canvas canvas, out Button play, out Button quit, out TMP_Text title)
    {
        var old = canvas.transform.Find(PanelName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var sprite = ImportButtonSprite();

        var panel = NewUI(PanelName, canvas.transform);
        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        // Deliberately NO background image here. The MenuCamera supplies the black,
        // and a full-screen image on an overlay canvas would cover the character.

        // ---- title
        var titleGo = NewUI("Title", panel.transform);
        var tRt = titleGo.GetComponent<RectTransform>();
        tRt.anchorMin = tRt.anchorMax = new Vector2(1f, 0.5f);
        tRt.pivot = new Vector2(1f, 0.5f);
        tRt.anchoredPosition = new Vector2(-RightMargin, 190f);
        tRt.sizeDelta = new Vector2(680f, 120f);

        title = titleGo.AddComponent<TextMeshProUGUI>();
        title.text = "Happy Birthday, Ava";
        title.fontSize = 62;
        title.color = Color.white;
        title.alignment = TextAlignmentOptions.Right;
        title.raycastTarget = false;

        // ---- buttons
        float half = (ButtonH + ButtonGap) * 0.5f;
        play = MakeButton(panel.transform, sprite, "PlayButton", "Play", new Vector2(-RightMargin, half));
        quit = MakeButton(panel.transform, sprite, "QuitButton", "Quit", new Vector2(-RightMargin, -half));

        panel.transform.SetAsLastSibling();   // above the HUD and the speech bubble
        return panel;
    }

    private static Button MakeButton(Transform parent, Sprite sprite, string name, string label, Vector2 pos)
    {
        var go = NewUI(name, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(ButtonW, ButtonH);

        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Sliced;        // required, or the corners go oval
        img.pixelsPerUnitMultiplier = 1f;
        img.color = Color.white;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.ColorTint;

        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.87f, 0.93f);
        colors.pressedColor = new Color(0.90f, 0.74f, 0.82f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.5f);
        colors.fadeDuration = 0.09f;
        btn.colors = colors;

        var textGo = NewUI("Label", go.transform);
        var lRt = textGo.GetComponent<RectTransform>();
        lRt.anchorMin = Vector2.zero;
        lRt.anchorMax = Vector2.one;
        lRt.offsetMin = lRt.offsetMax = Vector2.zero;

        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 40;
        text.color = new Color(0.17f, 0.15f, 0.19f);
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;

        return btn;
    }

    // ===================================================================
    // Helpers
    // ===================================================================

    private static GameObject FindPlayer()
    {
        // Prefer the object that actually drives movement, whatever it is called.
        var byController = Object.FindObjectsByType<CharacterController>(FindObjectsSortMode.None)
            .FirstOrDefault(c => c.GetComponentInChildren<Animator>() != null);
        if (byController != null) return byController.gameObject;

        var tagged = GameObject.FindGameObjectWithTag("Player");
        if (tagged != null) return tagged;

        return null;
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform t in go.transform) SetLayerRecursive(t.gameObject, layer);
    }

    private static int EnsureLayer(string name)
    {
        int existing = LayerMask.NameToLayer(name);
        if (existing >= 0) return existing;

        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0) return -1;

        var so = new SerializedObject(assets[0]);
        var layers = so.FindProperty("layers");

        // 0 to 7 are Unity's built-ins and cannot be renamed.
        for (int i = 8; i < layers.arraySize; i++)
        {
            var slot = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(slot.stringValue)) continue;

            slot.stringValue = name;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            return i;
        }

        return -1;
    }

    private static Sprite ImportButtonSprite()
    {
        string guid = AssetDatabase.FindAssets("MenuButton t:Texture2D").FirstOrDefault();
        if (guid == null)
        {
            Debug.LogWarning("[Birthday] MenuButton.png not found. Buttons will use Unity's default sprite. " +
                             "Unzip the UIArt files into Assets/UIArt/ and run this again.");
            return null;
        }

        string path = AssetDatabase.GUIDToAssetPath(guid);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;

        if (importer != null)
        {
            var s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);

            s.textureType = TextureImporterType.Sprite;
            s.spriteMode = (int)SpriteImportMode.Single;
            s.spriteMeshType = SpriteMeshType.FullRect;          // needed for sliced
            s.spriteBorder = new Vector4(SliceBorder, SliceBorder, SliceBorder, SliceBorder);
            s.spritePixelsPerUnit = 100f;
            s.alphaSource = TextureImporterAlphaSource.FromInput;
            s.alphaIsTransparency = true;

            importer.SetTextureSettings(s);
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        return go;
    }
}
