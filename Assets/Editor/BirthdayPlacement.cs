using UnityEngine;
using UnityEditor;

/// <summary>
/// Drops selected objects onto the terrain surface, with a sensible height
/// offset per object type. Saves a lot of gizmo wrestling when placing orbs.
///
/// Tools > Birthday > Drop Selection To Ground   (shortcut: Ctrl+Shift+D)
/// </summary>
public static class BirthdayPlacement
{
    [MenuItem("Tools/Birthday/Drop Selection To Ground %#d")]
    public static void DropSelection()
    {
        var terrain = Terrain.activeTerrain ?? Object.FindFirstObjectByType<Terrain>();
        if (terrain == null)
        {
            EditorUtility.DisplayDialog("No terrain", "There is no Terrain in the scene.", "OK");
            return;
        }

        var selected = Selection.gameObjects;
        if (selected.Length == 0)
        {
            EditorUtility.DisplayDialog("Nothing selected",
                "Select the objects you want to drop, then run this again.\n\n" +
                "Tip: click the first orb in the Hierarchy, then shift-click the last to select the whole run.", "OK");
            return;
        }

        int moved = 0;

        foreach (var go in selected)
        {
            // Skip children whose parent is also selected, or they fight each other.
            if (IsChildOfSelection(go, selected)) continue;

            Vector3 p = go.transform.position;
            float ground = terrain.SampleHeight(p) + terrain.transform.position.y;

            Undo.RecordObject(go.transform, "Drop to ground");
            go.transform.position = new Vector3(p.x, ground + OffsetFor(go), p.z);
            moved++;
        }

        Debug.Log($"<b>[Birthday]</b> Dropped {moved} object(s) onto the terrain.");
    }

    /// <summary>
    /// Orbs float at chest height so they read as magical and are easy to walk
    /// into. Everything else sits on the ground.
    /// </summary>
    private static float OffsetFor(GameObject go)
    {
        if (go.GetComponent<MemoryPickup>() != null) return 1.3f;
        if (go.GetComponent<HeadPickup>() != null) return 0.3f;
        if (go.GetComponent<CharacterController>() != null) return 0.1f;
        return 0f;
    }

    private static bool IsChildOfSelection(GameObject go, GameObject[] selection)
    {
        foreach (var other in selection)
        {
            if (other == go) continue;
            if (go.transform.IsChildOf(other.transform)) return true;
        }
        return false;
    }

    /// <summary>
    /// Sanity report before playtesting. Every message prints the actual numbers,
    /// so you can judge whether the tool is right rather than trusting it.
    /// </summary>
    [MenuItem("Tools/Birthday/Check Placement")]
    public static void CheckPlacement()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<b>[Birthday] Placement report</b>");

        // ---- reference heights
        var water = GameObject.Find("Water");
        bool hasWater = water != null;
        float waterY = hasWater ? water.transform.position.y : float.NegativeInfinity;

        var terrain = Terrain.activeTerrain ?? Object.FindFirstObjectByType<Terrain>();

        sb.AppendLine(hasWater
            ? $"  Water surface: y = {waterY:0.00}"
            : "  Water surface: NONE FOUND (no object named exactly 'Water')");

        if (terrain != null)
        {
            var td = terrain.terrainData;
            sb.AppendLine($"  Terrain base y = {terrain.transform.position.y:0.00}, height range {td.size.y:0.0}");
        }

        int problems = 0;

        // ---- orbs
        var pickups = Object.FindObjectsByType<MemoryPickup>(FindObjectsSortMode.None);
        System.Array.Sort(pickups, (a, b) => string.CompareOrdinal(a.name, b.name));

        sb.AppendLine($"\n  {pickups.Length} orb(s):");

        foreach (var p in pickups)
        {
            Vector3 pos = p.transform.position;
            float ground = terrain != null
                ? terrain.SampleHeight(pos) + terrain.transform.position.y
                : float.NaN;

            float aboveGround = pos.y - ground;

            float nearest = float.MaxValue;
            foreach (var q in pickups)
            {
                if (q == p) continue;
                nearest = Mathf.Min(nearest, Vector3.Distance(pos, q.transform.position));
            }

            string flags = "";
            if (p.memory == null) { flags += " [NO MEMORY]"; problems++; }
            if (hasWater && pos.y < waterY) { flags += " [UNDER WATER]"; problems++; }
            if (!float.IsNaN(aboveGround) && aboveGround > 4f) { flags += " [FLOATING HIGH]"; problems++; }
            if (nearest < 8f) { flags += $" [ONLY {nearest:0.#}m FROM NEIGHBOUR]"; problems++; }

            sb.AppendLine($"    {p.name,-12} y={pos.y,7:0.00}  ground={ground,7:0.00}  " +
                          $"above={aboveGround,5:0.0}  nearest={nearest,5:0.0}m{flags}");
        }

        // ---- body
        var body = Object.FindFirstObjectByType<BodyReattach>();
        sb.AppendLine("\n  Body:");

        if (body == null)
        {
            sb.AppendLine("    NONE IN SCENE");
            problems++;
        }
        else
        {
            Vector3 bpos = body.transform.position;
            float bground = terrain != null
                ? terrain.SampleHeight(bpos) + terrain.transform.position.y
                : float.NaN;

            sb.AppendLine($"    root y = {bpos.y:0.00}   terrain here = {bground:0.00}   " +
                          $"water = {(hasWater ? waterY.ToString("0.00") : "n/a")}");

            if (hasWater && bpos.y < waterY)
            {
                sb.AppendLine($"    FLAGGED: the body's ROOT sits {waterY - bpos.y:0.00}m below the water line.");
                sb.AppendLine($"    If the body LOOKS like it is on the hill, its Visual child has been moved " +
                              $"away from the root. Run Tools > Birthday > Reset Body Layout.");
                problems++;
            }

            if (!float.IsNaN(bground) && Mathf.Abs(bpos.y - bground) > 2f)
            {
                sb.AppendLine($"    FLAGGED: root is {bpos.y - bground:0.00}m off the terrain surface here.");
                problems++;
            }
        }

        // ---- head and player
        var head = Object.FindFirstObjectByType<HeadPickup>();
        var player = GameObject.FindGameObjectWithTag("Player");

        if (head != null && player != null)
        {
            float d = Vector3.Distance(head.transform.position, player.transform.position);
            sb.AppendLine($"\n  Head is {d:0.#}m from the player at start " +
                          $"({(d < 12f ? "good, she will find it immediately" : "far, consider moving it closer")}).");
        }

        if (body != null && player != null)
        {
            float d = Vector3.Distance(body.transform.position, player.transform.position);
            sb.AppendLine($"  Body is {d:0.#}m from spawn.");
        }

        sb.AppendLine($"\n  {problems} thing(s) flagged.");
        Debug.Log(sb.ToString());
    }
}
