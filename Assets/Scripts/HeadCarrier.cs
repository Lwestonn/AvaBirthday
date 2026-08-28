using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Goes on the player (PlayerArmature). Handles finding, picking up, dropping,
/// and throwing the head.
///
/// Reads Keyboard/Mouse directly instead of going through the Starter Assets
/// InputActionAsset. That is deliberate: editing the Starter Assets action map
/// to add two new bindings is fiddly and easy to break, and this is a single
/// player game that will never need rebindable controls.
///
/// Controls:
///   E                 pick up the head when close, or put it down when held
///   Hold Left Mouse   wind up a throw AND turn to face where the camera looks
///   Release           throw along the direction she is facing
///
/// IMPORTANT: add this component to PlayerControlLock's "Components To Disable"
/// list, so she cannot throw the head while a memory is open.
/// </summary>
public class HeadCarrier : MonoBehaviour
{
    [Header("Carry point")]
    [Tooltip("Empty child of the player where the head rides. Chest height, slightly in front.")]
    public Transform carryPoint;

    [Header("Pickup")]
    public float pickupRange = 2.5f;
    [Tooltip("Layer(s) the head is on. Leave as Everything if you are not using layers.")]
    public LayerMask headLayers = ~0;

    [Header("Throw")]
    public float minThrowForce = 6f;
    public float maxThrowForce = 18f;
    [Tooltip("Seconds of holding the mouse to reach max force.")]
    public float maxChargeTime = 1.2f;
    [Tooltip("Upward tilt on the throw so it arcs instead of firing flat.")]
    public float throwArc = 0.35f;

    [Header("Aiming")]
    [Tooltip("While charging, turn the character to face where the camera is looking.")]
    public bool faceAimWhileCharging = true;

    [Tooltip("Degrees per second she turns to line up the throw.")]
    public float aimTurnSpeed = 540f;

    [Tooltip("Camera used to decide the aim direction. Leave empty to use Camera.main.")]
    public Transform aimSource;

    private HeadPickup _held;
    private HeadPickup _nearby;
    private float _chargeStart = -1f;

    /// <summary>0 to 1 while winding up. Negative when not charging. Read by ChargeBarUI.</summary>
    public float ChargeNormalized =>
        _chargeStart < 0f ? -1f : Mathf.Clamp01((Time.time - _chargeStart) / maxChargeTime);

    public bool IsCharging => _chargeStart >= 0f && _held != null;
    public bool IsHolding => _held != null;
    public HeadPickup Held => _held;
    public HeadPickup Nearby => _nearby;

    private void Awake()
    {
        if (aimSource == null && Camera.main != null)
            aimSource = Camera.main.transform;

        if (carryPoint == null)
        {
            var go = new GameObject("CarryPoint");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0.42f, 1.28f, 0.42f);
            carryPoint = go.transform;
        }
    }

    private void Update()
    {
        RefreshNearby();

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        var mouse = Mouse.current;

        if (kb != null && kb.eKey.wasPressedThisFrame) TogglePickup();

        if (mouse != null && _held != null)
        {
            if (mouse.leftButton.wasPressedThisFrame) _chargeStart = Time.time;
            if (mouse.leftButton.wasReleasedThisFrame && _chargeStart >= 0f) ReleaseThrow();
        }
#else
        if (Input.GetKeyDown(KeyCode.E)) TogglePickup();

        if (_held != null)
        {
            if (Input.GetMouseButtonDown(0)) _chargeStart = Time.time;
            if (Input.GetMouseButtonUp(0) && _chargeStart >= 0f) ReleaseThrow();
        }
#endif
    }

    private void LateUpdate()
    {
        if (!faceAimWhileCharging || !IsCharging) return;

        // Runs in LateUpdate deliberately. ThirdPersonController sets rotation in
        // Update, so doing this later means we win the argument and she actually
        // turns to face the throw instead of snapping back to her movement heading.
        Vector3 fwd = aimSource != null ? aimSource.forward : transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) return;

        Quaternion target = Quaternion.LookRotation(fwd.normalized);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, aimTurnSpeed * Time.deltaTime);
    }

    private void OnDisable()
    {
        // If the player gets locked mid-charge, cancel rather than firing on unlock.
        _chargeStart = -1f;
    }

    private void RefreshNearby()
    {
        if (_held != null) { _nearby = null; return; }

        _nearby = null;
        float best = float.MaxValue;

        var hits = Physics.OverlapSphere(transform.position + Vector3.up, pickupRange, headLayers, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            var head = h.GetComponentInParent<HeadPickup>();
            if (head == null || head.IsHeld) continue;

            float d = Vector3.SqrMagnitude(head.transform.position - transform.position);
            if (d < best) { best = d; _nearby = head; }
        }
    }

    private void TogglePickup()
    {
        if (_held != null)
        {
            _held.Drop();
            _held = null;
            _chargeStart = -1f;
            return;
        }

        if (_nearby != null)
        {
            _held = _nearby;
            _held.PickUp(carryPoint);
            _nearby = null;
        }
    }

    private void ReleaseThrow()
    {
        if (_held == null) { _chargeStart = -1f; return; }

        float charge = Mathf.Clamp01((Time.time - _chargeStart) / maxChargeTime);
        float force = Mathf.Lerp(minThrowForce, maxThrowForce, charge);

        // Throw along the CHARACTER's facing, not the camera's. She has been turning
        // to match the camera during the wind-up, so this is where she is pointed.
        Vector3 dir = transform.forward;
        dir.y = 0f;
        dir = (dir.normalized + Vector3.up * throwArc).normalized;

        _held.Throw(dir * force);
        _held = null;
        _chargeStart = -1f;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.8f, 0.35f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up, pickupRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position + Vector3.up, transform.forward * 3f);
    }
}
