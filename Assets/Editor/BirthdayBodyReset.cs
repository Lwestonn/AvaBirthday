using UnityEngine;
using UnityEditor;

/// <summary>
/// Puts every child of LukeBody back where it was designed to sit, relative to
/// the body's root. Fixes marks, neck socket, prompt and mesh all at once,
/// including re-parenting anything that got dragged out of the hierarchy.
///
/// The root's own world position is NOT touched, so wherever you have dragged
/// the body to on the island, it stays there. Only the internal layout resets.
///
/// Tools > Birthday > Reset Body Layout
/// </summary>
public static class BirthdayBodyReset
{
    // These match what BirthdaySceneBuilder originally created.
    private static readonly Vector3 VisualPos = new(0f, 0.85f, 0f);
    private static readonly Vector3 VisualScale = new(0.6f, 0.85f, 0.6f);
    private static readonly Vector3 NeckPos = new(0f, 1.85f, 0f);
    private static readonly Vector3 PromptPos = new(0f, 2.6f, 0f);
    private const float MarkRingHeight = 1.15f;
    private const float MarkRingRadius = 0.42f;
    private const float MarkRingLift = 0.28f;
    private const float MarkForward = 0.32f;
    private const float MarkScale = 0.11f;

    [MenuItem("Tools/Birthday/Reset Body Layout")]
    public static void ResetBody()
    {
        var body = Object.FindFirstObjectByType<BodyReattach>();
        if (body == null)
        {
            EditorUtility.DisplayDialog("No body found",
                "There is no BodyReattach in the scene. Run Tools > Birthday > Build Head and Body.", "OK");
            return;
        }

        var root = body.transform;
        Undo.RegisterFullObjectHierarchyUndo(body.gameObject, "Reset body layout");

        // --- mesh
        var visual = FindOrAdopt(root, "Visual");
        if (visual != null)
        {
            visual.localPosition = VisualPos;
            visual.localRotation = Quaternion.identity;
            if (visual.localScale == Vector3.one) visual.localScale = VisualScale;
        }

        // --- neck socket
        var neck = body.neckSocket != null ? body.neckSocket : FindOrAdopt(root, "NeckSocket");
        if (neck == null)
        {
            var go = new GameObject("NeckSocket");
            Undo.RegisterCreatedObjectUndo(go, "Create NeckSocket");
            go.transform.SetParent(root, false);
            neck = go.transform;
        }
        else if (!neck.IsChildOf(root))
        {
            Undo.SetTransformParent(neck, root, "Reparent NeckSocket");
        }

        neck.localPosition = NeckPos;
        neck.localRotation = Quaternion.identity;
        body.neckSocket = neck;

        // --- prompt label
        var prompt = body.promptLabel != null ? body.promptLabel.transform : FindOrAdopt(root, "PromptLabel");
        if (prompt != null)
        {
            if (!prompt.IsChildOf(root)) Undo.SetTransformParent(prompt, root, "Reparent prompt");
            prompt.localPosition = PromptPos;
        }

        // --- the marks, back into their arc
        int placed = 0;
        int total = body.progressMarks != null ? body.progressMarks.Length : 0;

        for (int i = 0; i < total; i++)
        {
            var mark = body.progressMarks[i];
            if (mark == null) continue;

            var t = mark.transform;
            if (!t.IsChildOf(root)) Undo.SetTransformParent(t, root, "Reparent mark");

            float a = Mathf.Lerp(-70f, 70f, total > 1 ? i / (float)(total - 1) : 0.5f) * Mathf.Deg2Rad;
            t.localPosition = new Vector3(
                Mathf.Sin(a) * MarkRingRadius,
                MarkRingHeight + Mathf.Cos(a) * MarkRingLift,
                MarkForward);
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one * MarkScale;

            placed++;
        }

        EditorUtility.SetDirty(body);
        Selection.activeGameObject = body.gameObject;

        Debug.Log($"<b>[Birthday]</b> Body layout reset. Mesh, neck socket, prompt and {placed} mark(s) " +
                  $"are back in place. The body itself did not move, only its parts.");
    }

    /// <summary>Find a child by name, or adopt a stray root object with that name.</summary>
    private static Transform FindOrAdopt(Transform root, string name)
    {
        var child = root.Find(name);
        if (child != null) return child;

        var stray = GameObject.Find(name);
        if (stray == null) return null;

        Debug.Log($"[Birthday] '{name}' was loose in the scene. Re-parenting it to '{root.name}'.", stray);
        Undo.SetTransformParent(stray.transform, root, "Adopt " + name);
        return stray.transform;
    }
}
