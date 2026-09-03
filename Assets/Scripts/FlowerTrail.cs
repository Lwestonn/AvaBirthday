using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Flowers bloom behind her while she carries the head. The head is what makes
/// things grow, so this is the visual reason to want to carry it.
///
/// Put this on the player. Fill the Flowers list with as many kinds as you like:
/// one is a novelty, four or five is a meadow. Weight controls how often each
/// kind turns up, so her favourite can be the common one.
///
/// The finale uses the same list, so anything you add here also carpets the map
/// at the end.
/// </summary>
public class FlowerTrail : MonoBehaviour
{
    [System.Serializable]
    public class FlowerKind
    {
        [Tooltip("The flower model. Any prefab works.")]
        public GameObject prefab;

        [Tooltip("How often this one is picked, relative to the others. Two kinds at " +
                 "1 and 3 means the second shows up three times as often.")]
        [Range(0f, 10f)] public float weight = 1f;

        [Tooltip("Random size range, so copies of the same flower do not look stamped.")]
        public Vector2 scaleRange = new(0.85f, 1.2f);

        [Tooltip("Fixed rotation correction for models that were exported lying on their side. " +
                 "X -90 stands up most Z-up models. Applied before the random spin, so the flower " +
                 "still turns on the spot afterwards.")]
        public Vector3 rotationOffset;
    }

    [Header("Flowers")]
    [Tooltip("Every kind that can bloom. Add several for variety.")]
    public FlowerKind[] flowers;

    [Tooltip("Old single-flower slot. Only used if the list above is empty, so nothing " +
             "breaks if you have not filled the list in yet.")]
    public GameObject flowerPrefab;

    [Header("Wiring")]
    [Tooltip("Leave empty to find the HeadCarrier on this object.")]
    public HeadCarrier carrier;

    [Header("Rules")]
    [Tooltip("Only bloom while she is actually carrying the head.")]
    public bool requireHeld = true;

    [Tooltip("Metres of walking between flowers. Lower is denser and heavier.")]
    public float spacing = 1.1f;

    [Tooltip("Sideways scatter so it reads as a meadow, not a dotted line.")]
    public float sideScatter = 1.3f;

    [Tooltip("How far behind her they appear.")]
    public float trailBehind = 0.6f;

    [Header("Growing in")]
    [Tooltip("Pop each flower up from nothing as it appears, instead of it blinking into existence. " +
             "Adds a GrowIn component automatically, so your prefabs need no setup.")]
    public bool growIn = true;

    [Tooltip("Seconds for a flower to reach full size.")]
    public float growTime = 0.55f;

    [Tooltip("How far past full size it springs before settling. 0 = no bounce.")]
    public float growOvershoot = 0.22f;

    [Header("Variation")]
    [Tooltip("Random spin on the spot. Without this every flower faces the same way and it shows.")]
    public bool randomYaw = true;

    [Tooltip("Lean with the slope. Looks right on hills, slightly odd on flat ground.")]
    public bool alignToGround = true;

    [Tooltip("How much of the ground's tilt to take, 0 to 1. Part way usually looks best.")]
    [Range(0f, 1f)] public float groundAlignAmount = 0.6f;

    [Header("Performance")]
    [Tooltip("Hard cap on TRAIL flowers. Oldest are recycled past this. Finale flowers are " +
             "never recycled, so the ending carpet stays put.")]
    public int maxFlowers = 300;

    [Header("Ground detection")]
    public LayerMask groundLayers = ~0;
    public float rayHeight = 3f;
    public float rayDistance = 8f;

    [Tooltip("If no ground is found, skip the flower entirely instead of leaving it hanging " +
             "in the air. This is what stops the ending bloom putting flowers off the side of a cliff.")]
    public bool requireGround = true;

    [Tooltip("Skip anything steeper than this, in degrees. Flowers growing out of a cliff face " +
             "look wrong even when they are technically touching it.")]
    [Range(0f, 90f)] public float maxGroundAngle = 50f;

    [Tooltip("Only grow on the Terrain, never on props, the pier or the water. Turn on if you see " +
             "flowers sprouting out of rocks and tree canopies.")]
    public bool terrainOnly;

    [Header("Ending bloom")]
    [Tooltip("The ending scatters flowers over a wide radius, and the ground there can be well " +
             "below the tree. These give that pass a longer reach so it can find the ground on slopes.")]
    public float finaleRayHeight = 8f;

    public float finaleRayDistance = 40f;

    private readonly Queue<GameObject> _spawned = new();
    private Vector3 _lastSpawnPos;
    private Transform _container;
    private float _totalWeight;
    private int _validKinds;

    private void Awake()
    {
        if (carrier == null) carrier = GetComponent<HeadCarrier>();
        _lastSpawnPos = transform.position;

        var go = new GameObject("FlowerTrail_Spawned");
        _container = go.transform;   // keeps the Hierarchy from filling with 300 items at root

        CacheWeights();
    }

    private void CacheWeights()
    {
        _totalWeight = 0f;
        _validKinds = 0;

        if (flowers == null) return;

        foreach (var f in flowers)
        {
            if (f == null || f.prefab == null) continue;

            _validKinds++;
            _totalWeight += Mathf.Max(0f, f.weight);
        }

        // Adding entries in the Inspector can leave every weight at zero, which
        // would otherwise mean nothing is ever picked. Treat that as "all equal"
        // rather than as "no flowers".
        if (_validKinds > 0 && _totalWeight <= 0f)
            _totalWeight = _validKinds;
    }

    private bool HasAnyFlower => _validKinds > 0 || flowerPrefab != null;

    /// <summary>
    /// Says out loud what it thinks it has, because "I added flowers and nothing
    /// happens" is otherwise a guessing game.
    /// </summary>
    private void Start()
    {
        if (_validKinds == 0 && flowerPrefab == null)
        {
            Debug.LogWarning("[FlowerTrail] No flowers assigned. Fill the Flowers list on the player, " +
                             "and make sure each entry actually has a Prefab in it.", this);
            return;
        }

        if (requireHeld && carrier == null)
            Debug.LogWarning("[FlowerTrail] Require Held is on but there is no HeadCarrier on this object, " +
                             "so nothing will ever bloom. Either put FlowerTrail on the player, or untick " +
                             "Require Held.", this);
    }

    private void Update()
    {
        if (!HasAnyFlower) return;
        if (requireHeld && (carrier == null || !carrier.IsHolding)) return;

        Vector3 pos = transform.position;
        if (Vector3.Distance(pos, _lastSpawnPos) < spacing) return;

        _lastSpawnPos = pos;

        Vector3 side = transform.right * Random.Range(-sideScatter, sideScatter);
        Vector3 back = -transform.forward * trailBehind;

        var flower = Place(pos + side + back, rayHeight, rayDistance);
        if (flower == null) return;

        // Only trail flowers are recycled. The finale carpet is permanent.
        _spawned.Enqueue(flower);
        while (_spawned.Count > maxFlowers)
        {
            var old = _spawned.Dequeue();
            if (old != null) Destroy(old);
        }
    }

    /// <summary>
    /// Used by the finale to carpet the map. Ignores spacing, never recycled, and
    /// searches further down for ground because the bloom reaches over slopes.
    /// Returns null where there is nowhere sensible to grow.
    /// </summary>
    public GameObject SpawnAt(Vector3 worldPos) => Place(worldPos, finaleRayHeight, finaleRayDistance);

    // ---------------------------------------------------------------------

    private GameObject Place(Vector3 target, float castUp, float castDown)
    {
        var kind = Pick();
        if (kind == null) return null;

        Quaternion rot = Quaternion.identity;

        // Drop it onto whatever ground is beneath, so it works on slopes.
        if (Physics.Raycast(target + Vector3.up * castUp, Vector3.down,
                            out RaycastHit hit, castDown, groundLayers, QueryTriggerInteraction.Ignore))
        {
            if (terrainOnly && hit.collider is not TerrainCollider) return null;

            // A flower growing sideways out of a cliff face reads as a glitch.
            if (Vector3.Angle(hit.normal, Vector3.up) > maxGroundAngle) return null;

            target = hit.point;

            if (alignToGround && groundAlignAmount > 0f)
            {
                Quaternion tilt = Quaternion.FromToRotation(Vector3.up, hit.normal);
                rot = Quaternion.Slerp(Quaternion.identity, tilt, groundAlignAmount);
            }
        }
        else if (requireGround)
        {
            // Nothing underneath. Better to have no flower here than one hanging
            // in mid air off the side of the island.
            return null;
        }

        if (randomYaw)
            rot *= Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        // Applied last in the chain, which means first to the model, so a flower
        // exported lying on its side is stood upright before anything else.
        if (kind.rotationOffset != Vector3.zero)
            rot *= Quaternion.Euler(kind.rotationOffset);

        var flower = Instantiate(kind.prefab, target, rot, _container);

        // Switch it off while we configure it. GrowIn runs on enable and reads
        // localScale as its target, so adding it to a live object would start the
        // animation before the scale and timing are set.
        flower.SetActive(false);

        // A Scale Range left at 0,0 would spawn every flower at zero size: they
        // are all there, perfectly placed, and completely invisible. Guard it.
        float lo = kind.scaleRange.x;
        float hi = kind.scaleRange.y;
        if (hi <= 0.001f) { lo = 1f; hi = 1f; }
        else if (lo <= 0.001f) lo = hi;

        float s = Random.Range(lo, hi);
        flower.transform.localScale = kind.prefab.transform.localScale * s;

        if (growIn)
        {
            // GrowIn plays on enable and reads localScale as its target, so it has
            // to be configured before the object is switched on.
            var g = flower.GetComponent<GrowIn>();
            if (g == null) g = flower.AddComponent<GrowIn>();

            g.duration = growTime;
            g.overshoot = growOvershoot;
            g.delay = 0f;

            // Rotation and size are already handled above. Letting GrowIn do them
            // again would fight the ground alignment and double the scatter.
            g.randomYaw = false;
            g.scaleJitter = Vector2.one;
        }

        flower.SetActive(true);
        return flower;
    }

    /// <summary>Weighted random pick, falling back to the old single slot.</summary>
    private FlowerKind Pick()
    {
        if (_totalWeight <= 0f)
        {
            // Recompute once in case the list was filled in after Awake.
            CacheWeights();

            if (_totalWeight <= 0f)
                return flowerPrefab != null
                    ? new FlowerKind { prefab = flowerPrefab, scaleRange = new Vector2(0.9f, 1.15f) }
                    : null;
        }

        float roll = Random.value * _totalWeight;

        foreach (var f in flowers)
        {
            if (f == null || f.prefab == null) continue;

            roll -= Mathf.Max(0f, f.weight);
            if (roll <= 0f) return f;
        }

        // Floating point can land past the end. Return the last valid one.
        for (int i = flowers.Length - 1; i >= 0; i--)
            if (flowers[i] != null && flowers[i].prefab != null) return flowers[i];

        return null;
    }
}
