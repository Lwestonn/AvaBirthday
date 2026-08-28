using UnityEngine;
using UnityEditor;
using TMPro;

/// <summary>
/// Builds the head, the body, and the player's carry rig as grey placeholders,
/// fully wired. Swap the meshes for real art later; the logic never changes.
///
/// Menu: Tools > Birthday > Build Head and Body
/// Safe to re-run. It removes what it made and rebuilds.
/// </summary>
public static class BirthdaySceneBuilder
{
    private const string HeadName = "LukeHead";
    private const string BodyName = "LukeBody";
    private const string CarryPointName = "CarryPoint";

    [MenuItem("Tools/Birthday/Build Head and Body")]
    public static void BuildHeadAndBody()
    {
        if (TMP_Settings.instance == null)
        {
            EditorUtility.DisplayDialog("TextMeshPro not set up",
                "Window > TextMeshPro > Import TMP Essential Resources, then run this again.", "OK");
            return;
        }

        var player = FindPlayer();
        if (player == null)
        {
            EditorUtility.DisplayDialog("No player found",
                "Could not find a GameObject tagged 'Player'. Make sure PlayerArmature is in the scene and tagged.", "OK");
            return;
        }

        DestroyIfExists(HeadName);
        DestroyIfExists(BodyName);

        Vector3 origin = player.transform.position;

        var head = BuildHead(origin + player.transform.forward * 2.5f + Vector3.up * 0.5f);
        var body = BuildBody(origin + player.transform.forward * 10f);
        SetupCarrier(player, head);

        Selection.activeGameObject = head;
        Debug.Log("<b>[Birthday]</b> Head and body built. Now run Tools > Birthday > Rewire Events to connect them to the game manager.");
    }

    // ------------------------------------------------------------------ head

    private static GameObject BuildHead(Vector3 pos)
    {
        var root = new GameObject(HeadName);
        Undo.RegisterCreatedObjectUndo(root, "Create Head");
        root.transform.position = pos;

        // Visual. Replace this child with your real head model later.
        var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.name = "Visual";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localScale = Vector3.one * 0.45f;
        Object.DestroyImmediate(visual.GetComponent<SphereCollider>());   // root owns the collider
        Paint(visual, new Color(0.95f, 0.82f, 0.72f));

        var col = root.AddComponent<SphereCollider>();
        col.radius = 0.24f;

        var rb = root.AddComponent<Rigidbody>();
        rb.mass = 1f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        var pickup = root.AddComponent<HeadPickup>();
        pickup.killPlaneY = -15f;

        // Bark label
        var labelGo = new GameObject("BarkLabel", typeof(TextMeshPro));
        labelGo.transform.SetParent(root.transform, false);
        labelGo.transform.localPosition = new Vector3(0f, 0.65f, 0f);

        var label = labelGo.GetComponent<TextMeshPro>();
        label.text = "";
        label.fontSize = 3.2f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.outlineWidth = 0.25f;
        label.outlineColor = new Color32(20, 10, 20, 220);
        label.rectTransform.sizeDelta = new Vector2(6f, 1.5f);

        var barks = root.AddComponent<HeadBarks>();
        barks.label = label;

        return root;
    }

    // ------------------------------------------------------------------ body

    private static GameObject BuildBody(Vector3 pos)
    {
        var root = new GameObject(BodyName);
        Undo.RegisterCreatedObjectUndo(root, "Create Body");
        root.transform.position = pos;

        // Trigger zone lives on the root, visual lives underneath.
        var trigger = root.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 3f;
        trigger.center = Vector3.up;

        var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "Visual";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = new Vector3(0f, 0.85f, 0f);
        visual.transform.localScale = new Vector3(0.6f, 0.85f, 0.6f);
        Paint(visual, new Color(0.45f, 0.45f, 0.52f));

        var neck = new GameObject("NeckSocket");
        neck.transform.SetParent(root.transform, false);
        neck.transform.localPosition = new Vector3(0f, 1.85f, 0f);

        // Eight marks in an arc across the chest, one per memory.
        var marks = new Renderer[8];
        for (int i = 0; i < 8; i++)
        {
            var m = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            m.name = $"Mark_{i + 1:00}";
            m.transform.SetParent(root.transform, false);
            Object.DestroyImmediate(m.GetComponent<SphereCollider>());

            float a = Mathf.Lerp(-70f, 70f, i / 7f) * Mathf.Deg2Rad;
            m.transform.localPosition = new Vector3(Mathf.Sin(a) * 0.42f, 1.15f + Mathf.Cos(a) * 0.28f, 0.32f);
            m.transform.localScale = Vector3.one * 0.11f;

            Paint(m, new Color(0.25f, 0.25f, 0.3f));
            marks[i] = m.GetComponent<Renderer>();
        }

        var promptGo = new GameObject("PromptLabel", typeof(TextMeshPro));
        promptGo.transform.SetParent(root.transform, false);
        promptGo.transform.localPosition = new Vector3(0f, 2.6f, 0f);

        var prompt = promptGo.GetComponent<TextMeshPro>();
        prompt.text = "";
        prompt.fontSize = 3f;
        prompt.alignment = TextAlignmentOptions.Center;
        prompt.color = new Color(1f, 0.9f, 0.95f);
        prompt.outlineWidth = 0.25f;
        prompt.outlineColor = new Color32(20, 10, 20, 220);
        prompt.rectTransform.sizeDelta = new Vector2(10f, 2f);

        var body = root.AddComponent<BodyReattach>();
        body.neckSocket = neck.transform;
        body.promptLabel = prompt;
        body.progressMarks = marks;

        // Keep the prompt facing the camera.
        promptGo.AddComponent<Billboard>();

        return root;
    }

    // ---------------------------------------------------------------- player

    private static void SetupCarrier(GameObject player, GameObject head)
    {
        var existing = player.transform.Find(CarryPointName);
        if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

        var carry = new GameObject(CarryPointName);
        carry.transform.SetParent(player.transform, false);
        carry.transform.localPosition = new Vector3(0f, 1.35f, 0.55f);

        var carrier = player.GetComponent<HeadCarrier>();
        if (carrier == null) carrier = Undo.AddComponent<HeadCarrier>(player);

        Undo.RecordObject(carrier, "Setup HeadCarrier");
        carrier.carryPoint = carry.transform;
        if (Camera.main != null) carrier.aimSource = Camera.main.transform;

        // Make sure she cannot throw the head while a memory panel is open.
        var locker = player.GetComponent<PlayerControlLock>();
        if (locker != null)
        {
            var list = new System.Collections.Generic.List<MonoBehaviour>(locker.componentsToDisable ?? new MonoBehaviour[0]);
            if (!list.Contains(carrier))
            {
                Undo.RecordObject(locker, "Add carrier to lock list");
                list.Add(carrier);
                locker.componentsToDisable = list.ToArray();
                EditorUtility.SetDirty(locker);
            }
        }
        else
        {
            Debug.LogWarning("[Birthday] No PlayerControlLock on the player. Add it, then re-run so HeadCarrier gets added to its disable list.");
        }

        EditorUtility.SetDirty(carrier);
    }

    // --------------------------------------------------------------- helpers

    private static GameObject FindPlayer()
    {
        var tagged = GameObject.FindGameObjectWithTag("Player");
        if (tagged != null) return tagged;

        var locker = Object.FindFirstObjectByType<PlayerControlLock>();
        return locker != null ? locker.gameObject : null;
    }

    private static void Paint(GameObject go, Color c)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) return;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader) { name = go.name + "_Mat" };
        mat.color = c;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        r.sharedMaterial = mat;
    }

    private static void DestroyIfExists(string name)
    {
        var go = GameObject.Find(name);
        if (go != null) Undo.DestroyObjectImmediate(go);
    }
}
