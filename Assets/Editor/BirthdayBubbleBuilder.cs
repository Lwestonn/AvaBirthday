using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// Builds the Animal Crossing style speech bubble and wires it to the head.
///
/// Also fixes the sprite import settings for you, including the 9-slice border,
/// which is the part that is easy to miss and makes the bubble corners stretch
/// into ovals when wrong.
///
/// Tools > Birthday > Build Speech Bubble
/// </summary>
public static class BirthdayBubbleBuilder
{
    private const string BubbleName = "SpeechBubble";
    private const int SliceBorder = 56;   // matches the corner radius of the art

    [MenuItem("Tools/Birthday/Build Speech Bubble")]
    public static void BuildBubble()
    {
        if (TMP_Settings.instance == null)
        {
            EditorUtility.DisplayDialog("TextMeshPro not set up",
                "Window > TextMeshPro > Import TMP Essential Resources, then run this again.", "OK");
            return;
        }

        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("No Canvas",
                "Run Tools > Birthday > Build UI first.", "OK");
            return;
        }

        var bubbleSprite = ImportSprite("SpeechBubble", SliceBorder);
        var tailSprite = ImportSprite("SpeechBubbleTail", 0);

        var old = canvas.transform.Find(BubbleName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        // ---- root
        var root = NewUI(BubbleName, canvas.transform);
        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-60f, 40f);
        rt.sizeDelta = new Vector2(560f, 150f);

        var group = root.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        var bg = root.AddComponent<Image>();
        bg.sprite = bubbleSprite;
        bg.type = Image.Type.Sliced;      // required, or the corners distort
        bg.pixelsPerUnitMultiplier = 1f;
        bg.raycastTarget = false;
        bg.color = Color.white;

        // ---- tail, hanging off the bottom-left
        if (tailSprite != null)
        {
            var tailGo = NewUI("Tail", root.transform);
            var tailRt = tailGo.GetComponent<RectTransform>();
            tailRt.anchorMin = tailRt.anchorMax = new Vector2(0f, 0f);
            tailRt.pivot = new Vector2(0.5f, 1f);
            tailRt.anchoredPosition = new Vector2(96f, 6f);
            tailRt.sizeDelta = new Vector2(52f, 42f);

            var tailImg = tailGo.AddComponent<Image>();
            tailImg.sprite = tailSprite;
            tailImg.raycastTarget = false;

            tailGo.transform.SetAsFirstSibling();   // behind the text
        }

        // ---- text
        var textGo = NewUI("Text", root.transform);
        var tRt = textGo.GetComponent<RectTransform>();
        tRt.anchorMin = tRt.anchorMax = tRt.pivot = new Vector2(0.5f, 0.5f);
        tRt.anchoredPosition = Vector2.zero;
        tRt.sizeDelta = new Vector2(560f - 92f, 70f);

        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.text = "";
        text.fontSize = 34;
        text.color = new Color(0.08f, 0.07f, 0.09f);   // near-black, softer than pure
        text.alignment = TextAlignmentOptions.Left;
        text.enableWordWrapping = true;
        text.raycastTarget = false;

        // ---- audio
        var audio = root.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;      // 2D: the bubble is UI, not a thing in the world
        audio.volume = 1f;

        // ---- component
        var bubble = root.AddComponent<SpeechBubbleUI>();
        bubble.panel = rt;
        bubble.group = group;
        bubble.label = text;
        bubble.tail = tailSprite != null ? root.transform.Find("Tail") as RectTransform : null;
        bubble.blipSource = audio;
        bubble.blips = LoadBlips();

        // Sit below the menus so pausing covers it, above the HUD.
        root.transform.SetSiblingIndex(Mathf.Min(2, canvas.transform.childCount - 1));

        // ---- wire the head
        var barks = Object.FindFirstObjectByType<HeadBarks>(FindObjectsInactive.Include);
        if (barks != null)
        {
            Undo.RecordObject(barks, "Wire bubble");
            barks.bubble = bubble;
            EditorUtility.SetDirty(barks);
        }
        else
        {
            Debug.LogWarning("[Birthday] No HeadBarks found. Assign the bubble to it manually.");
        }

        Selection.activeGameObject = root;
        Debug.Log($"<b>[Birthday]</b> Speech bubble built on the right, {bubble.blips.Length} blip(s) loaded. " +
                  $"The old floating world label is now bypassed automatically.");
    }

    /// <summary>
    /// Imports as a Single sprite and sets the 9-slice border. Without the border
    /// the rounded corners get stretched into ovals when the bubble resizes.
    /// </summary>
    private static Sprite ImportSprite(string name, int border)
    {
        string guid = AssetDatabase.FindAssets($"{name} t:Texture2D").FirstOrDefault();
        if (guid == null)
        {
            Debug.LogWarning($"[Birthday] Could not find '{name}'. Did UIArt get unzipped into Assets?");
            return null;
        }

        string path = AssetDatabase.GUIDToAssetPath(guid);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;

        if (importer != null)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spriteMeshType = SpriteMeshType.FullRect;   // needed for sliced
            settings.spriteBorder = new Vector4(border, border, border, border);
            settings.spritePixelsPerUnit = 100f;
            settings.alphaSource = TextureImporterAlphaSource.FromInput;
            settings.alphaIsTransparency = true;

            importer.SetTextureSettings(settings);
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static AudioClip[] LoadBlips()
    {
        return AssetDatabase.FindAssets("Blip_ t:AudioClip")
            .Select(g => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(c => c != null)
            .OrderBy(c => c.name)
            .ToArray();
    }

    private static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        return go;
    }
}
