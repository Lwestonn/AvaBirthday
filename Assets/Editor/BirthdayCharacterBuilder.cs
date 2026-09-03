using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Builds simple blob characters out of primitives: a rounded body, a ball head,
/// two dot eyes, floating hands.
///
/// The trick for the player is that nothing is skinned. The parts are parented
/// straight onto the existing rig's bones, so your Starter Assets animations move
/// them for free. Hands attached to hand bones and detached from the body is not
/// a compromise here, it is the look.
///
/// Tools > Birthday > Characters
/// </summary>
public class BirthdayCharacterBuilder : EditorWindow
{
    private const string MatFolder = "Assets/Materials";
    private const string PartsTag = "BlobPart";      // names carry this so cleanup is easy

    [SerializeField] private Color skinColor = new(0.97f, 0.96f, 0.95f);
    [SerializeField] private Color hairColor = new(0.09f, 0.08f, 0.10f);
    [SerializeField] private Color eyeColor = new(0.06f, 0.05f, 0.07f);
    [SerializeField] private Color blushColor = new(1f, 0.72f, 0.75f);

    [SerializeField] private float bodyWidth = 0.52f;
    [SerializeField] private float headSize = 0.58f;
    [SerializeField] private float handSize = 0.20f;
    [SerializeField] private float footSize = 0.19f;

    [SerializeField] private float eyeSize = 0.115f;
    [SerializeField] private float eyeSpread = 0.30f;

    [SerializeField] private float bodyHeight = 0.95f; // measured from the soles
    [SerializeField] private float headGap;            // fine tune the neck
    [SerializeField] private float handOutward = 0.03f;
    [SerializeField] private float footSink = 0.30f;
    [SerializeField] private float footForward = 0.45f;
    [SerializeField] private float carryClearance = 0.14f;

    [SerializeField] private bool addHair = true;
    [SerializeField] private float hairLength = 0.55f;
    [SerializeField] private bool carryInFront = true;
    [SerializeField] private bool addBlush;

    [SerializeField] private bool hideOriginalMesh = true;

    private Vector2 _scroll;

    [MenuItem("Tools/Birthday/Characters")]
    public static void Open()
    {
        var w = GetWindow<BirthdayCharacterBuilder>(true, "Blob Characters", true);
        w.minSize = new Vector2(360f, 520f);
        w.Show();
    }

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.HelpBox(
            "Simple rounded characters built from spheres and capsules. For the player the parts are " +
            "attached to the existing bones, so your walk and idle animations drive them with no rigging.",
            MessageType.Info);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Colours", EditorStyles.boldLabel);
        skinColor = EditorGUILayout.ColorField("Body", skinColor);
        hairColor = EditorGUILayout.ColorField("Hair", hairColor);
        eyeColor = EditorGUILayout.ColorField("Eyes", eyeColor);

        addBlush = EditorGUILayout.Toggle("Add blush", addBlush);
        if (addBlush) blushColor = EditorGUILayout.ColorField("Blush", blushColor);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Proportions", EditorStyles.boldLabel);
        bodyHeight = EditorGUILayout.Slider(
            new GUIContent("Body height", "Height of the body alone, from the soles to the neck. " +
                                          "The head is placed on top of it, so this cannot open a gap."),
            bodyHeight, 0.4f, 1.8f);

        bodyWidth = EditorGUILayout.Slider("Body width", bodyWidth, 0.3f, 1.0f);
        headSize = EditorGUILayout.Slider("Head size", headSize, 0.3f, 1.1f);
        handSize = EditorGUILayout.Slider("Hand size", handSize, 0.08f, 0.4f);
        footSize = EditorGUILayout.Slider("Foot size", footSize, 0.08f, 0.4f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Fine tuning", EditorStyles.boldLabel);

        headGap = EditorGUILayout.Slider(
            new GUIContent("Neck gap", "Negative sinks the head into the shoulders, positive lifts it clear. " +
                                       "Around -0.05 looks best: a little overlap reads as one creature."),
            headGap, -0.25f, 0.25f);

        footSink = EditorGUILayout.Slider(
            new GUIContent("Foot height", "How far up from the ground the feet sit."),
            footSink, 0f, 1.2f);

        footForward = EditorGUILayout.Slider(
            new GUIContent("Feet forward", "Pushes the feet out in front so they read as feet rather than " +
                                           "lumps on the bottom of the body."),
            footForward, 0f, 1.5f);

        carryClearance = EditorGUILayout.Slider(
            new GUIContent("Carry clearance", "Extra space between her body and the head she is carrying. " +
                                              "Raise it if the head clips into her while sprinting."),
            carryClearance, 0f, 0.5f);

        handOutward = EditorGUILayout.Slider(
            new GUIContent("Hands out", "Pushes the hands away from the sides so they read as separate."),
            handOutward, 0f, 0.2f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Face and hair", EditorStyles.boldLabel);
        eyeSize = EditorGUILayout.Slider("Eye size", eyeSize, 0.04f, 0.25f);
        eyeSpread = EditorGUILayout.Slider("Eye spread", eyeSpread, 0.1f, 0.6f);
        addHair = EditorGUILayout.Toggle("Hair", addHair);
        using (new EditorGUI.DisabledScope(!addHair))
            hairLength = EditorGUILayout.Slider(
                new GUIContent("    Hair length", "How far the hair hangs down the back. 0 is a short cap."),
                hairLength, 0f, 1.6f);

        EditorGUILayout.Space();
        carryInFront = EditorGUILayout.Toggle(
            new GUIContent("Carry the head in front", "Moves the carry point to chest height in front of " +
                                                      "her, so both hands can hold it. Off keeps it tucked " +
                                                      "under one arm."),
            carryInFront);

        EditorGUILayout.Space();
        hideOriginalMesh = EditorGUILayout.Toggle(
            new GUIContent("Hide the old model", "Switches off the existing mesh rather than deleting it, " +
                                                 "so you can always turn it back on."),
            hideOriginalMesh);

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        if (GUILayout.Button("Build Ava On The Player", GUILayout.Height(32f))) BuildPlayer();

        EditorGUILayout.Space();
        if (GUILayout.Button("Build Luke's Head", GUILayout.Height(26f))) BuildLukeHead();
        if (GUILayout.Button("Build Luke's Body (headless)", GUILayout.Height(26f))) BuildLukeBody();

        EditorGUILayout.Space();
        EditorGUILayout.Space();
        if (GUILayout.Button("Remove Everything This Tool Built")) RemoveAll();

        EditorGUILayout.EndScrollView();
    }

    // ===================================================================
    // Player
    // ===================================================================

    private void BuildPlayer()
    {
        var animator = FindPlayerAnimator();

        if (animator == null || !animator.isHuman)
        {
            EditorUtility.DisplayDialog("No humanoid player",
                "Could not find a player with a Humanoid Animator.\n\n" +
                "The parts attach to the rig's bones, so the model needs Animation Type set to " +
                "Humanoid on its Rig tab.", "OK");
            return;
        }

        var root = animator.transform;
        ClearParts(root);

        var skin = Mat("Char_Skin", skinColor);
        var hair = Mat("Char_Hair", hairColor);
        var eye = Mat("Char_Eye", eyeColor);
        var blush = Mat("Char_Blush", blushColor);

        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        Transform lHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        Transform rHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        Transform lFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        Transform rFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);

        if (hips == null || head == null)
        {
            EditorUtility.DisplayDialog("Rig incomplete", "The avatar has no Hips or Head bone mapped.", "OK");
            return;
        }

        // Measure the bottom from the ANKLE BONES, not from the root object.
        // The root sits wherever the rig author put it, which on Starter Assets is
        // not reliably at the soles.
        float feetY = root.position.y;
        if (lFoot != null && rFoot != null) feetY = Mathf.Min(lFoot.position.y, rFoot.position.y);
        else if (lFoot != null) feetY = lFoot.position.y;
        else if (rFoot != null) feetY = rFoot.position.y;

        // The body is an explicit height standing on the soles, and the head is
        // then placed ON TOP of it. Deriving the head from the head bone while the
        // body was trimmed independently is what left her decapitated: shortening
        // one did nothing to the other.
        // Lift the body so it rests on the feet instead of swallowing them. With
        // the capsule bottom at ground level the feet were buried inside it and
        // only their edges poked out.
        float bodyBottom = feetY + footSize * 0.45f;
        float bodyH = bodyHeight;
        float bodyTop = bodyBottom + bodyH;

        Vector3 bodyCentre = new(root.position.x, bodyBottom + bodyH * 0.5f, root.position.z);

        Part(hips, PrimitiveType.Capsule, "Body", bodyCentre,
             new Vector3(bodyWidth, bodyH * 0.5f, bodyWidth), root.rotation, skin);

        // ---- head, sitting on the shoulders by construction
        Vector3 headCentre = new(root.position.x,
                                 bodyTop + headSize * 0.38f + headGap,
                                 root.position.z);

        var headPart = Part(head, PrimitiveType.Sphere, "Head", headCentre,
                            Vector3.one * headSize, root.rotation, skin);

        BuildFace(headPart, root.rotation, eye, hair, blush);

        // ---- floating hands
        Vector3 rightDir = root.rotation * Vector3.right;

        if (lHand != null)
            Part(lHand, PrimitiveType.Sphere, "HandL", lHand.position - rightDir * handOutward,
                 Vector3.one * handSize, root.rotation, skin);

        if (rHand != null)
            Part(rHand, PrimitiveType.Sphere, "HandR", rHand.position + rightDir * handOutward,
                 Vector3.one * handSize, root.rotation, skin);

        // ---- little feet
        float footY = feetY + footSize * footSink;
        Vector3 fwdDir = root.rotation * Vector3.forward;
        Vector3 footNudge = fwdDir * (footSize * footForward);

        if (lFoot != null)
            Part(lFoot, PrimitiveType.Sphere, "FootL",
                 new Vector3(lFoot.position.x, footY, lFoot.position.z) + footNudge,
                 new Vector3(footSize, footSize * 0.7f, footSize * 1.35f), root.rotation, skin);

        if (rFoot != null)
            Part(rFoot, PrimitiveType.Sphere, "FootR",
                 new Vector3(rFoot.position.x, footY, rFoot.position.z) + footNudge,
                 new Vector3(footSize, footSize * 0.7f, footSize * 1.35f), root.rotation, skin);

        // ---- hands cradle the head while carrying
        var carrier = root.GetComponentInParent<HeadCarrier>();
        if (carrier == null) carrier = Object.FindFirstObjectByType<HeadCarrier>();

        if (carrier != null)
        {
            var hands = carrier.GetComponent<BlobHands>();
            if (hands == null) hands = Undo.AddComponent<BlobHands>(carrier.gameObject);

            Undo.RecordObject(hands, "Wire hands");
            hands.carrier = carrier;
            hands.handLeft = root.GetComponentsInChildren<Transform>(true)
                                 .FirstOrDefault(t => t.name == $"{PartsTag}_HandL");
            hands.handRight = root.GetComponentsInChildren<Transform>(true)
                                  .FirstOrDefault(t => t.name == $"{PartsTag}_HandR");
            hands.gripSpread = headSize * 0.46f;
            hands.gripDrop = headSize * 0.10f;
            hands.CaptureRest();
            EditorUtility.SetDirty(hands);

            // Put the head where two hands can plausibly reach it, and hang the
            // carry point off the CHEST bone rather than the player object.
            //
            // Sprinting leans her torso forward. With the carry point fixed to the
            // upright player object, her chest swung forward into a head that
            // stayed put, and it clipped straight through her. Riding the chest
            // means the head leans with her and the gap never closes.
            if (carryInFront && carrier.carryPoint != null)
            {
                Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest)
                               ?? animator.GetBoneTransform(HumanBodyBones.UpperChest)
                               ?? animator.GetBoneTransform(HumanBodyBones.Spine)
                               ?? hips;

                float reach = bodyWidth * 0.5f + headSize * 0.5f + carryClearance;

                Vector3 target = new Vector3(root.position.x,
                                             bodyTop - headSize * 0.10f,
                                             root.position.z)
                               + root.rotation * Vector3.forward * reach;

                Undo.RecordObject(carrier.carryPoint, "Move carry point");
                carrier.carryPoint.SetParent(chest, true);
                carrier.carryPoint.position = target;
                EditorUtility.SetDirty(carrier.carryPoint);
            }
        }

        if (hideOriginalMesh) HideOldMeshes(root);

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = root.gameObject;

        Debug.Log("<b>[Birthday]</b> Built Ava on the player rig.\n" +
                  "  The parts are parented to the bones, so your existing animations move them.\n" +
                  "  Press Play and walk around. Adjust the sliders and press the button again to rebuild.\n" +
                  "  Then re-run Tools > Birthday > Build Start Screen, because the menu character is a copy.");
    }

    // ===================================================================
    // Luke
    // ===================================================================

    private void BuildLukeHead()
    {
        var visual = FindVisual("LukeHead");
        if (visual == null) return;

        ClearParts(visual);

        var skin = Mat("Char_Skin", skinColor);
        var hair = Mat("Char_Hair", hairColor);
        var eye = Mat("Char_Eye", eyeColor);
        var blush = Mat("Char_Blush", blushColor);

        var headPart = Part(visual, PrimitiveType.Sphere, "Head", visual.position,
                            Vector3.one * headSize, visual.rotation, skin);

        BuildFace(headPart, visual.rotation, eye, hair, blush);

        if (hideOriginalMesh) HideOldMeshes(visual);

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = visual.gameObject;

        Debug.Log("<b>[Birthday]</b> Built Luke's head under LukeHead/Visual.\n" +
                  "  Check the collider on LukeHead still matches the new size, and that it is roughly " +
                  "centred, since this is what tumbles when she throws you.");
    }

    private void BuildLukeBody()
    {
        var visual = FindVisual("LukeBody");
        if (visual == null) return;

        ClearParts(visual);

        var skin = Mat("Char_Skin", skinColor);

        // No head on purpose. That is the whole premise.
        float bodyH = bodyHeight;

        Part(visual, PrimitiveType.Capsule, "Body", visual.position + Vector3.up * (bodyH * 0.1f),
             new Vector3(bodyWidth, bodyH * 0.5f, bodyWidth), visual.rotation, skin);

        // Hands hanging at his sides, doing nothing, which is funnier than arms.
        Vector3 right = visual.rotation * Vector3.right;
        Part(visual, PrimitiveType.Sphere, "HandL",
             visual.position + right * -(bodyWidth * 0.72f) + Vector3.up * (bodyH * 0.05f),
             Vector3.one * handSize, visual.rotation, skin);

        Part(visual, PrimitiveType.Sphere, "HandR",
             visual.position + right * (bodyWidth * 0.72f) + Vector3.up * (bodyH * 0.05f),
             Vector3.one * handSize, visual.rotation, skin);

        Vector3 fwd = visual.rotation * Vector3.forward;
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            Part(visual, PrimitiveType.Sphere, i == 0 ? "FootL" : "FootR",
                 visual.position + right * (side * bodyWidth * 0.26f) - Vector3.up * (bodyH * 0.52f) + fwd * 0.03f,
                 new Vector3(footSize, footSize * 0.7f, footSize * 1.35f), visual.rotation, skin);
        }

        if (hideOriginalMesh) HideOldMeshes(visual);

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = visual.gameObject;

        Debug.Log("<b>[Birthday]</b> Built Luke's headless body under LukeBody/Visual.\n" +
                  "  Now reposition NeckSocket to the top of the new body, and drag Mark_01 to Mark_08 " +
                  "onto its chest. Run Tools > Birthday > Check Ending afterwards.");
    }

    // ===================================================================
    // Pieces
    // ===================================================================

    private void BuildFace(Transform headPart, Quaternion facing, Material eye, Material hair, Material blush)
    {
        // Eyes sit slightly proud of the surface so they never z-fight with the head.
        float r = 0.5f;                       // sphere primitive radius in local units
        float outward = r * 0.94f;

        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;

            var e = Prim(PrimitiveType.Sphere, $"{PartsTag}_Eye{(i == 0 ? "L" : "R")}", headPart, eye);
            e.transform.localPosition = new Vector3(side * eyeSpread * r, r * 0.10f, outward);
            e.transform.localScale = new Vector3(eyeSize, eyeSize * 1.15f, eyeSize * 0.55f);
            e.transform.localRotation = Quaternion.identity;

            if (addBlush)
            {
                var b = Prim(PrimitiveType.Sphere, $"{PartsTag}_Blush{(i == 0 ? "L" : "R")}", headPart, blush);
                b.transform.localPosition = new Vector3(side * (eyeSpread + 0.22f) * r, -r * 0.16f, outward * 0.86f);
                b.transform.localScale = new Vector3(eyeSize * 1.5f, eyeSize * 0.9f, eyeSize * 0.4f);
                b.transform.localRotation = Quaternion.identity;
            }
        }

        if (!addHair) return;

        // A slightly oversized squashed sphere pushed back and up reads as a fringe
        // without needing any actual hair geometry.
        var cap = Prim(PrimitiveType.Sphere, $"{PartsTag}_Hair", headPart, hair);
        cap.transform.localPosition = new Vector3(0f, r * 0.23f, -r * 0.05f);
        cap.transform.localScale = new Vector3(1.05f, 0.84f, 1.05f);
        cap.transform.localRotation = Quaternion.identity;

        if (hairLength <= 0.01f) return;

        // ONE mass behind the head rather than two side pieces. Two separate
        // shapes beside the face read as pigtails no matter how they are placed;
        // a single volume that wraps the back and sides reads as hair.
        var back = Prim(PrimitiveType.Sphere, $"{PartsTag}_HairBack", headPart, hair);
        back.transform.localPosition = new Vector3(0f, -r * hairLength * 0.30f, -r * 0.26f);
        back.transform.localScale = new Vector3(1.0f, 0.70f + hairLength * 0.55f, 0.86f);
        back.transform.localRotation = Quaternion.identity;
    }

    /// <summary>
    /// Creates a primitive at a world position and parents it to a bone, converting
    /// into that bone's space. Bones point in all sorts of directions, so the part
    /// has to be told where it is in the world and then translated, not simply
    /// dropped in with a local offset.
    /// </summary>
    private Transform Part(Transform parent, PrimitiveType type, string name,
                           Vector3 worldPos, Vector3 worldScale, Quaternion worldRot, Material mat)
    {
        var go = Prim(type, $"{PartsTag}_{name}", parent, mat);
        var t = go.transform;

        t.position = worldPos;
        t.rotation = worldRot;

        Vector3 ls = parent.lossyScale;
        t.localScale = new Vector3(
            worldScale.x / Mathf.Max(0.0001f, ls.x),
            worldScale.y / Mathf.Max(0.0001f, ls.y),
            worldScale.z / Mathf.Max(0.0001f, ls.z));

        return t;
    }

    private GameObject Prim(PrimitiveType type, string name, Transform parent, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;

        // Primitives arrive with colliders. The player already has a
        // CharacterController and the head has its own collider, so these would
        // only ever cause trouble.
        var col = go.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);

        go.transform.SetParent(parent, true);
        go.layer = parent.gameObject.layer;

        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

        Undo.RegisterCreatedObjectUndo(go, "Build blob part");
        return go;
    }

    // ===================================================================
    // Housekeeping
    // ===================================================================

    private static Animator FindPlayerAnimator()
    {
        var cc = Object.FindObjectsByType<CharacterController>(FindObjectsSortMode.None)
            .FirstOrDefault(c => c.GetComponentInChildren<Animator>() != null);

        if (cc != null) return cc.GetComponentInChildren<Animator>();

        var tagged = GameObject.FindGameObjectWithTag("Player");
        return tagged != null ? tagged.GetComponentInChildren<Animator>() : null;
    }

    private static Transform FindVisual(string rootName)
    {
        var root = GameObject.Find(rootName);
        if (root == null)
        {
            EditorUtility.DisplayDialog("Not found",
                $"No object called '{rootName}' in the scene.", "OK");
            return null;
        }

        var visual = root.transform.Find("Visual");
        if (visual == null)
        {
            // Build into a Visual child rather than the root, so colliders and
            // scripts on the root are never disturbed.
            var go = new GameObject("Visual");
            go.transform.SetParent(root.transform, false);
            Undo.RegisterCreatedObjectUndo(go, "Create Visual");
            visual = go.transform;
        }

        return visual;
    }

    private static void ClearParts(Transform root)
    {
        var doomed = root.GetComponentsInChildren<Transform>(true)
            .Where(t => t != null && t.name.StartsWith(PartsTag))
            .Select(t => t.gameObject)
            .ToList();

        foreach (var go in doomed)
            if (go != null) Undo.DestroyObjectImmediate(go);
    }

    private static void HideOldMeshes(Transform root)
    {
        foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Undo.RecordObject(smr, "Hide old mesh");
            smr.enabled = false;
        }

        foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (mr.name.StartsWith(PartsTag)) continue;
            if (mr.GetComponent<TMPro.TMP_Text>() != null) continue;   // leave labels alone

            Undo.RecordObject(mr, "Hide old mesh");
            mr.enabled = false;
        }
    }

    private void RemoveAll()
    {
        int n = 0;

        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t == null || !t.name.StartsWith(PartsTag)) continue;
            Undo.DestroyObjectImmediate(t.gameObject);
            n++;
        }

        // Put the original meshes back on.
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (r.enabled) continue;
            Undo.RecordObject(r, "Restore mesh");
            r.enabled = true;
        }

        Debug.Log($"<b>[Birthday]</b> Removed {n} blob part(s) and re-enabled the original meshes.");
    }

    private static Material Mat(string name, Color color)
    {
        if (!Directory.Exists(MatFolder))
        {
            Directory.CreateDirectory(MatFolder);
            AssetDatabase.Refresh();
        }

        string path = $"{MatFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.shader = shader;
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color", color);

        // Matte. A shiny blob looks like plastic, a flat one looks drawn.
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.06f);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();

        return mat;
    }
}
