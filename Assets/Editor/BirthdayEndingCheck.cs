using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEditor;

/// <summary>
/// "I collected everything and the body still says he is missing something."
///
/// That sentence has exactly three possible causes, and this reports all three:
///   1. the count she needs is higher than the number she can actually collect
///   2. MemoryManager.onAllCollected is not wired to BodyReattach.Unlock
///   3. BodyReattach.onReattached is not wired to FinaleGrowth.Play
///
/// Tools > Birthday > Check Ending
/// </summary>
public static class BirthdayEndingCheck
{
    [MenuItem("Tools/Birthday/Check Ending")]
    public static void CheckEnding()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<b>[Birthday] Ending report</b>\n");

        int problems = 0;

        // ---------------------------------------------------------- counting
        var manager = Object.FindFirstObjectByType<MemoryManager>(FindObjectsInactive.Include);
        var body = Object.FindFirstObjectByType<BodyReattach>(FindObjectsInactive.Include);
        var growth = Object.FindFirstObjectByType<FinaleGrowth>(FindObjectsInactive.Include);

        if (manager == null)
        {
            sb.AppendLine("  NO MemoryManager IN THE SCENE. Nothing can work without it.");
            Debug.LogWarning(sb.ToString());
            return;
        }

        var pickups = Object.FindObjectsByType<MemoryPickup>(FindObjectsSortMode.None);
        var assigned = pickups.Select(p => p.memory).Where(m => m != null).ToList();
        var distinct = assigned.Distinct().ToList();
        int missing = pickups.Count(p => p.memory == null);

        sb.AppendLine("  Counting:");
        sb.AppendLine($"    orbs in the scene            {pickups.Length}");
        sb.AppendLine($"    orbs with a memory assigned  {assigned.Count}");
        sb.AppendLine($"    DISTINCT memories among them {distinct.Count}   <- this is what she can actually collect");
        sb.AppendLine($"    MemoryManager.totalMemories  {manager.totalMemories}" +
                      (manager.totalMemories <= 0 ? "   (0 = auto-count at startup, which is fine)" : ""));

        if (missing > 0)
        {
            sb.AppendLine($"\n    PROBLEM: {missing} orb(s) have no MemoryData assigned. They do nothing when touched.");
            problems++;

            foreach (var p in pickups.Where(p => p.memory == null))
                sb.AppendLine($"      empty orb: {Path(p.transform)}");
        }

        // Duplicates are the classic cause: two orbs share one asset, so the last
        // one can never be collected and the total is never reached.
        var dupes = assigned.GroupBy(m => m).Where(g => g.Count() > 1).ToList();
        if (dupes.Count > 0)
        {
            sb.AppendLine($"\n    PROBLEM: {dupes.Count} memory asset(s) are used by more than one orb.");
            problems++;

            foreach (var g in dupes)
            {
                sb.AppendLine($"      '{g.Key.name}' is on {g.Count()} orbs:");
                foreach (var p in pickups.Where(p => p.memory == g.Key))
                    sb.AppendLine($"        {Path(p.transform)}");
            }
        }

        if (manager.totalMemories > 0 && manager.totalMemories != distinct.Count)
        {
            sb.AppendLine($"\n    PROBLEM: totalMemories is pinned to {manager.totalMemories} but only " +
                          $"{distinct.Count} distinct memories exist in the scene. She can never reach the total.");
            sb.AppendLine($"      Fix: set MemoryManager.totalMemories to 0 so it counts for itself, " +
                          $"or set it to {distinct.Count}.");
            problems++;
        }

        // ------------------------------------------------------- duplicates
        // A second copy of any of these is invisible in the Inspector but fatal:
        // the event fires on one object while she is standing at the other.
        problems += ReportInstances<MemoryManager>(sb, "MemoryManager");
        problems += ReportInstances<BodyReattach>(sb, "BodyReattach");
        problems += ReportInstances<FinaleGrowth>(sb, "FinaleGrowth");

        // ---------------------------------------------------------- wiring
        sb.AppendLine("\n  Wiring:");

        sb.AppendLine($"    MemoryManager.onProgressChanged  {Listeners(manager.onProgressChanged)}");
        sb.AppendLine($"    MemoryManager.onAllCollected     {Listeners(manager.onAllCollected)}");

        int disabled = CountDisabled(manager.onAllCollected)
                     + CountDisabled(manager.onProgressChanged)
                     + (body != null ? CountDisabled(body.onReattached) : 0);

        if (disabled > 0)
        {
            sb.AppendLine($"\n    PROBLEM: {disabled} listener(s) are set to Off. They appear in the Inspector " +
                          $"and look correct, but never fire.");
            sb.AppendLine($"      Fix: in the event list, the small dropdown on each row says Off / Editor And " +
                          $"Runtime / Runtime Only. Set it to Runtime Only.");
            problems++;
        }

        bool unlockWired = HasListener(manager.onAllCollected, body, "Unlock");

        if (body == null)
        {
            sb.AppendLine("\n    PROBLEM: no BodyReattach in the scene.");
            problems++;
        }
        else if (!unlockWired)
        {
            sb.AppendLine("\n    PROBLEM: MemoryManager.onAllCollected does NOT call BodyReattach.Unlock.");
            sb.AppendLine("      This is almost certainly your bug. The body is never told she finished, so it");
            sb.AppendLine("      stays locked and keeps showing the 'missing something' prompt forever.");
            sb.AppendLine("      Fix by hand: select GameManager, find MemoryManager > On All Collected,");
            sb.AppendLine("      press +, drag the body object in, and choose BodyReattach > Unlock ().");
            sb.AppendLine("      Or run Tools > Birthday > Repair Ending Wiring below.");
            problems++;
        }

        if (body != null)
        {
            sb.AppendLine($"    BodyReattach.onReattached        {Listeners(body.onReattached)}");
            sb.AppendLine($"    BodyReattach.neckSocket          {(body.neckSocket != null ? body.neckSocket.name : "NOT SET")}");
            sb.AppendLine($"    progress marks                   {(body.progressMarks?.Length ?? 0)}");

            if (growth != null && !HasListener(body.onReattached, growth, "Play"))
            {
                sb.AppendLine("\n    PROBLEM: BodyReattach.onReattached does NOT call FinaleGrowth.Play.");
                sb.AppendLine("      She would put your head back on and nothing would happen.");
                problems++;
            }
        }

        sb.AppendLine(problems == 0
            ? "\n  <b>No problems found. If it still misbehaves, press Play and watch the Console for warnings.</b>"
            : $"\n  <b>{problems} problem(s) found, listed above.</b>");

        if (problems > 0) Debug.LogWarning(sb.ToString());
        else Debug.Log(sb.ToString());
    }

    /// <summary>
    /// Lists every orb side by side. When nine work and one does not, the fastest
    /// way to find the fault is to see all ten in one table and spot the odd row.
    ///
    /// Tools > Birthday > Check Orbs
    /// </summary>
    [MenuItem("Tools/Birthday/Check Orbs")]
    public static void CheckOrbs()
    {
        var pickups = Object.FindObjectsByType<MemoryPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .OrderBy(p => p.memory != null ? p.memory.name : p.name)
            .ToArray();

        if (pickups.Length == 0)
        {
            Debug.LogWarning("[Birthday] No MemoryPickup objects in the scene.");
            return;
        }

        var terrain = Terrain.activeTerrain;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<b>[Birthday] Orb report</b>\n");

        int problems = 0;

        foreach (var p in pickups)
        {
            var col = p.GetComponent<SphereCollider>();
            var issues = new List<string>();

            if (!p.gameObject.activeInHierarchy) issues.Add("GAMEOBJECT INACTIVE");
            if (!p.enabled) issues.Add("SCRIPT DISABLED");
            if (p.memory == null) issues.Add("NO MEMORY");

            if (col == null)
            {
                issues.Add("NO SPHERE COLLIDER");
            }
            else
            {
                if (!col.enabled) issues.Add("COLLIDER DISABLED");
                if (!col.isTrigger) issues.Add("IS TRIGGER UNTICKED");
                if (col.radius < 0.6f) issues.Add($"RADIUS TINY ({col.radius:0.00})");
            }

            if (p.requiresHead) issues.Add("REQUIRES HEAD (she must be carrying it)");

            Vector3 pos = p.transform.position;
            string ground = "";

            if (terrain != null)
            {
                float th = terrain.SampleHeight(pos) + terrain.transform.position.y;
                float above = pos.y - th;
                ground = $"  ground {th:0.0}, orb {above:+0.0;-0.0} above it";

                if (above < -1.5f) issues.Add($"BURIED {(-above):0.0}m UNDER THE TERRAIN");
                else if (above > 6f) issues.Add($"FLOATING {above:0.0}m UP, out of reach");
            }

            string name = p.memory != null ? p.memory.name : p.name;
            sb.AppendLine($"  <b>{name,-14}</b> {Path(p.transform)}");
            sb.AppendLine($"      collider: {(col == null ? "NONE" : $"sphere r={col.radius:0.00}, trigger={col.isTrigger}, enabled={col.enabled}")}");
            sb.AppendLine($"      layer: {LayerMask.LayerToName(p.gameObject.layer)}   pos {pos}{ground}");

            if (issues.Count > 0)
            {
                sb.AppendLine($"      <b>PROBLEM: {string.Join("; ", issues)}</b>");
                problems++;
            }

            sb.AppendLine();
        }

        sb.AppendLine(problems == 0
            ? "  Every orb looks correct. If one still will not collect, the cause is physics: check that its " +
              "layer collides with the player's layer in Edit > Project Settings > Physics."
            : $"  <b>{problems} orb(s) have problems. Tools > Birthday > Repair Orbs fixes the collider ones.</b>");

        if (problems > 0) Debug.LogWarning(sb.ToString());
        else Debug.Log(sb.ToString());
    }

    /// <summary>Forces every orb's trigger back into a working state.</summary>
    [MenuItem("Tools/Birthday/Repair Orbs")]
    public static void RepairOrbs()
    {
        var pickups = Object.FindObjectsByType<MemoryPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int fixedCount = 0;

        foreach (var p in pickups)
        {
            var col = p.GetComponent<SphereCollider>();
            bool changed = false;

            if (col == null)
            {
                col = Undo.AddComponent<SphereCollider>(p.gameObject);
                changed = true;
            }

            Undo.RecordObject(col, "Repair orb");

            if (!col.isTrigger) { col.isTrigger = true; changed = true; }
            if (!col.enabled) { col.enabled = true; changed = true; }
            if (col.radius < 0.6f) { col.radius = 1.5f; changed = true; }

            if (!p.enabled) { Undo.RecordObject(p, "Repair orb"); p.enabled = true; changed = true; }

            if (!p.gameObject.activeSelf)
            {
                Undo.RecordObject(p.gameObject, "Repair orb");
                p.gameObject.SetActive(true);
                changed = true;
            }

            if (changed) { EditorUtility.SetDirty(p.gameObject); fixedCount++; }
        }

        Debug.Log(fixedCount == 0
            ? "<b>[Birthday]</b> Every orb's trigger was already correct. If one still fails, run Check Orbs " +
              "and look at its position and layer instead."
            : $"<b>[Birthday]</b> Repaired {fixedCount} orb(s). Run Check Orbs to confirm.");
    }

    /// <summary>
    /// Strips persistent listeners whose target object no longer exists. They do
    /// nothing at runtime, but they hide real listeners in a wall of noise and
    /// make the Inspector impossible to read.
    /// </summary>
    [MenuItem("Tools/Birthday/Clean Dead Listeners")]
    public static void CleanDeadListeners()
    {
        var manager = Object.FindFirstObjectByType<MemoryManager>(FindObjectsInactive.Include);
        var body = Object.FindFirstObjectByType<BodyReattach>(FindObjectsInactive.Include);

        int removed = 0;

        if (manager != null)
        {
            Undo.RecordObject(manager, "Clean listeners");
            removed += Strip(manager.onProgressChanged);
            removed += Strip(manager.onAllCollected);
            EditorUtility.SetDirty(manager);
        }

        if (body != null)
        {
            Undo.RecordObject(body, "Clean listeners");
            removed += Strip(body.onUnlocked);
            removed += Strip(body.onReattached);
            EditorUtility.SetDirty(body);
        }

        AssetDatabase.SaveAssets();

        Debug.Log($"<b>[Birthday]</b> Removed {removed} dead listener(s) pointing at deleted objects.");
    }

    private static int Strip(UnityEventBase evt)
    {
        if (evt == null) return 0;

        int removed = 0;

        // Backwards, because removing shifts every index after it.
        for (int i = evt.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            if (evt.GetPersistentTarget(i) != null) continue;

            UnityEditor.Events.UnityEventTools.RemovePersistentListener(evt, i);
            removed++;
        }

        return removed;
    }

    /// <summary>
    /// Wires the two events the ending depends on. Safe to run repeatedly: it
    /// adds only what is missing.
    /// </summary>
    [MenuItem("Tools/Birthday/Repair Ending Wiring")]
    public static void RepairWiring()
    {
        var manager = Object.FindFirstObjectByType<MemoryManager>(FindObjectsInactive.Include);
        var body = Object.FindFirstObjectByType<BodyReattach>(FindObjectsInactive.Include);
        var growth = Object.FindFirstObjectByType<FinaleGrowth>(FindObjectsInactive.Include);

        if (manager == null || body == null)
        {
            EditorUtility.DisplayDialog("Missing pieces",
                $"MemoryManager: {(manager != null ? "found" : "NOT FOUND")}\n" +
                $"BodyReattach: {(body != null ? "found" : "NOT FOUND")}\n\n" +
                "Both have to be in the scene.", "OK");
            return;
        }

        int added = 0;

        if (!HasListener(manager.onAllCollected, body, "Unlock"))
        {
            Undo.RecordObject(manager, "Wire ending");
            UnityEditor.Events.UnityEventTools.AddPersistentListener(manager.onAllCollected, body.Unlock);
            EditorUtility.SetDirty(manager);
            added++;
        }

        if (!HasListener(manager.onProgressChanged, body, "SetProgress"))
        {
            Undo.RecordObject(manager, "Wire progress");
            UnityEditor.Events.UnityEventTools.AddPersistentListener<int, int>(
                manager.onProgressChanged, body.SetProgress);
            EditorUtility.SetDirty(manager);
            added++;
        }

        if (growth != null && !HasListener(body.onReattached, growth, "Play"))
        {
            Undo.RecordObject(body, "Wire finale");
            UnityEditor.Events.UnityEventTools.AddPersistentListener(body.onReattached, growth.Play);
            EditorUtility.SetDirty(body);
            added++;
        }

        AssetDatabase.SaveAssets();

        Debug.Log(added == 0
            ? "<b>[Birthday]</b> Ending wiring was already complete. Nothing changed."
            : $"<b>[Birthday]</b> Added {added} missing event connection(s). Run Check Ending to confirm.");
    }

    /// <summary>
    /// The one that actually settles it. Everything above inspects the scene as
    /// saved; this reads what the game currently believes while it is running.
    /// </summary>
    [MenuItem("Tools/Birthday/Report Live State (Play mode)")]
    public static void ReportLive()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Play mode only",
                "Press Play, collect the memories, then run this while still playing.", "OK");
            return;
        }

        var manager = FindInLoadedScenes<MemoryManager>();
        var body = FindInLoadedScenes<BodyReattach>();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<b>[Birthday] Live state</b>\n");

        if (manager == null)
        {
            sb.AppendLine("  No MemoryManager alive.");
        }
        else
        {
            sb.AppendLine($"  Collected      {manager.CollectedCount}");
            sb.AppendLine($"  Total needed   {manager.totalMemories}");
            sb.AppendLine($"  AllCollected   {manager.AllCollected}");

            sb.AppendLine("\n  Memories collected so far:");
            foreach (var m in manager.Collected)
                sb.AppendLine($"    {m.name}");

            if (manager.CollectedCount < manager.totalMemories)
            {
                var got = new HashSet<MemoryData>(manager.Collected);
                var all = Object.FindObjectsByType<MemoryPickup>(FindObjectsSortMode.None)
                    .Select(p => p.memory).Where(m => m != null).Distinct();

                sb.AppendLine("\n  STILL MISSING:");
                foreach (var m in all.Where(m => !got.Contains(m)))
                    sb.AppendLine($"    {m.name}");

                sb.AppendLine("\n  So the body is correct to stay locked. Those orbs were not actually picked up.");
            }
        }

        if (body == null)
            sb.AppendLine("\n  No BodyReattach alive.");
        else
            sb.AppendLine($"\n  Body '{Path(body.transform)}' IsUnlocked: {body.IsUnlocked}");

        if (manager != null && body != null && manager.AllCollected && !body.IsUnlocked)
            sb.AppendLine("\n  <b>Everything is collected but the body is still locked, so onAllCollected " +
                          "did not reach it. Run Repair Ending Wiring.</b>");

        Debug.Log(sb.ToString());
    }

    /// <summary>Unlocks the body immediately so you can test the ending without a full playthrough.</summary>
    [MenuItem("Tools/Birthday/Unlock Body Now (Play mode)")]
    public static void UnlockNow()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Play mode only",
                "Press Play first, then run this. It unlocks the body so you can walk the head up " +
                "and test the ending without collecting all ten.", "OK");
            return;
        }

        var body = FindInLoadedScenes<BodyReattach>();

        if (body == null)
        {
            ReportWhatIsActuallyThere();
            return;
        }

        body.Unlock();
        Debug.Log($"<b>[Birthday]</b> Body '{Path(body.transform)}' unlocked. Carry the head over and press E.");
    }

    // -------------------------------------------------------------------

    /// <summary>
    /// Counts every instance of a component in the scene. More than one is the
    /// failure that looks exactly like "the wiring is correct but nothing happens",
    /// because the event fires on a different object than the one she is at.
    /// </summary>
    private static int ReportInstances<T>(System.Text.StringBuilder sb, string label) where T : Component
    {
        var all = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (all.Length <= 1) return 0;

        sb.AppendLine($"\n    PROBLEM: {all.Length} {label} components in the scene. There should be one.");
        foreach (var c in all)
            sb.AppendLine($"      {Path(c.transform)}" +
                          (c.gameObject.activeInHierarchy ? "" : "   (inactive)"));

        sb.AppendLine($"      The events point at ONE of these. If she interacts with the other, nothing happens.");
        sb.AppendLine($"      Delete the leftover, then run Repair Ending Wiring.");
        return 1;
    }

    private static string Listeners(UnityEventBase evt)
    {
        if (evt == null) return "NULL";

        int n = evt.GetPersistentEventCount();
        if (n == 0) return "no listeners";

        var parts = new List<string>();
        for (int i = 0; i < n; i++)
        {
            var t = evt.GetPersistentTarget(i);
            string target = t != null ? t.GetType().Name : "MISSING TARGET";

            // A listener set to Off is wired, shows in the Inspector, and never
            // fires. It looks completely correct until you check this.
            var state = evt.GetPersistentListenerState(i);
            string flag = state == UnityEventCallState.Off ? "  <- DISABLED, never fires" : "";

            parts.Add($"{target}.{evt.GetPersistentMethodName(i)}{flag}");
        }

        return $"{n} -> " + string.Join(", ", parts);
    }

    /// <summary>Any persistent listener that is present but will never fire.</summary>
    private static int CountDisabled(UnityEventBase evt)
    {
        if (evt == null) return 0;

        int n = 0;
        for (int i = 0; i < evt.GetPersistentEventCount(); i++)
            if (evt.GetPersistentListenerState(i) == UnityEventCallState.Off) n++;

        return n;
    }

    private static bool HasListener(UnityEventBase evt, Object target, string method)
    {
        if (evt == null || target == null) return false;

        for (int i = 0; i < evt.GetPersistentEventCount(); i++)
        {
            if (evt.GetPersistentMethodName(i) != method) continue;

            var t = evt.GetPersistentTarget(i);
            if (t == target) return true;

            // The Inspector may store the component or its GameObject.
            if (t is Component c && target is Component tc && c.gameObject == tc.gameObject
                && c.GetType() == tc.GetType()) return true;
        }

        return false;
    }

    /// <summary>
    /// When the search comes up empty, dump everything so we can see why instead
    /// of guessing: which scenes are loaded, what is at their roots, and every
    /// BodyReattach that exists anywhere at all, including inside prefab assets.
    /// </summary>
    private static void ReportWhatIsActuallyThere()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<b>[Birthday] Could not find BodyReattach. Here is what is actually loaded.</b>\n");

        int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCount;
        sb.AppendLine($"  Play mode: {Application.isPlaying}");
        sb.AppendLine($"  Loaded scenes: {sceneCount}\n");

        for (int i = 0; i < sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            sb.AppendLine($"  Scene '{scene.name}'  loaded={scene.isLoaded}  path={scene.path}");

            if (!scene.isLoaded) continue;

            var roots = scene.GetRootGameObjects();
            sb.AppendLine($"    {roots.Length} root object(s):");
            foreach (var r in roots)
                sb.AppendLine($"      {r.name}{(r.activeInHierarchy ? "" : "  (inactive)")}");

            sb.AppendLine();
        }

        // The nuclear option: everything Unity has in memory, scene or asset.
        var everything = Resources.FindObjectsOfTypeAll<BodyReattach>();
        sb.AppendLine($"  BodyReattach components anywhere in memory: {everything.Length}");

        foreach (var b in everything)
        {
            string where = b.gameObject.scene.IsValid()
                ? $"scene '{b.gameObject.scene.name}'"
                : "NOT IN A SCENE (prefab asset or preview)";

            sb.AppendLine($"    {Path(b.transform)}   {where}   active={b.gameObject.activeInHierarchy}");
        }

        if (everything.Length == 0)
            sb.AppendLine("\n  <b>There is no BodyReattach anywhere. The component was removed from your body " +
                          "object, or the body object itself was deleted. Undo, or re-add the component.</b>");
        else
            sb.AppendLine("\n  <b>It exists but not in a loaded scene. Easiest fix: select the body in the " +
                          "Hierarchy, right-click the BodyReattach component header in the Inspector, and " +
                          "choose 'Unlock Now (testing)'.</b>");

        Debug.LogWarning(sb.ToString());
    }

    /// <summary>
    /// Searches the real loaded scenes only. Unity's FindObjectsByType can return
    /// objects from the Prefab Mode preview scene when a prefab is open for
    /// editing, which makes tools mysteriously report that your scene is empty.
    /// </summary>
    private static T FindInLoadedScenes<T>() where T : Component
    {
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
        }

        return null;
    }

    private static string Path(Transform t)
    {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }
}
