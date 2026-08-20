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
/// One-click builder for the whole birthday game UI.
///
/// MUST live in a folder named exactly "Editor" (Assets/Editor/). Unity strips
/// that folder from builds, which is required because this file uses UnityEditor.
///
/// Run it from the menu: Tools > Birthday > Build UI
/// It is safe to run twice: it deletes and rebuilds its own objects.
/// </summary>
public static class BirthdayUIBuilder
{
    private const string PanelName  = "MemoryPanel";
    private const string HudName    = "HUD_Counter";
    private const string FadeName   = "FadeImage";
    private const string FinaleName = "FinalePanel";

    [MenuItem("Tools/Birthday/Build UI")]
    public static void BuildUI()
    {
        if (TMP_Settings.instance == null)
        {
            EditorUtility.DisplayDialog(
                "TextMeshPro not set up",
                "Import TMP Essentials first:\n\nWindow > TextMeshPro > Import TMP Essential Resources\n\nThen run this again.",
                "OK");
            return;
        }

        Canvas canvas = EnsureCanvas();
        EnsureEventSystem();

        // Rebuild cleanly if it was run before.
        DestroyChild(canvas.transform, PanelName);
        DestroyChild(canvas.transform, HudName);
        DestroyChild(canvas.transform, FadeName);
        DestroyChild(canvas.transform, FinaleName);

        // Sibling order == render order. Later = on top.
        var hud         = BuildHud(canvas.transform);
        var panelUI     = BuildMemoryPanel(canvas.transform);
        var fadeImage   = BuildFadeImage(canvas.transform);
        var finale      = BuildFinalePanel(canvas.transform);

        WireGameManager(panelUI, hud, fadeImage, finale.panel, finale.text, finale.group);

        Selection.activeGameObject = canvas.gameObject;
        EditorUtility.SetDirty(canvas.gameObject);
        Debug.Log("<b>[Birthday]</b> UI built and wired. Press Play and walk into a memory orb.");
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

        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
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

    private static HudCounter BuildHud(Transform parent)
    {
        var go = NewUI(HudName, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-40f, -30f);
        rt.sizeDelta = new Vector2(460f, 60f);

        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = "0 / 10 memories found";
        text.fontSize = 34;
        text.alignment = TextAlignmentOptions.TopRight;
        text.color = Color.white;
        text.outlineWidth = 0.2f;      // keeps it readable over bright sky
        text.outlineColor = new Color32(0, 0, 0, 160);

        var hud = go.AddComponent<HudCounter>();
        hud.label = text;
        return hud;
    }

    // ---------------------------------------------------------- memory panel

    private static MemoryPanelUI BuildMemoryPanel(Transform parent)
    {
        var panel = NewUI(PanelName, parent);
        Stretch(panel.GetComponent<RectTransform>());

        var group = panel.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        // --- backdrop
        var backdrop = NewUI("Backdrop", panel.transform);
        Stretch(backdrop.GetComponent<RectTransform>());
        var backImg = backdrop.AddComponent<Image>();
        backImg.color = new Color(0.02f, 0.02f, 0.05f, 0.78f);

        // --- photo
        var photo = NewUI("PhotoImage", panel.transform);
        var photoRt = photo.GetComponent<RectTransform>();
        photoRt.anchorMin = photoRt.anchorMax = photoRt.pivot = new Vector2(0.5f, 0.5f);
        photoRt.sizeDelta = new Vector2(760f, 480f);
        photoRt.anchoredPosition = new Vector2(0f, 90f);
        var photoImg = photo.AddComponent<Image>();
        photoImg.preserveAspect = true;
        photoImg.color = Color.white;

        // --- title
        var title = NewUI("TitleText", panel.transform);
        var titleRt = title.GetComponent<RectTransform>();
        titleRt.anchorMin = titleRt.anchorMax = titleRt.pivot = new Vector2(0.5f, 0.5f);
        titleRt.sizeDelta = new Vector2(1200f, 80f);
        titleRt.anchoredPosition = new Vector2(0f, 390f);
        var titleText = title.AddComponent<TextMeshProUGUI>();
        titleText.text = "Memory title";
        titleText.fontSize = 54;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = new Color(1f, 0.93f, 0.96f);

        // --- note
        var note = NewUI("NoteText", panel.transform);
        var noteRt = note.GetComponent<RectTransform>();
        noteRt.anchorMin = noteRt.anchorMax = noteRt.pivot = new Vector2(0.5f, 0.5f);
        noteRt.sizeDelta = new Vector2(1100f, 220f);
        noteRt.anchoredPosition = new Vector2(0f, -240f);
        var noteText = note.AddComponent<TextMeshProUGUI>();
        noteText.text = "Note goes here.";
        noteText.fontSize = 34;
        noteText.alignment = TextAlignmentOptions.Top;
        noteText.enableWordWrapping = true;
        noteText.color = new Color(0.95f, 0.95f, 0.95f);

        // --- continue button
        var btnGo = NewUI("ContinueButton", panel.transform);
        var btnRt = btnGo.GetComponent<RectTransform>();
        btnRt.anchorMin = btnRt.anchorMax = btnRt.pivot = new Vector2(0.5f, 0f);
        btnRt.sizeDelta = new Vector2(340f, 76f);
        btnRt.anchoredPosition = new Vector2(0f, 70f);
        var btnImg = btnGo.AddComponent<Image>();
        btnImg.color = new Color(0.98f, 0.72f, 0.82f);
        var button = btnGo.AddComponent<Button>();
        button.targetGraphic = btnImg;

        var btnLabelGo = NewUI("Label", btnGo.transform);
        Stretch(btnLabelGo.GetComponent<RectTransform>());
        var btnLabel = btnLabelGo.AddComponent<TextMeshProUGUI>();
        btnLabel.text = "Continue";
        btnLabel.fontSize = 34;
        btnLabel.alignment = TextAlignmentOptions.Center;
        btnLabel.color = new Color(0.18f, 0.08f, 0.12f);

        // --- script + wiring
        var ui = panel.AddComponent<MemoryPanelUI>();
        ui.photoImage = photoImg;
        ui.titleText = titleText;
        ui.noteText = noteText;
        ui.continueButton = button;

        var voice = panel.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        ui.voiceSource = voice;

        panel.SetActive(false);   // the script expects this
        return ui;
    }

    // ------------------------------------------------------------- finale UI

    private static Image BuildFadeImage(Transform parent)
    {
        var go = NewUI(FadeName, parent);
        Stretch(go.GetComponent<RectTransform>());
        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 0.92f, 0.95f, 0f);
        img.raycastTarget = false;
        go.SetActive(false);
        return img;
    }

    private static (GameObject panel, TMP_Text text, CanvasGroup group) BuildFinalePanel(Transform parent)
    {
        var go = NewUI(FinaleName, parent);
        Stretch(go.GetComponent<RectTransform>());
        var group = go.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        var textGo = NewUI("FinaleText", go.transform);
        var rt = textGo.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1300f, 600f);
        rt.anchoredPosition = Vector2.zero;

        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.text = "Happy birthday, Ava.";
        text.fontSize = 64;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.35f, 0.12f, 0.22f);

        go.SetActive(false);
        return (go, text, group);
    }

    // ---------------------------------------------------------------- wiring

    private static void WireGameManager(MemoryPanelUI panelUI, HudCounter hud, Image fadeImage,
                                        GameObject finalePanel, TMP_Text finaleText, CanvasGroup finaleGroup)
    {
        var manager = Object.FindFirstObjectByType<MemoryManager>();
        if (manager == null)
        {
            Debug.LogWarning("[Birthday] No MemoryManager in the scene. Create an empty GameObject named 'GameManager', add MemoryManager, then run this again to auto-wire it.");
            return;
        }

        Undo.RecordObject(manager, "Wire MemoryManager");
        manager.panel = panelUI;

        if (manager.playerLock == null)
        {
            var found = Object.FindFirstObjectByType<PlayerControlLock>();
            if (found != null) manager.playerLock = found;
            else Debug.LogWarning("[Birthday] No PlayerControlLock found. Add it to PlayerArmature and assign it on the GameManager.");
        }

        // Hook the HUD. Doing this in code sidesteps the classic Inspector mistake
        // of picking the STATIC int,int overload instead of the dynamic one.
        if (hud != null)
        {
            RemoveListenersTargeting(manager.onProgressChanged, hud);
            UnityEventTools.AddPersistentListener(manager.onProgressChanged,
                                                  new UnityAction<int, int>(hud.SetProgress));
        }

        // Finale
        var finale = manager.GetComponent<FinaleSequence>();
        if (finale == null) finale = Undo.AddComponent<FinaleSequence>(manager.gameObject);

        Undo.RecordObject(finale, "Wire FinaleSequence");
        finale.fadeImage = fadeImage;
        finale.finalePanel = finalePanel;
        finale.finaleText = finaleText;
        finale.finaleGroup = finaleGroup;

        RemoveListenersTargeting(manager.onAllCollected, finale);
        UnityEventTools.AddPersistentListener(manager.onAllCollected, new UnityAction(finale.Play));

        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(finale);
    }

    private static void RemoveListenersTargeting(UnityEventBase evt, Object target)
    {
        for (int i = evt.GetPersistentEventCount() - 1; i >= 0; i--)
            if (evt.GetPersistentTarget(i) == target)
                UnityEventTools.RemovePersistentListener(evt, i);
    }

    // --------------------------------------------------------------- helpers

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
