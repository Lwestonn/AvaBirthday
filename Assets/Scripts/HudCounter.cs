using UnityEngine;
using TMPro;

/// <summary>
/// Tiny "3 / 10 memories found" readout.
/// Put on a TextMeshPro text in the corner of your Canvas, then drag this
/// object into MemoryManager's onProgressChanged event and pick
/// HudCounter.SetProgress (the dynamic int,int version).
/// </summary>
public class HudCounter : MonoBehaviour
{
    public TMP_Text label;

    [Tooltip("{0} is collected, {1} is total.")]
    public string format = "{0} / {1} memories found";

    [Tooltip("Little pop when the number changes.")]
    public float punchScale = 1.25f;
    public float punchTime = 0.25f;

    private Vector3 _baseScale;
    private float _punchTimer;

    private void Awake()
    {
        if (label == null) label = GetComponent<TMP_Text>();
        _baseScale = transform.localScale;
    }

    public void SetProgress(int collected, int total)
    {
        if (label != null)
            label.text = string.Format(format, collected, total);

        if (collected > 0) _punchTimer = punchTime;
    }

    private void Update()
    {
        if (_punchTimer <= 0f) return;

        _punchTimer -= Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(_punchTimer / punchTime);
        float s = Mathf.Lerp(1f, punchScale, t);
        transform.localScale = _baseScale * s;

        if (_punchTimer <= 0f) transform.localScale = _baseScale;
    }
}
