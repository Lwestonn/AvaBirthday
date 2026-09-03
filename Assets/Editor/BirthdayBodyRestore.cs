using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Puts the BodyReattach component back on the body and reconnects everything it
/// needs, without rebuilding the body or touching where you have placed it.
///
/// Written after the component vanished from a finished scene. Rebuilding the
/// body from scratch would have thrown away its position on the hill; this only
/// restores the missing piece.
///
/// Tools > Birthday > Restore Body Component
/// </summary>
public static class BirthdayBodyRestore
{
    [MenuItem("Tools/Birthday/Restore Body Component")]
    public static void Restore()
    {
        var body = FindBody();

        if (body == null)
        {
            EditorUtility.DisplayDialog("No body found",
                "Could not find an object called LukeBody, or any object with a NeckSocket child.\n\n" +
                "Select your body object in the Hierarchy and run this again.", "OK");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"<b>[Birthday] Restoring '{body.name}'</b>\n");

        var reattach = body.GetComponent<BodyReattach>();

        if (reattach == null)
        {
            reattach = Undo.AddComponent<BodyReattach>(body);
            sb.AppendLine("  Added the missing BodyReattach component.");
        }
        else
        {
            sb.AppendLine("  BodyReattach was already there. Filling in anything missing.");
        }

        Undo.RecordObject(reattach, "Restore body");

        // ---- neck socket
        if (reattach.neckSocket == null || reattach.neckSocket == body.transform)
        {
            var neck = body.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name.ToLowerInvariant().Contains("neck"));

            if (neck != null)
            {
                reattach.neckSocket = neck;
                sb.AppendLine($"  Neck socket: {neck.name}");
            }
            else
            {
                sb.AppendLine("  NECK SOCKET NOT FOUND. Make an empty child where the head should sit, " +
                              "call it NeckSocket, and drag it into the slot.");
            }
        }
        else
        {
            sb.AppendLine($"  Neck socket already set: {reattach.neckSocket.name}");
        }

        // ---- progress marks
        if (reattach.progressMarks == null || reattach.progressMarks.Length == 0
            || reattach.progressMarks.All(m => m == null))
        {
            var marks = body.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.name.ToLowerInvariant().StartsWith("mark"))
                .OrderBy(r => r.name)
                .ToArray();

            if (marks.Length > 0)
            {
                reattach.progressMarks = marks;
                sb.AppendLine($"  Progress marks: found {marks.Length} " +
                              $"({string.Join(", ", marks.Take(4).Select(m => m.name))}" +
                              $"{(marks.Length > 4 ? ", ..." : "")})");
            }
            else
            {
                sb.AppendLine("  NO MARKS FOUND. Children need names starting with 'Mark'. " +
                              "The chest meter will simply not light, everything else still works.");
            }
        }
        else
        {
            sb.AppendLine($"  Progress marks already set: {reattach.progressMarks.Length}");
        }

        // ---- trigger
        var col = body.GetComponent<Collider>();
        if (col == null)
        {
            var sphere = Undo.AddComponent<SphereCollider>(body);
            sphere.isTrigger = true;
            sphere.radius = 3f;
            sphere.center = new Vector3(0f, 1f, 0f);
            sb.AppendLine("  Added the missing trigger collider (sphere, radius 3).");
        }
        else if (!col.isTrigger)
        {
            Undo.RecordObject(col, "Restore body");
            col.isTrigger = true;
            sb.AppendLine("  Ticked Is Trigger on the existing collider. Without it she can walk up " +
                          "and nothing ever happens.");
        }
        else
        {
            sb.AppendLine($"  Trigger collider present ({col.GetType().Name}).");
        }

        // ---- prompts, only if blank
        if (string.IsNullOrWhiteSpace(reattach.lockedPrompt))
            reattach.lockedPrompt = "He's missing something. Keep looking.";
        if (string.IsNullOrWhiteSpace(reattach.readyPrompt))
            reattach.readyPrompt = "Press E to put him back together";
        if (string.IsNullOrWhiteSpace(reattach.needHeadPrompt))
            reattach.needHeadPrompt = "You'll need his head for this";

        sb.AppendLine($"\n  Prompts:");
        sb.AppendLine($"    locked    \"{reattach.lockedPrompt}\"");
        sb.AppendLine($"    ready     \"{reattach.readyPrompt}\"");
        sb.AppendLine($"    need head \"{reattach.needHeadPrompt}\"");

        // ---- prompt label
        if (reattach.promptLabel == null)
        {
            var label = body.GetComponentsInChildren<TMPro.TMP_Text>(true).FirstOrDefault();
            if (label != null)
            {
                reattach.promptLabel = label;
                sb.AppendLine($"\n  Prompt label: {label.name}");
            }
            else
            {
                sb.AppendLine("\n  No prompt label found. She gets no floating text at the body, " +
                              "but reattaching still works.");
            }
        }

        EditorUtility.SetDirty(reattach);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        sb.AppendLine("\n  Now run Tools > Birthday > Repair Ending Wiring to reconnect the events, " +
                      "then Check Ending to confirm.");

        Debug.Log(sb.ToString(), body);
        Selection.activeGameObject = body;
    }

    private static GameObject FindBody()
    {
        // Whatever is selected wins, so you can point at it directly.
        if (Selection.activeGameObject != null && Selection.activeGameObject.scene.IsValid())
            return Selection.activeGameObject;

        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name.ToLowerInvariant().Contains("body")) return root;

                var neck = root.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t.name.ToLowerInvariant().Contains("neck"));

                if (neck != null) return root;
            }
        }

        return null;
    }
}
