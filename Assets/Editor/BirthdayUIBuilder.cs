using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.Events;
using TMPro;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// One-click builder for the whole game UI: HUD, throw meter, memory photo card,
/// start screen and pause menu. Also creates and wires the manager components.
///
/// MUST live in Assets/Editor/.
///
/// Tools > Birthday > Build UI       rebuilds all UI (loses manual styling)
/// Tools > Birthday > Rewire Events  reconnects logic, leaves your UI alone
/// </summary>
public static class BirthdayUIBuilder
{
    private const string HudName    = "HUD_Counter";
    private const string ChargeName = "ChargeBar";
    private const string CardName   = "MemoryCard";
    private const string StartName  = "StartPanel";
    private const string PauseName  = "PausePanel";

    // Objects from the older modal-panel design, removed on rebuild.
    private static readonly string[] Legacy = { "MemoryPanel", "FadeImage", "FinalePanel" };

    private static readonly Color Pink   = new(0.98f, 0.72f, 0.82f);
    private static readonly Color Ink    = new(0.18f, 0.08f, 0.12f);
    private static readonly Color Cream  = new(1f, 0.95f, 0.97f);

    [MenuItem("Tools/Birthday/Build UI")]
    public static void BuildUI()
    {
        if (TMP_Settings.instance == null)
        {
            EditorUtility.DisplayDialog("TextMeshPro not set up",
                "Window > TextMeshPro > Import TMP Essential Resources, then run this again.", "OK");
            return;
        }

        Canvas canvas = EnsureCanvas();
        EnsureEventSystem();

        foreach (var n in Legacy) DestroyChild(canvas.transform, n);
        DestroyChild(canvas.transform, HudName);
        DestroyChild(canvas.transform, ChargeName);
        DestroyChild(canvas.transform, CardName);
        DestroyChild(canvas.transform, StartName);
        DestroyChild(canvas.transform, PauseName);

        // Sibling order is render order. Menus must be last so they sit on top.
        BuildHud(canvas.transform);
        BuildChargeBar(canvas.transform);
        BuildMemoryCard(canvas.transform);
        BuildStartPanel(canvas.transform);
        BuildPausePanel(canvas.transform);

        RewireEvents();
        Selection.activeGameObject = canvas.gameObject;
    }

    [MenuItem("Tools/Birthday/Rewire Events")]
    public static void RewireEvents()
    {
        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) { Debug.LogError("[Birthday] No Canvas. Run Build UI first."); return; }

        var manager = Object.FindFirstObjectByType<MemoryManager>();
        if (manager == null)
        {
            Debug.LogWarning("[Birthday] No MemoryManager found. Create an empty GameObject named 'GameManager', add MemoryManager, then run this again.");
            return;
        }

        var go = manager.gameObject;

        var narrator = Ensure<MemoryNarrator>(go);
        var menus    = Ensure<GameMenus>(go);

        var hud   = Object.FindFirstObjectByType<HudCounter>(FindObjectsInactive.Include);
        var card  = Object.FindFirstObjectByType<MemoryCardUI>(FindObjectsInactive.Include);
        var body  = Object.FindFirstObjectByType<BodyReattach>(FindObjectsInactive.Include);
        var locker = Object.FindFirstObjectByType<PlayerControlLock>(FindObjectsInactive.Include);
        var barks = Object.FindFirstObjectByType<HeadBarks>(FindObjectsInactive.Include);

        // --- narrator
        Undo.RecordObject(narrator, "Wire narrator");
        narrator.card = card;
        narrator.head = barks;

        // --- manager
        Undo.RecordObject(manager, "Wire manager");
        manager.narrator = narrator;
        manager.playerLock = locker;

        RemoveListenersTargeting(manager.onProgressChanged, hud);
        if (hud != null)
            UnityEventTools.AddPersistentListener(manager.onProgressChanged, new UnityAction<int, int>(hud.SetProgress));

        // --- menus
        Undo.RecordObject(menus, "Wire menus");
        menus.playerLock = locker;
        menus.startPanel = Find(canvas.transform, StartName);
        menus.pausePanel = Find(canvas.transform, PauseName);
        menus.titleLabel = FindText(menus.startPanel, "TitleText");
        menus.playButton = FindButton(menus.startPanel, "PlayButton");
        menus.quitButton = FindButton(menus.startPanel, "QuitButton");
        menus.resumeButton = FindButton(menus.pausePanel, "ResumeButton");
        menus.quitFromPauseButton = FindButton(menus.pausePanel, "QuitButton");

        // --- ending chain: last memory > body unlocks > she reattaches > growth
        RemoveListenersTargeting(manager.onAllCollected, body);
        RemoveListenersTargeting(manager.onProgressChanged, body);

        if (body != null)
        {
            Undo.RecordObject(body, "Wire body");
            UnityEventTools.AddPersistentListener(manager.onAllCollected, new UnityAction(body.Unlock));
            UnityEventTools.AddPersistentListener(manager.onProgressChanged, new UnityAction<int, int>(body.SetProgress));

            var growth = Ensure<FinaleGrowth>(body.gameObject);
            RemoveListenersTargeting(body.onReattached, growth);
            UnityEventTools.AddPersistentListener(body.onReattached, new UnityAction(growth.Play));

            EditorUtility.SetDirty(body);
            EditorUtility.SetDirty(growth);
            Debug.Log("<b>[Birthday]</b> Chain wired: memories > body unlock > press E to reattach > tree and flowers.");
        }
        else
        {
            Debug.LogWarning("[Birthday] No BodyReattach in the scene. Run Tools > Birthday > Build Head and Body first.");
        }

        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(narrator);
        EditorUtility.SetDirty(menus);
    }

    // ---------------------------------------------------------------- canvas

    private static Canvas EnsureCanvas()
    {
        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(go, "Create Canvas");
            canvas = go.GetComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvas.GetComponent<CanvasScaler>() ?? canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;

        var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
        Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
    }

    // ------------------------------------------------------------------- hud

    private static void BuildHud(Transform parent)
    {
        var go = NewUI(HudName, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-40f, -30f);
        rt.sizeDelta = new Vector2(460f, 60f);

        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = "0 / 10 memories found";
        text.fontSize = 34;
        text.alignment = TextAlignmentOptions.TopRight;
        text.color = Color.white;
        text.outlineWidth = 0.2f;
        text.outlineColor = new Color32(0, 0, 0, 160);
        text.raycastTarget = false;

        var hud = go.AddComponent<HudCounter>();
        hud.label = text;
    }

    // ------------------------------------------------------------ charge bar

    private static void BuildChargeBar(Transform parent)
    {
        const float W = 340f, H = 18f;

        var root = NewUI(ChargeName, parent);
        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 120f);
        rt.sizeDelta = new Vector2(W, H);

        var group = root.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        var track = NewUI("Track", root.transform);
        Stretch(track.GetComponent<RectTransform>());
        var trackImg = track.AddComponent<Image>();
        trackImg.color = new Color(0f, 0f, 0f, 0.45f);
        trackImg.raycastTarget = false;

        var fill = NewUI("Fill", root.transform);
        var fillRt = fill.GetComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0f, 0f);
        fillRt.anchorMax = new Vector2(0f, 1f);
        fillRt.pivot = new Vector2(0f, 0.5f);
        fillRt.anchoredPosition = Vector2.zero;
        fillRt.sizeDelta = Vector2.zero;
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.55f, 0.8f, 1f);
        fillImg.raycastTarget = false;

        var marker = NewUI("MaxMarker", root.transform);
        var mRt = marker.GetComponent<RectTransform>();
        mRt.anchorMin = new Vector2(0f, 0f);
        mRt.anchorMax = new Vector2(0f, 1f);
        mRt.pivot = new Vector2(0.5f, 0.5f);
        mRt.anchoredPosition = new Vector2(W, 0f);
        mRt.sizeDelta = new Vector2(4f, 12f);
        var mImg = marker.AddComponent<Image>();
        mImg.color = new Color(1f, 1f, 1f, 0.9f);
        mImg.raycastTarget = false;

        var bar = root.AddComponent<ChargeBarUI>();
        bar.group = group;
        bar.fillRect = fillRt;
        bar.fillImage = fillImg;
        bar.maxMarker = mRt;
        bar.maxWidth = W;
    }

    // ----------------------------------------------------------- memory card

    private static void BuildMemoryCard(Transform parent)
    {
        var root = NewUI(CardName, parent);
        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(60f, 0f);
        rt.sizeDelta = new Vector2(440f, 420f);

        var group = root.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        var bg = NewUI("Backing", root.transform);
        Stretch(bg.GetComponent<RectTransform>());
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(1f, 1f, 1f, 0.93f);
        bgImg.raycastTarget = false;

        var photo = NewUI("Photo", root.transform);
        var pRt = photo.GetComponent<RectTransform>();
        pRt.anchorMin = new Vector2(0f, 0f);
        pRt.anchorMax = new Vector2(1f, 1f);
        pRt.offsetMin = new Vector2(18f, 76f);
        pRt.offsetMax = new Vector2(-18f, -18f);
        var pImg = photo.AddComponent<Image>();
        pImg.preserveAspect = true;
        pImg.raycastTarget = false;

        var title = NewUI("Title", root.transform);
        var tRt = title.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 0f);
        tRt.anchorMax = new Vector2(1f, 0f);
        tRt.pivot = new Vector2(0.5f, 0f);
        tRt.offsetMin = new Vector2(18f, 16f);
        tRt.offsetMax = new Vector2(-18f, 66f);
        var tText = title.AddComponent<TextMeshProUGUI>();
        tText.text = "Memory";
        tText.fontSize = 30;
        tText.alignment = TextAlignmentOptions.Center;
        tText.color = Ink;
        tText.raycastTarget = false;

        var card = root.AddComponent<MemoryCardUI>();
        card.panel = rt;
        card.group = group;
        card.photo = pImg;
        card.title = tText;
    }

    // ----------------------------------------------------------------- menus

    private static void BuildStartPanel(Transform parent)
    {
        var root = NewUI(StartName, parent);
        Stretch(root.GetComponent<RectTransform>());

        // Deliberately only a partial scrim. The live 3D scene shows through, so
        // the start screen is the actual game world with Ava standing in it.
        var scrim = NewUI("Scrim", root.transform);
        Stretch(scrim.GetComponent<RectTransform>());
        var sImg = scrim.AddComponent<Image>();
        sImg.color = new Color(0.05f, 0.02f, 0.06f, 0.35f);

        MakeLabel(root.transform, "TitleText", "Happy Birthday, Ava", 84, Cream,
                  new Vector2(0.5f, 0.72f), new Vector2(1400f, 140f));

        MakeButton(root.transform, "PlayButton", "Play", new Vector2(0.5f, 0.42f), Pink, Ink);
        MakeButton(root.transform, "QuitButton", "Quit", new Vector2(0.5f, 0.30f),
                   new Color(1f, 1f, 1f, 0.85f), Ink);
    }

    private static void BuildPausePanel(Transform parent)
    {
        var root = NewUI(PauseName, parent);
        Stretch(root.GetComponent<RectTransform>());

        var scrim = NewUI("Scrim", root.transform);
        Stretch(scrim.GetComponent<RectTransform>());
        var sImg = scrim.AddComponent<Image>();
        sImg.color = new Color(0.05f, 0.02f, 0.06f, 0.55f);

        MakeLabel(root.transform, "PauseTitle", "Paused", 72, Cream,
                  new Vector2(0.5f, 0.66f), new Vector2(900f, 120f));

        MakeButton(root.transform, "ResumeButton", "Resume", new Vector2(0.5f, 0.46f), Pink, Ink);
        MakeButton(root.transform, "QuitButton", "Quit", new Vector2(0.5f, 0.34f),
                   new Color(1f, 1f, 1f, 0.85f), Ink);

        root.SetActive(false);
    }

    private static void MakeLabel(Transform parent, string name, string text, float size,
                                  Color color, Vector2 anchor, Vector2 dims)
    {
        var go = NewUI(name, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = dims;

        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.color = color;
        t.raycastTarget = false;
    }

    private static void MakeButton(Transform parent, string name, string label,
                                   Vector2 anchor, Color fill, Color textColor)
    {
        var go = NewUI(name, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(380f, 86f);

        var img = go.AddComponent<Image>();
        img.color = fill;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var labelGo = NewUI("Label", go.transform);
        Stretch(labelGo.GetComponent<RectTransform>());
        var t = labelGo.AddComponent<TextMeshProUGUI>();
        t.text = label;
        t.fontSize = 38;
        t.alignment = TextAlignmentOptions.Center;
        t.color = textColor;
        t.raycastTarget = false;
    }

    // --------------------------------------------------------------- helpers

    private static T Ensure<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : Undo.AddComponent<T>(go);
    }

    private static GameObject Find(Transform parent, string name)
    {
        var t = parent.Find(name);
        return t != null ? t.gameObject : null;
    }

    private static Button FindButton(GameObject root, string name)
    {
        if (root == null) return null;
        var t = root.transform.Find(name);
        return t != null ? t.GetComponent<Button>() : null;
    }

    private static TMP_Text FindText(GameObject root, string name)
    {
        if (root == null) return null;
        var t = root.transform.Find(name);
        return t != null ? t.GetComponent<TMP_Text>() : null;
    }

    private static void RemoveListenersTargeting(UnityEventBase evt, Object target)
    {
        if (evt == null || target == null) return;
        for (int i = evt.GetPersistentEventCount() - 1; i >= 0; i--)
            if (evt.GetPersistentTarget(i) == target)
                UnityEventTools.RemovePersistentListener(evt, i);
    }

    private static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        return go;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void DestroyChild(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child != null) Undo.DestroyObjectImmediate(child.gameObject);
    }
}
