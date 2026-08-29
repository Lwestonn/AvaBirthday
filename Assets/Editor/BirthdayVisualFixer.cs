using UnityEngine;
using UnityEditor;

/// <summary>
/// Re-centres "Visual" children that got dragged away from their root.
///
/// Why this happens: clicking an object in the Scene view selects the exact
/// renderer you clicked, which is the Visual child, not the root that holds the
/// collider, the script, and any sockets. Moving it separates what you SEE from
/// what the game actually uses.
///
/// IMPORTANT: this only zeroes the HORIZONTAL offset (X and Z). Vertical offset
/// is left alone, because several visuals are deliberately lifted: the body's
/// mesh sits 0.85 above its root so the capsule stands on the ground rather than
/// half-buried. Zeroing Y would sink it.
///
/// Tools > Birthday > Fix Detached Visuals
/// </summary>
public static class BirthdayVisualFixer
{
    [Tooltip("Offsets smaller than this are assumed deliberate.")]
    private const float Threshold = 0.05f;

    [MenuItem("Tools/Birthday/Fix Detached Visuals")]
    public static void FixVisuals()
    {
        int fixedCount = 0;

        fixedCount += FixOn(Object.FindObjectsByType<HeadPickup>(FindObjectsSortMode.None));
        fixedCount += FixOn(Object.FindObjectsByType<MemoryPickup>(FindObjectsSortMode.None));
        fixedCount += FixOn(Object.FindObjectsByType<BodyReattach>(FindObjectsSortMode.None));

        int orphans = ReportOrphans();

        if (fixedCount == 0 && orphans == 0)
        {
            Debug.Log("<b>[Birthday]</b> Nothing detached. Everything is lined up.");
            return;
        }

        Debug.Log($"<b>[Birthday]</b> Re-centred {fixedCount} Visual child object(s). " +
                  $"Move the ROOT from the Hierarchy to reposition, never the child.");
    }

    private static int FixOn(Component[] roots)
    {
        int count = 0;

        foreach (var c in roots)
        {
            var visual = c.transform.Find("Visual");
            if (visual == null) continue;

            Vector3 p = visual.localPosition;
            Vector2 horizontal = new(p.x, p.z);
            if (horizontal.magnitude < Threshold) continue;

            Debug.Log($"[Birthday] '{c.name}/Visual' was offset horizontally by {horizontal}. " +
                      $"Resetting X and Z, keeping Y at {p.y:0.##}.", c.gameObject);

            Undo.RecordObject(visual, "Recentre visual");
            visual.localPosition = new Vector3(0f, p.y, 0f);
            EditorUtility.SetDirty(visual);
            count++;
        }

        return count;
    }

    /// <summary>
    /// Catches the other version of this mistake: dragging a socket or label OUT
    /// of its parent in the Hierarchy, so it stops following the object entirely.
    /// </summary>
    private static int ReportOrphans()
    {
        int found = 0;

        var body = Object.FindFirstObjectByType<BodyReattach>();
        if (body != null)
        {
            if (body.neckSocket != null && !body.neckSocket.IsChildOf(body.transform))
            {
                Debug.LogWarning($"[Birthday] '{body.neckSocket.name}' is NOT a child of '{body.name}', " +
                                 $"so it will not move with the body. Drag it onto {body.name} in the Hierarchy.",
                                 body.neckSocket);
                found++;
            }

            if (body.promptLabel != null && !body.promptLabel.transform.IsChildOf(body.transform))
            {
                Debug.LogWarning($"[Birthday] The body's prompt label is not parented to the body.",
                                 body.promptLabel);
                found++;
            }

            foreach (var mark in body.progressMarks)
            {
                if (mark == null) continue;
                if (mark.transform.IsChildOf(body.transform)) continue;

                Debug.LogWarning($"[Birthday] Progress mark '{mark.name}' is not a child of the body.", mark);
                found++;
            }
        }

        var carrier = Object.FindFirstObjectByType<HeadCarrier>();
        if (carrier != null && carrier.carryPoint != null && !carrier.carryPoint.IsChildOf(carrier.transform))
        {
            Debug.LogWarning($"[Birthday] CarryPoint is not a child of the player, so the head will float " +
                             $"at a fixed spot in the world instead of following her.", carrier.carryPoint);
            found++;
        }

        return found;
    }
}
