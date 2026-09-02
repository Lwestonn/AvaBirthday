using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Floating one-liners above the head. This is where most of the charm lives,
/// and it costs you nothing but writing.
///
/// Setup: put this on the head. Make a child GameObject with a
/// TextMeshPro (3D, not UI) component, position it about 0.5 above the head,
/// and drag it into the Label slot.
///
/// Wire HeadPickup's events to the Say* methods in the Inspector, or leave
/// autoWireEvents on and it hooks itself up.
/// </summary>
public class HeadBarks : MonoBehaviour
{
    [System.Serializable]
    public class BarkSet
    {
        public string label;
        [TextArea(1, 3)] public string[] lines;
    }

    [Header("Wiring")]
    [Tooltip("Screen-space speech bubble. When set, ALL dialogue goes here and the world label below is ignored.")]
    public SpeechBubbleUI bubble;

    [Tooltip("Legacy floating world label. Only used when no bubble is assigned.")]
    public TMP_Text label;

    [Header("Label placement")]
    [Tooltip("How far above the head the text floats, in world units.")]
    public float labelHeight = 0.75f;

    [Tooltip("Smooths the label's follow so it drifts after a hard throw instead of snapping. 0 = rigid.")]
    public float labelFollowSmoothing = 12f;

    [Tooltip("Hook HeadPickup's events automatically. Turn off if you wire them by hand.")]
    public bool autoWireEvents = true;

    [Header("Timing")]
    public float displayTime = 2.6f;
    public float fadeTime = 0.4f;
    [Tooltip("Seconds of being ignored before the head starts complaining.")]
    public float idleChatterDelay = 18f;
    [Tooltip("Set to 0 to turn idle chatter off.")]
    public float idleChatterInterval = 22f;

    [Header("Lines")]
    public BarkSet onPickedUp = new() { label = "Picked up", lines = new[] {
        "There she is.",
        "Careful, that's my good side.",
        "I've been down here for hours.",
        "Finally. My arms are useless." } };

    public BarkSet onThrown = new() { label = "Thrown", lines = new[] {
        "WHEEEE",
        "This is fine!",
        "I regret everything!",
        "Tell my body I love it!" } };

    public BarkSet onLanded = new() { label = "Landed", lines = new[] {
        "Ow.",
        "Ten out of ten. Do it again.",
        "I saw the whole sky.",
        "That one's going to bruise." } };

    public BarkSet onDropped = new() { label = "Dropped", lines = new[] {
        "Rude.",
        "Just gonna leave me here?",
        "Cool. Cool cool cool." } };

    public BarkSet onMemoryFound = new() { label = "Memory found", lines = new[] {
        "Oh, I remember that one.",
        "That's a good one.",
        "I was so nervous that day.",
        "One step closer to having a neck." } };

    public BarkSet onIdle = new() { label = "Idle / ignored", lines = new[] {
        "Still here.",
        "Just a head. In some grass. Living the dream.",
        "No rush.",
        "Do you think my body misses me?" } };

    public BarkSet onNearBody = new() { label = "Near the body", lines = new[] {
        "Hey. That's mine.",
        "So close.",
        "Line me up, you've got this." } };

    private Coroutine _routine;
    private float _lastInteraction;
    private float _nextIdle;
    private bool _ended;
    private Transform _cam;
    private string _lastLine;
    private Color _baseColor;

    private void Awake()
    {
        // With a bubble in play the old world label is dead weight, so switch it off
        // rather than leaving an invisible object following the head around.
        if (bubble != null && label != null)
        {
            label.gameObject.SetActive(false);
            label = null;
        }

        if (label != null)
        {
            _baseColor = label.color;
            label.text = "";

            // Inherit the height the label was authored at, if it was set as a child.
            if (label.transform.parent == transform && label.transform.localPosition.y > 0.01f)
                labelHeight = label.transform.localPosition.y;

            // THE FIX for the spinning text: detach the label from the head entirely.
            // As a child it inherits the head's tumble, so the text both rotates AND
            // swings in an arc around the head as it spins. Detached, it just floats.
            label.transform.SetParent(null, true);
            label.transform.position = transform.position + Vector3.up * labelHeight;
        }

        if (Camera.main != null) _cam = Camera.main.transform;

        _lastInteraction = Time.time;
        _nextIdle = Time.time + idleChatterDelay;
    }

    private void OnDestroy()
    {
        // The label is no longer our child, so it will not be cleaned up with us.
        if (label != null) Destroy(label.gameObject);
    }

    private void Start()
    {
        if (!autoWireEvents) return;

        var head = GetComponent<HeadPickup>();
        if (head == null) return;

        head.onPickedUp.AddListener(SayPickedUp);
        head.onThrown.AddListener(SayThrown);
        head.onLanded.AddListener(SayLanded);
        head.onDropped.AddListener(SayDropped);
    }

    private void LateUpdate()
    {
        if (bubble == null && label != null)
        {
            // Follow the head's POSITION only. No rotation is inherited, so the text
            // stays level and upright no matter how hard the head is tumbling.
            Vector3 target = transform.position + Vector3.up * labelHeight;
            label.transform.position = labelFollowSmoothing > 0f
                ? Vector3.Lerp(label.transform.position, target, labelFollowSmoothing * Time.deltaTime)
                : target;

            if (_cam == null && Camera.main != null) _cam = Camera.main.transform;

            if (_cam != null)
            {
                // Billboard, but keep it upright: zero out the vertical component so
                // the text never tilts when the camera looks up or down.
                Vector3 look = label.transform.position - _cam.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.0001f)
                    label.transform.rotation = Quaternion.LookRotation(look);
            }
        }

        if (_ended) return;
        if (idleChatterInterval <= 0f) return;
        if (Time.time < _nextIdle) return;

        var head = GetComponent<HeadPickup>();
        if (head != null && head.IsHeld) { _nextIdle = Time.time + idleChatterInterval; return; }

        SayIdle();
        _nextIdle = Time.time + idleChatterInterval;
    }

    // ---- public hooks, safe to wire from the Inspector -------------------

    public void SayPickedUp()    => Say(onPickedUp);
    public void SayThrown()      => Say(onThrown);
    public void SayLanded()      => Say(onLanded);
    public void SayDropped()     => Say(onDropped);
    public void SayMemoryFound() => Say(onMemoryFound);
    public void SayIdle()        => Say(onIdle);
    public void SayNearBody()    => Say(onNearBody);

    /// <summary>
    /// Called once the head is back on the body. Stops idle chatter and every
    /// reaction line, because after that moment he is a person again rather than
    /// an object being carried around. Direct Say(string) calls still work, so
    /// the closing lines can still be spoken.
    /// </summary>
    public void EndGameMode()
    {
        _ended = true;
    }

    public void Say(BarkSet set)
    {
        if (_ended) return;
        if (set == null || set.lines == null || set.lines.Length == 0) return;
        Say(PickLine(set.lines));
    }

    public void Say(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;

        _lastInteraction = Time.time;
        _nextIdle = Time.time + idleChatterDelay;

        // The screen-space bubble wins whenever it exists. It cannot be blocked by
        // the photo card or lost behind terrain the way the world label could.
        if (bubble != null)
        {
            bubble.Show(line, displayTime);
            return;
        }

        if (label == null) return;
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(ShowLine(line));
    }

    // ---------------------------------------------------------------------

    private string PickLine(string[] lines)
    {
        if (lines.Length == 1) return lines[0];

        // Avoid repeating the same line twice in a row. Small touch, big
        // difference in how scripted it feels.
        string pick = _lastLine;
        int guard = 0;
        while (pick == _lastLine && guard++ < 12)
            pick = lines[Random.Range(0, lines.Length)];

        _lastLine = pick;
        return pick;
    }

    private IEnumerator ShowLine(string line)
    {
        label.text = line;
        label.color = _baseColor;

        yield return new WaitForSeconds(displayTime);

        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            var c = _baseColor;
            c.a = Mathf.Lerp(_baseColor.a, 0f, t / fadeTime);
            label.color = c;
            yield return null;
        }

        label.text = "";
        _routine = null;
    }
}
