using UnityEngine;
using UnityEditor;

/// <summary>
/// The opposite of Fix Detached Visuals.
///
/// When you place an object by dragging its Visual child, the mesh ends up where
/// you want it while the root, its collider, its script and its sockets stay
/// behind. Fix Detached Visuals solves that by snapping the mesh back to the
/// root, which throws away the placement you just did.
///
/// This does it the useful way round: it moves the ROOT to wherever the visual
/// already sits, then zeroes the child's offset. Nothing appears to move, but
/// the logic catches up with the art. All the other children (neck socket,
/// progress marks, lights) travel with the root.
///
/// Tools > Birthday > Adopt Visual Position
/// </summary>
public static class BirthdayAdoptVisual
{
    private const float Threshold = 0.05f;

    // Where each visual is SUPPOSED to sit relative to its root.
    private static readonly Vector3 BodyVisualLocal = new(0f, 0.85f, 0f);
    private static readonly Vector3 DefaultVisualLocal = Vector3.zero;

    [MenuItem("Tools/Birthday/Adopt Visual Position")]
    public static void Adopt()
    {
        int moved = 0;

        foreach (var b in Object.FindObjectsByType<BodyReattach>(FindObjectsSortMode.None))
            if (AdoptOne(b.transform, BodyVisualLocal)) moved++;

        foreach (var h in Object.FindObjectsByType<HeadPickup>(FindObjectsSortMode.None))
            if (AdoptOne(h.transform, DefaultVisualLocal)) moved++;

        foreach (var m in Object.FindObjectsByType<MemoryPickup>(FindObjectsSortMode.None))
            if (AdoptOne(m.transform, DefaultVisualLocal)) moved++;

        if (moved == 0)
        {
            Debug.Log("<b>[Birthday]</b> Nothing to adopt. Every root is already under its own mesh.");
            return;
        }

        Debug.Log($"<b>[Birthday]</b> Moved {moved} root(s) to where their meshes already were. " +
                  $"Nothing changed visually, but colliders, scripts and sockets are now in the right place. " +
                  $"Re-run Check Placement to confirm.");
    }

    private static bool AdoptOne(Transform root, Vector3 designedLocal)
    {
        var visual = FindVisual(root);
        if (visual == null) return false;

        Vector3 drift = visual.localPosition - designedLocal;
        if (drift.magnitude < Threshold) return false;

        // Where the visual is right now, in world space. This must not change.
        Vector3 keepWorld = visual.position;

        Undo.RecordObject(root, "Adopt visual position");
        Undo.RecordObject(visual, "Adopt visual position");

        // Move the root so that, with the child back at its designed offset,
        // the mesh lands in exactly the same world spot it already occupies.
        visual.localPosition = designedLocal;
        Vector3 nowWorld = visual.position;
        root.position += keepWorld - nowWorld;

        Debug.Log($"[Birthday] '{root.name}' root moved by {drift.magnitude:0.0}m to meet its mesh.", root);

        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(visual);
        return true;
    }

    /// <summary>
    /// Handles the hand-made orb whose child is still called "Sphere" rather
    /// than "Visual", and falls back to the first renderer child.
    /// </summary>
    private static Transform FindVisual(Transform root)
    {
        var v = root.Find("Visual");
        if (v != null) return v;

        v = root.Find("Sphere");
        if (v != null) return v;

        foreach (Transform child in root)
            if (child.GetComponent<MeshRenderer>() != null) return child;

        return null;
    }
}
