using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Luke's disembodied head. Lives its whole life in one of three states.
///
/// Setup on the head GameObject:
///   - Rigidbody (mass ~1, Continuous Dynamic collision detection)
///   - a Collider (Sphere or Capsule, NOT a trigger)
///   - this script
///   - optionally HeadBarks on the same object
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class HeadPickup : MonoBehaviour
{
    public enum State { Resting, Held, Airborne }

    [Header("Feel")]
    [Tooltip("How fast the head lerps to the carry point. Lower is floatier and funnier.")]
    public float followSpeed = 22f;
    public float followRotationSpeed = 12f;

    [Tooltip("Extra spin applied on throw so it tumbles.")]
    public float throwSpin = 8f;

    [Header("Collision")]
    [Tooltip("Turn the collider off while carried so the head cannot shove the player around. Leave this on.")]
    public bool disableColliderWhileHeld = true;

    [Tooltip("Grace period after release before the collider comes back.")]
    public float releaseColliderDelay = 0.05f;

    [Tooltip("How long the head ignores the thrower's own collider after release.")]
    public float ignoreThrowerTime = 0.5f;

    [Header("Safety net")]
    [Tooltip("If the head falls below this Y, it comes back. Set well under your ground level.")]
    public float killPlaneY = -15f;

    [Tooltip("Where the head returns to if it falls out of the world. Leave empty to return to the player.")]
    public Transform respawnPoint;

    [Header("Audio")]
    public AudioClip pickupSound;
    public AudioClip throwSound;
    public AudioClip[] bonkSounds;
    [Tooltip("Impact speed needed before a bonk sound plays.")]
    public float bonkThreshold = 3.5f;

    [Header("Events (HeadBarks listens to these)")]
    public UnityEvent onPickedUp;
    public UnityEvent onDropped;
    public UnityEvent onThrown;
    public UnityEvent onLanded;

    private Rigidbody _rb;
    private Collider _col;
    private Transform _carryPoint;
    private AudioSource _audio;
    private State _state = State.Resting;
    private float _airborneTime;
    private Coroutine _reenable;

    public State CurrentState => _state;
    public bool IsHeld => _state == State.Held;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _col = GetComponent<Collider>();

        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;

        _audio = GetComponent<AudioSource>();
        if (_audio == null)
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 1f;
        }
    }

    private void FixedUpdate()
    {
        if (_state == State.Held && _carryPoint != null)
        {
            // MovePosition rather than parenting. Parenting to an animated character
            // makes the head inherit every bone twitch and jitter badly.
            Vector3 target = Vector3.Lerp(_rb.position, _carryPoint.position, followSpeed * Time.fixedDeltaTime);
            _rb.MovePosition(target);

            Quaternion targetRot = Quaternion.Slerp(_rb.rotation, _carryPoint.rotation, followRotationSpeed * Time.fixedDeltaTime);
            _rb.MoveRotation(targetRot);
            return;
        }

        if (_state == State.Airborne)
        {
            _airborneTime += Time.fixedDeltaTime;

            if (_airborneTime > 0.4f && _rb.linearVelocity.magnitude < 0.6f)
            {
                _state = State.Resting;
                onLanded?.Invoke();
            }
        }

        if (transform.position.y < killPlaneY)
            Recover();
    }

    public void PickUp(Transform carryPoint)
    {
        _carryPoint = carryPoint;
        _state = State.Held;

        // Order matters. Unity throws "Setting linear velocity of a kinematic body
        // is not supported" if you zero these AFTER flipping isKinematic.
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.isKinematic = true;

        // A kinematic body still shoves a CharacterController around. Switching the
        // collider off is what stops the head from blocking her and pushing her sideways.
        if (disableColliderWhileHeld && _col != null)
        {
            if (_reenable != null) { StopCoroutine(_reenable); _reenable = null; }
            _col.enabled = false;
        }

        Play(pickupSound);
        onPickedUp?.Invoke();
    }

    public void Drop(Collider thrower = null)
    {
        if (_state != State.Held) return;

        _carryPoint = null;
        _rb.isKinematic = false;
        _state = State.Airborne;
        _airborneTime = 0f;

        RestoreCollider();
        IgnoreThrower(thrower);
        onDropped?.Invoke();
    }

    public void Throw(Vector3 impulse, Collider thrower = null)
    {
        if (_state != State.Held) return;

        _carryPoint = null;
        _rb.isKinematic = false;
        _state = State.Airborne;
        _airborneTime = 0f;

        _rb.AddForce(impulse, ForceMode.Impulse);
        _rb.AddTorque(Random.insideUnitSphere * throwSpin, ForceMode.Impulse);

        RestoreCollider();
        IgnoreThrower(thrower);
        Play(throwSound);
        onThrown?.Invoke();
    }

    /// <summary>Teleport home after falling out of the world.</summary>
    public void Recover()
    {
        Vector3 target;
        if (respawnPoint != null)
        {
            target = respawnPoint.position;
        }
        else
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            target = player != null
                ? player.transform.position + player.transform.forward * 1.5f + Vector3.up * 1.5f
                : Vector3.up * 2f;
        }

        _carryPoint = null;
        _rb.isKinematic = true;
        transform.position = target;
        _rb.isKinematic = false;
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;

        _state = State.Resting;
        RestoreCollider();
    }

    /// <summary>
    /// Physics-level exclusion between the head and whoever threw it, for a
    /// moment after release. More reliable than hoping the head clears her
    /// capsule before its collider switches back on.
    /// </summary>
    private void IgnoreThrower(Collider thrower)
    {
        if (thrower == null || _col == null) return;
        StartCoroutine(IgnoreRoutine(thrower));
    }

    private IEnumerator IgnoreRoutine(Collider thrower)
    {
        // Wait for the collider to come back before pairing them, or Unity
        // silently forgets the ignore on a disabled collider.
        yield return new WaitForSeconds(releaseColliderDelay + 0.02f);
        if (_col == null || thrower == null) yield break;

        Physics.IgnoreCollision(_col, thrower, true);
        yield return new WaitForSeconds(ignoreThrowerTime);

        if (_col != null && thrower != null)
            Physics.IgnoreCollision(_col, thrower, false);
    }

    private void RestoreCollider()
    {
        if (!disableColliderWhileHeld || _col == null) return;

        if (_reenable != null) StopCoroutine(_reenable);
        _reenable = StartCoroutine(ReenableCollider());
    }

    private IEnumerator ReenableCollider()
    {
        // Short delay so the head clears the player before physics wakes up.
        yield return new WaitForSeconds(releaseColliderDelay);
        if (_col != null) _col.enabled = true;
        _reenable = null;
    }

    private void OnCollisionEnter(Collision c)
    {
        if (_state != State.Airborne) return;
        if (bonkSounds == null || bonkSounds.Length == 0) return;
        if (c.relativeVelocity.magnitude < bonkThreshold) return;

        Play(bonkSounds[Random.Range(0, bonkSounds.Length)]);
    }

    private void Play(AudioClip clip)
    {
        if (clip != null && _audio != null) _audio.PlayOneShot(clip);
    }
}
