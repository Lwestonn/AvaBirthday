using UnityEngine;

/// <summary>
/// Put this on a collectible object in the world.
/// Requirements on the GameObject:
///   - a Sphere Collider with "Is Trigger" ticked (radius ~1.5 works well)
///   - the visual mesh as a CHILD object (so the bob/spin does not move the collider)
/// The player should have the tag "Player" (Starter Assets sets this for you).
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class MemoryPickup : MonoBehaviour
{
    [Header("Content")]
    public MemoryData memory;

    [Header("Visuals")]
    [Tooltip("The child object that spins and bobs. Leave empty to use the first child.")]
    public Transform visual;
    public float spinSpeed = 45f;
    public float bobHeight = 0.25f;
    public float bobSpeed = 1.5f;

    [Header("Feedback")]
    public AudioClip collectSound;
    [Tooltip("Can be a scene object or a prefab. Either way it gets instantiated at the pickup position.")]
    public ParticleSystem collectEffect;

    private Vector3 _startPos;
    private bool _collected;

    private void Reset()
    {
        // Convenience: auto-configure the collider when you add the component.
        var col = GetComponent<SphereCollider>();
        if (col != null)
        {
            col.isTrigger = true;
            col.radius = 1.5f;
        }
    }

    private void Awake()
    {
        if (visual == null && transform.childCount > 0)
            visual = transform.GetChild(0);

        if (visual != null)
            _startPos = visual.localPosition;
    }

    private void Update()
    {
        if (_collected || visual == null) return;

        visual.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        float y = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        visual.localPosition = _startPos + new Vector3(0f, y, 0f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_collected) return;

        // Tag is the fast path. The CharacterController fallback saves you if you
        // forget to tag the player, which is a very easy mistake to make.
        bool isPlayer = other.CompareTag("Player")
                        || other.GetComponentInParent<CharacterController>() != null;
        if (!isPlayer) return;

        if (memory == null)
        {
            Debug.LogWarning($"[MemoryPickup] '{name}' has no MemoryData assigned.", this);
            return;
        }

        var manager = MemoryManager.Instance;
        if (manager == null)
        {
            // Do NOT mark as collected. Otherwise this memory is gone for good
            // and she can never finish the game.
            Debug.LogError("[MemoryPickup] No MemoryManager found in the scene.", this);
            return;
        }

        _collected = true;

        if (collectSound != null)
            AudioSource.PlayClipAtPoint(collectSound, transform.position);

        if (collectEffect != null)
        {
            // Instantiate so this works whether the field points at a prefab asset
            // or a child in the scene. Reparenting a prefab asset would throw.
            var fx = Instantiate(collectEffect, transform.position, Quaternion.identity);
            fx.Play();
            Destroy(fx.gameObject, 3f);
        }

        manager.Collect(memory);

        // Hide rather than Destroy, so audio finishes cleanly.
        if (visual != null) visual.gameObject.SetActive(false);
        GetComponent<SphereCollider>().enabled = false;
    }

    private void OnDrawGizmos()
    {
        // Makes unassigned pickups obvious in the Scene view: red means no memory.
        Gizmos.color = memory != null ? memory.glowColor : Color.red;
        Gizmos.DrawWireSphere(transform.position, 1.5f);
    }
}
