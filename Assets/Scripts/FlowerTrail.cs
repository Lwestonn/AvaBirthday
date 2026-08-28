using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Flowers bloom behind her while she carries the head. The head is what makes
/// things grow, so this is the visual reason to want to carry it.
///
/// Put this on the player. It needs a flower prefab; the builder makes a
/// placeholder one for you at Assets/Prefabs/Flower.prefab.
/// </summary>
public class FlowerTrail : MonoBehaviour
{
    [Header("Wiring")]
    public GameObject flowerPrefab;

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

    [Header("Performance")]
    [Tooltip("Hard cap. Oldest flowers are recycled past this. Keep modest for WebGL.")]
    public int maxFlowers = 300;

    [Header("Ground detection")]
    public LayerMask groundLayers = ~0;
    public float rayHeight = 3f;
    public float rayDistance = 8f;

    private readonly Queue<GameObject> _spawned = new();
    private Vector3 _lastSpawnPos;
    private Transform _container;

    private void Awake()
    {
        if (carrier == null) carrier = GetComponent<HeadCarrier>();
        _lastSpawnPos = transform.position;

        var go = new GameObject("FlowerTrail_Spawned");
        _container = go.transform;   // keeps the Hierarchy from filling with 300 items at root
    }

    private void Update()
    {
        if (flowerPrefab == null) return;
        if (requireHeld && (carrier == null || !carrier.IsHolding)) return;

        Vector3 pos = transform.position;
        if (Vector3.Distance(pos, _lastSpawnPos) < spacing) return;

        _lastSpawnPos = pos;
        Spawn(pos);
    }

    private void Spawn(Vector3 near)
    {
        Vector3 side = transform.right * Random.Range(-sideScatter, sideScatter);
        Vector3 back = -transform.forward * trailBehind;
        Vector3 target = near + side + back;

        // Drop it onto whatever ground is beneath, so it works on slopes later.
        if (Physics.Raycast(target + Vector3.up * rayHeight, Vector3.down,
                            out RaycastHit hit, rayDistance, groundLayers, QueryTriggerInteraction.Ignore))
        {
            target = hit.point;
        }
        else
        {
            target.y = near.y;
        }

        var flower = Instantiate(flowerPrefab, target, Quaternion.identity, _container);
        flower.SetActive(true);

        _spawned.Enqueue(flower);
        while (_spawned.Count > maxFlowers)
        {
            var old = _spawned.Dequeue();
            if (old != null) Destroy(old);
        }
    }

    /// <summary>Used by the finale to carpet the map. Ignores the spacing rule.</summary>
    public GameObject SpawnAt(Vector3 worldPos)
    {
        if (flowerPrefab == null) return null;

        if (Physics.Raycast(worldPos + Vector3.up * rayHeight, Vector3.down,
                            out RaycastHit hit, rayDistance, groundLayers, QueryTriggerInteraction.Ignore))
        {
            worldPos = hit.point;
        }

        var flower = Instantiate(flowerPrefab, worldPos, Quaternion.identity, _container);
        flower.SetActive(true);
        _spawned.Enqueue(flower);
        return flower;
    }
}
