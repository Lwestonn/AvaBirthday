using UnityEngine;
using UnityEditor;
using TMPro;

/// <summary>
/// Rebuilds the floating speech label above the head, and reports why it might
/// not have been showing.
///
/// Tools > Birthday > Repair Head Label
/// </summary>
public static class BirthdayHeadRepair
{
    private const string LabelName = "BarkLabel";

    [MenuItem("Tools/Birthday/Repair Head Label")]
    public static void RepairLabel()
    {
        var barks = Object.FindFirstObjectByType<HeadBarks>(FindObjectsInactive.Include);
        if (barks == null)
        {
            EditorUtility.DisplayDialog("No HeadBarks",
                "There is no HeadBarks component in the scene. Select LukeHead and add one, " +
                "or run Tools > Birthday > Build Head and Body.", "OK");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<b>[Birthday] Head label repair</b>");

        var head = barks.transform;

        // ---- diagnose what was wrong before touching anything
        if (!barks.enabled)
            sb.AppendLine("  FOUND: the HeadBarks component was disabled.");
        if (!barks.gameObject.activeInHierarchy)
            sb.AppendLine("  FOUND: the head GameObject is inactive.");
        if (barks.label == null)
            sb.AppendLine("  FOUND: the Label slot was empty. Nothing could be displayed.");

        // ---- find or rebuild the label
        TMP_Text label = barks.label;

        if (label == null)
        {
            var existing = head.Find(LabelName);
            if (existing != null) label = existing.GetComponent<TMP_Text>();
        }

        if (label == null)
        {
            // It may have been detached at runtime and left loose in the scene.
            var loose = GameObject.Find(LabelName);
            if (loose != null)
            {
                label = loose.GetComponent<TMP_Text>();
                if (label != null)
                {
                    sb.AppendLine("  FOUND: a loose BarkLabel at the scene root. Re-parenting it.");
                    Undo.SetTransformParent(loose.transform, head, "Reparent label");
                }
            }
        }

        if (label == null)
        {
            if (TMP_Settings.instance == null)
            {
                EditorUtility.DisplayDialog("TextMeshPro not set up",
                    "Window > TextMeshPro > Import TMP Essential Resources, then run this again.", "OK");
                return;
            }

            var go = new GameObject(LabelName, typeof(TextMeshPro));
            Undo.RegisterCreatedObjectUndo(go, "Create BarkLabel");
            go.transform.SetParent(head, false);
            label = go.GetComponent<TMP_Text>();
            sb.AppendLine("  Created a new BarkLabel.");
        }

        // ---- configure it so it is definitely readable
        Undo.RecordObject(label.gameObject.transform, "Configure label");
        Undo.RecordObject(label, "Configure label");

        label.transform.localPosition = new Vector3(0f, 0.75f, 0f);
        label.transform.localRotation = Quaternion.identity;
        label.transform.localScale = Vector3.one;

        label.fontSize = 3.2f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.outlineWidth = 0.25f;
        label.outlineColor = new Color32(20, 10, 20, 220);
        label.enableWordWrapping = true;
        label.rectTransform.sizeDelta = new Vector2(6f, 1.5f);
        label.text = "";
        label.gameObject.SetActive(true);

        Undo.RecordObject(barks, "Wire label");
        barks.label = label;
        barks.enabled = true;

        // ---- make sure the narrator knows about this HeadBarks
        var narrator = Object.FindFirstObjectByType<MemoryNarrator>(FindObjectsInactive.Include);
        if (narrator != null)
        {
            if (narrator.head != barks)
            {
                sb.AppendLine("  FOUND: MemoryNarrator was not pointing at this HeadBarks. Fixed.");
                Undo.RecordObject(narrator, "Wire narrator head");
                narrator.head = barks;
                EditorUtility.SetDirty(narrator);
            }
        }
        else
        {
            sb.AppendLine("  WARNING: no MemoryNarrator in the scene, so notes will never be spoken. " +
                          "Run Tools > Birthday > Rewire Events.");
        }

        EditorUtility.SetDirty(barks);
        EditorUtility.SetDirty(label);
        Selection.activeGameObject = barks.gameObject;

        sb.AppendLine($"\n  Label ready on '{head.name}', 0.75m above it, font size {label.fontSize}.");
        sb.AppendLine("  Press Play and pick the head up. It should say something immediately.");
        Debug.Log(sb.ToString());
    }
}
