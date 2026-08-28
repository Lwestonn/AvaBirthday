using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Removes "The referenced script (Unknown) on this Behaviour is missing!"
///
/// That warning means a component is attached to an object but its script file
/// was deleted or renamed, so Unity kept the slot and lost the class. It is
/// harmless at runtime but it never stops nagging, and it hides real errors.
///
/// Tools > Birthday > Remove Missing Scripts
/// </summary>
public static class BirthdayCleanup
{
    [MenuItem("Tools/Birthday/Remove Missing Scripts")]
    public static void RemoveMissingScripts()
    {
        var scene = EditorSceneManager.GetActiveScene();
        int objectsFixed = 0, componentsRemoved = 0;

        foreach (var root in scene.GetRootGameObjects())
        {
            // Include inactive children: menus and panels start disabled.
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var go = t.gameObject;
                int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                if (count == 0) continue;

                Debug.Log($"[Birthday] Removing {count} missing script(s) from '{GetPath(go)}'.", go);
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);

                objectsFixed++;
                componentsRemoved += count;
            }
        }

        if (componentsRemoved == 0)
        {
            Debug.Log("<b>[Birthday]</b> No missing scripts found. The scene is clean.");
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"<b>[Birthday]</b> Removed {componentsRemoved} missing script(s) across {objectsFixed} object(s). Save the scene.");
    }

    private static string GetPath(GameObject go)
    {
        string path = go.name;
        var t = go.transform.parent;
        while (t != null)
        {
            path = t.name + "/" + path;
            t = t.parent;
        }
        return path;
    }
}
