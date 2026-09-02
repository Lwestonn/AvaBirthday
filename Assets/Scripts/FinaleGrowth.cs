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
            var tree = Instantiate(treePrefab, center, Quaternion.identity);
            tree.transform.localScale = treeScale;
            tree.SetActive(true);

            var grow = tree.GetComponent<GrowIn>();
            if (grow == null) grow = tree.AddComponent<GrowIn>();
            grow.duration = treeGrowTime;
            grow.overshoot = 0.06f;      // a big tree should not boing
            grow.scaleJitter = Vector2.one;
            grow.enabled = true;
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
        float startTime = Time.time;

        while (spawned < flowerCount)
        {
            for (int i = 0; i < flowersPerFrame && spawned < flowerCount; i++, spawned++)
            {
                // Spreads outward over time rather than popping in everywhere at once.
                float progress = bloomDuration > 0f
                    ? Mathf.Clamp01((Time.time - startTime) / bloomDuration)
                    : 1f;

                float r = bloomRadius * Mathf.Sqrt(Random.value) * Mathf.Max(0.15f, progress);
                float a = Random.Range(0f, Mathf.PI * 2f);

                Vector3 p = center + new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r);
                flowers.SpawnAt(p);
            }

            yield return null;
        }
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
