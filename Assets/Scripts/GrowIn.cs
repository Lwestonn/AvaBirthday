using System.Collections;
using UnityEngine;

/// <summary>
/// Scales an object up from nothing with a springy overshoot. Used by flowers
/// and by the finale tree. Add it and it plays on enable.
/// </summary>
public class GrowIn : MonoBehaviour
{
    public float duration = 0.6f;
    public float delay = 0f;

    [Tooltip("How far past the target it overshoots before settling. 0 = no bounce.")]
    public float overshoot = 0.18f;

    [Tooltip("Random spin around Y so a field of identical flowers does not look stamped.")]
    public bool randomYaw = true;

    [Tooltip("Random scale multiplier range, for variety.")]
    public Vector2 scaleJitter = new(0.85f, 1.2f);

    [Tooltip("Makes height run ahead of width, so the thing shoots up and then fills out. " +
             "0 is a plain uniform scale, which reads as an object being resized. Around 0.6 " +
             "reads as something growing. Worth turning up on the finale tree.")]
    [Range(0f, 1f)] public float verticalLead;

    private Vector3 _target;

    private void OnEnable()
    {
        _target = transform.localScale;
        if (scaleJitter.y > 0f)
            _target *= Random.Range(scaleJitter.x, scaleJitter.y);

        if (randomYaw)
            transform.Rotate(Vector3.up, Random.Range(0f, 360f), Space.Self);

        transform.localScale = Vector3.zero;
        StartCoroutine(Grow());
    }

    private IEnumerator Grow()
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);

            // Ease out with a little bounce past 1.0 then back.
            float eased = 1f - Mathf.Pow(1f - k, 3f);
            float bounce = 1f + overshoot * Mathf.Sin(k * Mathf.PI) * (1f - k);

            // Height runs ahead of width. A uniform scale reads as an object being
            // resized; something that gets tall first and then thickens reads as
            // growing. Pow with an exponent below 1 always sits above the plain
            // curve, so Y leads and both still land on 1 together.
            float tall = Mathf.Lerp(eased, Mathf.Pow(eased, 0.55f), verticalLead);

            transform.localScale = new Vector3(
                _target.x * eased * bounce,
                _target.y * tall * bounce,
                _target.z * eased * bounce);

            yield return null;
        }

        transform.localScale = _target;
    }
}
