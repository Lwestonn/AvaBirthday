using UnityEngine;

/// <summary>
/// Brings her hands together to cradle the head while she is carrying it, then
/// hands them back to the animation when she lets go.
///
/// Runs after the Animator (LateUpdate, late execution order) because the rig
/// writes the hand bones every frame and anything set earlier would be
/// overwritten before it was drawn.
///
/// Added and wired by Tools > Birthday > Characters.
/// </summary>
[DefaultExecutionOrder(200)]
public class BlobHands : MonoBehaviour
{
    [Header("Wiring")]
    [Tooltip("Leave empty to find the HeadCarrier on this object.")]
    public HeadCarrier carrier;

    public Transform handLeft;
    public Transform handRight;

    [Header("Grip")]
    [Tooltip("How far apart the hands sit while holding, measured from the centre of the head.")]
    public float gripSpread = 0.27f;

    [Tooltip("How far below the middle of the head the hands cup it.")]
    public float gripDrop = 0.06f;

    [Tooltip("Push the hands forward or back relative to the head.")]
    public float gripForward = 0f;

    [Header("Blending")]
    [Tooltip("How quickly the hands move into and out of the grip. Higher is snappier.")]
    public float blendSpeed = 7f;

    [Tooltip("How tightly they track the head once gripping.")]
    public float followSpeed = 26f;

    private Vector3 _restL, _restR;
    private bool _haveRest;
    private float _blend;

    // Where the hands actually are, kept between frames. This has to be its own
    // state: reading it back off the transform does not work, because the
    // transform is reset to the animated pose at the top of every frame.
    private Vector3 _smoothL, _smoothR;
    private bool _smoothReady;

    private void Awake()
    {
        if (carrier == null) carrier = GetComponent<HeadCarrier>();
        CaptureRest();
    }

    /// <summary>
    /// The hands live as children of the hand bones, so their local offset is what
    /// the animation drives them through. Remember it, because overriding a world
    /// position permanently rewrites the local one and the offset would drift away
    /// a little more every frame.
    /// </summary>
    public void CaptureRest()
    {
        if (handLeft != null) _restL = handLeft.localPosition;
        if (handRight != null) _restR = handRight.localPosition;
        _haveRest = handLeft != null && handRight != null;
    }

    private void LateUpdate()
    {
        if (!_haveRest || carrier == null) return;

        bool holding = carrier.IsHolding && carrier.Held != null;

        _blend = Mathf.MoveTowards(_blend, holding ? 1f : 0f, blendSpeed * Time.deltaTime);

        // Always restore the animated offset first, so the natural position is a
        // clean read rather than yesterday's override.
        handLeft.localPosition = _restL;
        handRight.localPosition = _restR;

        Vector3 restWorldL = handLeft.position;
        Vector3 restWorldR = handRight.position;

        // Where they are trying to be: cupping the head, or back on the animation.
        Vector3 targetL = restWorldL;
        Vector3 targetR = restWorldR;

        if (holding)
        {
            Transform head = carrier.Held.transform;
            Vector3 right = transform.right;
            Vector3 fwd = transform.forward;

            Vector3 centre = head.position - Vector3.up * gripDrop + fwd * gripForward;

            targetL = centre - right * gripSpread;
            targetR = centre + right * gripSpread;
        }

        if (!_smoothReady)
        {
            _smoothL = restWorldL;
            _smoothR = restWorldR;
            _smoothReady = true;
        }

        // Chase the target from the remembered position, not from the reset one.
        // Lerping from the animated pose every frame is what made them shudder:
        // they only ever travelled a fraction of the way and then got yanked back
        // to the rig on the next frame, so they vibrated between the two.
        float snap = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        _smoothL = Vector3.Lerp(_smoothL, targetL, snap);
        _smoothR = Vector3.Lerp(_smoothR, targetR, snap);

        if (_blend <= 0.001f) return;

        float k = Mathf.SmoothStep(0f, 1f, _blend);
        handLeft.position = Vector3.Lerp(restWorldL, _smoothL, k);
        handRight.position = Vector3.Lerp(restWorldR, _smoothR, k);
    }
}
