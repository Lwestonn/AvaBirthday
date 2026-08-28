using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Throw power meter. Hidden until she starts winding up, then fills left to
/// right and flashes when it tops out.
///
/// Built and wired automatically by Tools > Birthday > Build UI.
/// Finds the HeadCarrier on its own at runtime, so no manual wiring needed.
/// </summary>
public class ChargeBarUI : MonoBehaviour
{
    [Header("Wiring")]
    [Tooltip("Leave empty and it finds the HeadCarrier in the scene automatically.")]
    public HeadCarrier carrier;

    public CanvasGroup group;

    [Tooltip("The coloured bar that grows. Anchored left, pivot left.")]
    public RectTransform fillRect;

    public Image fillImage;

    [Tooltip("Small tick showing where max power is. Purely cosmetic.")]
    public RectTransform maxMarker;

    [Header("Layout")]
    [Tooltip("Width of the bar at full charge, in UI units.")]
    public float maxWidth = 340f;

    [Header("Feel")]
    public float fadeSpeed = 10f;

    [Tooltip("Colour ramp from empty to full.")]
    public Gradient fillGradient = new();

    [Tooltip("Pulse the bar once it hits max so she knows to let go.")]
    public bool pulseAtMax = true;
    public float pulseSpeed = 9f;
    public float pulseAmount = 0.08f;

    private Vector3 _baseScale;

    private void Awake()
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        if (group != null) group.alpha = 0f;

        _baseScale = transform.localScale;

        // Sensible default ramp if none was authored in the Inspector.
        if (fillGradient == null || fillGradient.colorKeys.Length == 0)
        {
            fillGradient = new Gradient();
            fillGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.55f, 0.80f, 1.00f), 0f),
                    new GradientColorKey(new Color(1.00f, 0.72f, 0.82f), 0.6f),
                    new GradientColorKey(new Color(1.00f, 0.45f, 0.45f), 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        }
    }

    private void Start()
    {
        if (carrier == null) carrier = FindFirstObjectByType<HeadCarrier>();
    }

    private void Update()
    {
        if (carrier == null)
        {
            carrier = FindFirstObjectByType<HeadCarrier>();
            if (carrier == null) return;
        }

        float charge = carrier.ChargeNormalized;
        bool showing = charge >= 0f;

        if (group != null)
            group.alpha = Mathf.MoveTowards(group.alpha, showing ? 1f : 0f, fadeSpeed * Time.deltaTime);

        if (!showing)
        {
            transform.localScale = _baseScale;
            return;
        }

        if (fillRect != null)
            fillRect.sizeDelta = new Vector2(maxWidth * charge, fillRect.sizeDelta.y);

        if (fillImage != null)
            fillImage.color = fillGradient.Evaluate(charge);

        if (maxMarker != null)
            maxMarker.anchoredPosition = new Vector2(maxWidth, maxMarker.anchoredPosition.y);

        // At full power, breathe slightly so it reads as "release now".
        if (pulseAtMax && charge >= 0.999f)
        {
            float p = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            transform.localScale = _baseScale * p;
        }
        else
        {
            transform.localScale = _baseScale;
        }
    }
}
