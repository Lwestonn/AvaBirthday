using UnityEngine;

/// <summary>
/// Slowly turns an object on the spot. Used for the character on the start
/// screen. Runs on unscaled time so it keeps turning even if something has
/// frozen the game clock.
/// </summary>
public class TurntableSpin : MonoBehaviour
{
    [Tooltip("Degrees per second. Negative spins the other way. 20 is a calm showroom turn.")]
    public float speed = 20f;

    [Tooltip("Gentle bob, in world units. Set to 0 for none.")]
    public float bobHeight = 0.04f;

    [Tooltip("Bobs per second.")]
    public float bobSpeed = 0.5f;

    private Vector3 _base;

    private void Awake() => _base = transform.localPosition;

    private void OnEnable()
    {
        // Re-read in case it was repositioned while disabled.
        _base = transform.localPosition;
    }

    private void Update()
    {
        transform.Rotate(Vector3.up, speed * Time.unscaledDeltaTime, Space.Self);

        if (bobHeight > 0f)
        {
            float y = Mathf.Sin(Time.unscaledTime * bobSpeed * Mathf.PI * 2f) * bobHeight;
            transform.localPosition = _base + Vector3.up * y;
        }
    }
}
