using System.Collections;
using UnityEngine;

/// <summary>
/// The ending. No fade, no credits, no freeze.
///
/// When she puts the head back on, a big tree grows behind the body and flowers
/// bloom across the whole map. Then she is just left standing in it, free to
/// walk around. That is the end of the game.
///
/// Hook BodyReattach.onReattached to Play().
/// </summary>
public class FinaleGrowth : MonoBehaviour
{
    [Header("Tree")]
    public GameObject treePrefab;

    [Tooltip("Where the tree grows. Leave empty to use behind this object.")]
    public Transform treeAnchor;

    [Tooltip("If no anchor, how far behind the body the tree grows.")]
    public float treeBehindDistance = 4f;

    public float treeGrowTime = 4.5f;
    public Vector3 treeScale = new(2.2f, 2.2f, 2.2f);

    [Tooltip("Lift the tree so the bottom of its mesh sits exactly on the anchor point. " +
             "Turn this on when a model's origin is at its middle rather than at the foot of the " +
             "trunk, which buries half the tree. Works at any scale, so you can resize freely.")]
    public bool sitOnGround = true;

    [Tooltip("Extra nudge after the automatic fit, if you want the trunk slightly sunk into the ground.")]
    public float treeYNudge = 0f;

    [Header("Flower bloom")]
    [Tooltip("Leave empty to find the FlowerTrail on the player.")]
    public FlowerTrail flowers;

    public int flowerCount = 220;

    [Tooltip("Radius of the bloom, centred on the tree.")]
    public float bloomRadius = 38f;

    [Tooltip("Seconds over which the bloom spreads outward. 0 = all at once.")]
    public float bloomDuration = 6f;

    [Tooltip("Flowers spawned per frame. Keep low so WebGL does not hitch.")]
    public int flowersPerFrame = 3;

    [Header("Audio")]
    public AudioSource music;
    public AudioClip finaleMusic;
    public float musicFadeTime = 2.5f;
    public AudioClip growSound;

    [Header("Head")]
    [Tooltip("What he says once his head is back on. Each entry is one speech bubble, spoken in order. This is the last thing in the game, so it is worth several lines.")]
    [TextArea(2, 4)] public string[] finalLines =
    {
        "There you go. Good as new.",
        "Happy birthday, Ava.",
        "Stay as long as you want.",
    };

    [Tooltip("Base seconds each closing line stays up, before the per-character bonus.")]
    public float lineBaseTime = 2.0f;

    [Tooltip("Extra seconds per character, so longer lines linger.")]
    public float linePerCharTime = 0.05f;

    [Tooltip("Gap between closing lines.")]
    public float lineGap = 0.5f;

    private bool _played;

    public void Play()
    {
        if (_played) return;
        _played = true;

        // Silence idle chatter and reaction lines the instant his head is back on.
        // Otherwise he keeps complaining about being left in the grass while the
        // ending is playing, which undercuts the whole moment.
        var barks = FindFirstObjectByType<HeadBarks>();
        if (barks != null) barks.EndGameMode();

        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        if (flowers == null) flowers = FindFirstObjectByType<FlowerTrail>();

        Vector3 center = TreePosition();

        if (growSound != null)
            AudioSource.PlayClipAtPoint(growSound, center);

        if (music != null && finaleMusic != null)
            StartCoroutine(CrossfadeMusic());

        // --- the tree
        if (treePrefab != null)
        {
            // Grow from a holder whose origin sits ON the ground, with the tree
            // parented inside it and pushed up so its trunk meets that origin.
            //
            // Scaling the tree directly cannot look like growing: a scale of zero
            // collapses everything to the model's own origin, and for a model whose
            // origin is at its middle that point is floating in mid air. So it
            // appears in the sky and inflates. Scaling the holder collapses it to
            // the ground instead, and it rises out of the earth.
            var holder = new GameObject("BigTree");

            // Configure before it goes live. GrowIn starts on enable and reads the
            // current scale as its target, so adding it to an active object would
            // begin the animation with default timings.
            holder.SetActive(false);
            holder.transform.position = center + Vector3.up * treeYNudge;

            var tree = Instantiate(treePrefab, holder.transform);
            tree.transform.localRotation = Quaternion.identity;
            tree.transform.localScale = treeScale;
            tree.transform.localPosition = sitOnGround
                ? Vector3.up * (BottomOffset(treePrefab) * treeScale.y)
                : Vector3.zero;

            tree.SetActive(true);

            var grow = holder.AddComponent<GrowIn>();
            grow.duration = treeGrowTime;
            grow.overshoot = 0.06f;        // a big tree should not boing
            grow.scaleJitter = Vector2.one;
            grow.randomYaw = false;        // set the facing on the prefab instead
            grow.verticalLead = 0.6f;      // shoots up, then fills out
            grow.enabled = true;

            holder.SetActive(true);        // this is what starts the growth
        }

        // --- the bloom starts while he talks, so the world is changing behind him
        if (flowers != null && flowerCount > 0)
            StartCoroutine(Bloom(center));

        // --- his last words, once the tree is on its way up
        yield return new WaitForSeconds(1.2f);

        var head = FindFirstObjectByType<HeadBarks>();
        if (head != null && finalLines != null)
        {
            foreach (string entry in finalLines)
            {
                if (string.IsNullOrWhiteSpace(entry)) continue;

                // Split each entry into sentences and give every one its own bubble,
                // the same way memory notes are delivered. Writing a paragraph into
                // one field should not produce one wall of text.
                foreach (string line in MemoryNarrator.SplitIntoLines(entry))
                {
                    head.Say(line);
                    yield return new WaitForSeconds(lineBaseTime + line.Length * linePerCharTime);
                    yield return new WaitForSeconds(lineGap);
                }
            }
        }
    }

    private IEnumerator Bloom(Vector3 center)
    {
        int spawned = 0;
        int attempts = 0;

        // Positions over water, off cliffs, or on ground too steep to grow on are
        // now refused rather than left floating, so a rejected spot has to be
        // retried or the bloom comes out thin. The cap stops this spinning forever
        // if the radius is mostly sea.
        int maxAttempts = Mathf.Max(64, flowerCount * 8);

        float startTime = Time.time;

        while (spawned < flowerCount && attempts < maxAttempts)
        {
            for (int i = 0; i < flowersPerFrame && spawned < flowerCount && attempts < maxAttempts; i++)
            {
                attempts++;
                // Spreads outward over time rather than popping in everywhere at once.
                float progress = bloomDuration > 0f
                    ? Mathf.Clamp01((Time.time - startTime) / bloomDuration)
                    : 1f;

                float r = bloomRadius * Mathf.Sqrt(Random.value) * Mathf.Max(0.15f, progress);
                float a = Random.Range(0f, Mathf.PI * 2f);

                Vector3 p = center + new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r);

                if (flowers.SpawnAt(p) != null) spawned++;
            }

            yield return null;
        }

        if (spawned < flowerCount)
            Debug.Log($"[FinaleGrowth] Bloomed {spawned} of {flowerCount}. The rest had nowhere to grow, " +
                      $"usually sea or cliff inside the bloom radius. Shrink Bloom Radius or move the tree " +
                      $"if you want the full count.");
    }

    /// <summary>
    /// How far the prefab's geometry extends BELOW its own origin, in the prefab's
    /// own units. Read from the meshes rather than from Renderer.bounds, because
    /// renderer bounds are world space and only valid on a live, active object,
    /// and this has to work on a prefab asset before anything is spawned.
    /// </summary>
    private static float BottomOffset(GameObject prefab)
    {
        float lowest = 0f;
        bool any = false;

        var root = prefab.transform;
        Matrix4x4 toRoot = root.worldToLocalMatrix;

        foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;

            Bounds mb = mf.sharedMesh.bounds;
            Matrix4x4 m = toRoot * mf.transform.localToWorldMatrix;

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3(
                    (i & 1) == 0 ? -1f : 1f,
                    (i & 2) == 0 ? -1f : 1f,
                    (i & 4) == 0 ? -1f : 1f));

                float y = m.MultiplyPoint3x4(corner).y;

                if (!any || y < lowest) { lowest = y; any = true; }
            }
        }

        // Also handle rigged models, just in case.
        foreach (var smr in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.sharedMesh == null) continue;

            Bounds mb = smr.sharedMesh.bounds;
            Matrix4x4 m = toRoot * smr.transform.localToWorldMatrix;
            float y = m.MultiplyPoint3x4(mb.center - Vector3.up * mb.extents.y).y;

            if (!any || y < lowest) { lowest = y; any = true; }
        }

        // A negative lowest means the mesh hangs below the origin; lift by that much.
        return any ? -lowest : 0f;
    }

    private Vector3 TreePosition()
    {
        if (treeAnchor != null) return treeAnchor.position;

        Vector3 p = transform.position - transform.forward * treeBehindDistance;

        if (Physics.Raycast(p + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f))
            p = hit.point;

        return p;
    }

    private IEnumerator CrossfadeMusic()
    {
        float startVol = music.volume > 0.01f ? music.volume : 1f;

        float t = 0f;
        while (t < musicFadeTime)
        {
            t += Time.deltaTime;
            music.volume = Mathf.Lerp(startVol, 0f, t / musicFadeTime);
            yield return null;
        }

        music.clip = finaleMusic;
        music.loop = true;
        music.Play();

        t = 0f;
        while (t < musicFadeTime)
        {
            t += Time.deltaTime;
            music.volume = Mathf.Lerp(0f, startVol, t / musicFadeTime);
            yield return null;
        }

        music.volume = startVol;
    }
}
