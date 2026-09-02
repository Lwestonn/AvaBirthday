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
