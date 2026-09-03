using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Tells you exactly how much of the actual gift is still placeholder.
/// Run it whenever you want to know how far from done you really are.
///
/// Tools > Birthday > Check Content
/// </summary>
public static class BirthdayContentCheck
{
    private static readonly string[] PlaceholderMarkers =
    {
        "Replace this with the real one",
        "Memory 1", "Memory 2", "Memory 3", "Memory 4", "Memory 5",
        "Memory 6", "Memory 7", "Memory 8", "Memory 9", "Memory 10",
    };

    /// <summary>
    /// Selects any MemoryData asset that no orb in the scene actually uses, so
    /// leftovers from testing stop inflating the total and stop you hunting for
    /// a file you cannot remember creating.
    /// </summary>
    [MenuItem("Tools/Birthday/Find Unused Memories")]
    public static void FindUnusedMemories()
    {
        var used = new HashSet<MemoryData>(
            Object.FindObjectsByType<MemoryPickup>(FindObjectsSortMode.None)
                  .Select(p => p.memory)
                  .Where(m => m != null));

        var all = AssetDatabase.FindAssets("t:MemoryData")
            .Select(g => AssetDatabase.GUIDToAssetPath(g))
            .Select(AssetDatabase.LoadAssetAtPath<MemoryData>)
            .Where(m => m != null)
            .OrderBy(m => m.name)
            .ToList();

        var unused = all.Where(m => !used.Contains(m)).ToList();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<b>[Birthday] Unused memories</b>\n");
        sb.AppendLine($"  {all.Count} MemoryData asset(s) in the project, {used.Count} used by orbs in this scene.\n");

        if (unused.Count == 0)
        {
            sb.AppendLine("  Every memory asset is assigned to an orb. Nothing to clean up.");
            Debug.Log(sb.ToString());
            return;
        }

        sb.AppendLine($"  {unused.Count} not used by any orb:");
        foreach (var m in unused)
            sb.AppendLine($"    {m.name}\n      {AssetDatabase.GetAssetPath(m)}");

        sb.AppendLine("\n  They are now selected in the Project window. If you recognise them as leftovers, " +
                      "press Delete. Unused assets do not affect the game, but they do make Check Content lie " +
                      "to you about how many memories there are.");

        Debug.Log(sb.ToString());

        Selection.objects = unused.Cast<Object>().ToArray();
        EditorGUIUtility.PingObject(unused[0]);
    }

    [MenuItem("Tools/Birthday/Check Content")]
    public static void CheckContent()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<b>[Birthday] Content report</b>\n");

        var guids = AssetDatabase.FindAssets("t:MemoryData");
        var memories = guids
            .Select(g => AssetDatabase.LoadAssetAtPath<MemoryData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(m => m != null)
            .OrderBy(m => m.name)
            .ToList();

        if (memories.Count == 0)
        {
            Debug.LogWarning("[Birthday] No MemoryData assets found.");
            return;
        }

        int written = 0, photographed = 0, voiced = 0;

        sb.AppendLine($"  {memories.Count} memories:");
        foreach (var m in memories)
        {
            bool hasNote = !string.IsNullOrWhiteSpace(m.note)
                           && !PlaceholderMarkers.Any(x => m.note.Contains(x));
            bool hasTitle = !string.IsNullOrWhiteSpace(m.title)
                            && !PlaceholderMarkers.Any(x => m.title == x);
            bool hasPhoto = m.photo != null;
            bool hasVoice = m.voiceClip != null;

            if (hasNote && hasTitle) written++;
            if (hasPhoto) photographed++;
            if (hasVoice) voiced++;

            string note = hasNote ? $"{m.note.Length} chars" : "PLACEHOLDER";
            sb.AppendLine($"    {m.name,-22} title:{(hasTitle ? "yes" : "PLACEHOLDER"),-12} " +
                          $"note:{note,-14} photo:{(hasPhoto ? "yes" : "MISSING"),-8} " +
                          $"voice:{(hasVoice ? "yes" : "-")}");
        }

        // ---- head dialogue
        var barks = Object.FindFirstObjectByType<HeadBarks>(FindObjectsInactive.Include);
        sb.AppendLine("\n  Head dialogue:");

        if (barks == null)
        {
            sb.AppendLine("    No HeadBarks in the scene.");
        }
        else
        {
            int w = barks.welcomeLines?.Length ?? 0;
            sb.AppendLine($"    <b>welcome ({w} entr{(w == 1 ? "y" : "ies")}, spoken on the first pickup)</b>");
            if (barks.welcomeLines != null)
                foreach (var l in barks.welcomeLines)
                    sb.AppendLine($"      \"{Trim(l)}\"");
            sb.AppendLine($"    silent until first pickup: {barks.silentUntilFirstPickup}\n");

            ReportBark(sb, "picked up", barks.onPickedUp);
            ReportBark(sb, "thrown", barks.onThrown);
            ReportBark(sb, "landed", barks.onLanded);
            ReportBark(sb, "dropped", barks.onDropped);
            ReportBark(sb, "memory found", barks.onMemoryFound);
            ReportBark(sb, "idle", barks.onIdle);
            ReportBark(sb, "near body", barks.onNearBody);
        }

        // ---- ending
        var growth = Object.FindFirstObjectByType<FinaleGrowth>(FindObjectsInactive.Include);
        if (growth != null)
        {
            int n = growth.finalLines?.Length ?? 0;
            sb.AppendLine($"\n  Closing lines ({n}):");
            if (growth.finalLines != null)
                foreach (var l in growth.finalLines)
                    sb.AppendLine($"    \"{Trim(l)}\"");
        }

        var body = Object.FindFirstObjectByType<BodyReattach>(FindObjectsInactive.Include);
        if (body != null)
        {
            sb.AppendLine("\n  Body prompts:");
            sb.AppendLine($"    locked    \"{Trim(body.lockedPrompt)}\"");
            sb.AppendLine($"    ready     \"{Trim(body.readyPrompt)}\"");
            sb.AppendLine($"    need head \"{Trim(body.needHeadPrompt)}\"");
        }

        var menus = Object.FindFirstObjectByType<GameMenus>(FindObjectsInactive.Include);
        if (menus != null)
            sb.AppendLine($"\n  Title screen: \"{menus.titleText}\"");

        sb.AppendLine($"\n  <b>{written}/{memories.Count} notes written, " +
                      $"{photographed}/{memories.Count} photos in, " +
                      $"{voiced}/{memories.Count} voiced.</b>");

        Debug.Log(sb.ToString());
    }

    private static void ReportBark(System.Text.StringBuilder sb, string label, HeadBarks.BarkSet set)
    {
        int n = set?.lines?.Length ?? 0;
        string sample = n > 0 ? $"  e.g. \"{Trim(set.lines[0])}\"" : "";
        sb.AppendLine($"    {label,-15} {n} line(s){sample}");
    }

    private static string Trim(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace("\n", " ");
        return s.Length > 60 ? s[..60] + "..." : s;
    }
}
