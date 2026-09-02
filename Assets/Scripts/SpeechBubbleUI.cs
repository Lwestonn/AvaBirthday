using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Animal Crossing style speech bubble that floats above the head.
///
/// It is still a screen-space UI element, it is just repositioned every frame to
/// sit over the head's position in the world. That gets the best of both: the
/// text stays crisp and perfectly readable at any distance, the 9-slice art never
/// distorts, and the bubble can never be swallowed by terrain, but it is anchored
/// to the character so it reads as him talking rather than as a subtitle.
///
/// If the head goes near a screen edge the bubble is clamped so it stays fully
/// visible, and the tail slides sideways to keep pointing at him.
///
/// Leave Follow Target empty to go back to a bubble parked in one screen corner.
///
/// Built and wired by Tools > Birthday > Build Speech Bubble.
/// </summary>
public class SpeechBubbleUI : MonoBehaviour
{
    [Header("Wiring")]
    public RectTransform panel;
    public CanvasGroup group;
    public TMP_Text label;
    [Tooltip("The little pointer under the bubble. Purely decorative.")]
    public RectTransform tail;

    [Header("Layout")]
    [Tooltip("Bubble width. Height is computed from the text.")]
    public float width = 560f;
    [Tooltip("Space between the text and the bubble edge.")]
    public float paddingX = 46f;
    public float paddingY = 40f;
    public float minHeight = 130f;

    [Header("Typing")]
    [Tooltip("Characters revealed per second.")]
    public float charsPerSecond = 42f;

    [Tooltip("Extra pause on . ! ? so sentences breathe.")]
    public float punctuationPause = 0.16f;

    [Tooltip("Seconds the finished line stays up before fading, unless told otherwise.")]
    public float defaultHold = 2.2f;

    public float fadeTime = 0.18f;

    [Header("Voice blips")]
    public AudioSource blipSource;
    public AudioClip[] blips;

    [Tooltip("Play a blip every N revealed characters. 2 is close to Animal Crossing.")]
    public int blipEveryNChars = 2;

    [Range(0f, 0.5f)]
    public float pitchJitter = 0.14f;
    [Range(0f, 1f)]
    public float blipVolume = 0.5f;

    [Header("Motion")]
    [Tooltip("Small pop as the bubble appears.")]
    public float popScale = 1.06f;
    public float popTime = 0.14f;

    [Header("Follow the head")]
    [Tooltip("The head. The bubble floats above this every frame. Leave empty to park the bubble in a fixed screen position instead.")]
    public Transform followTarget;

    [Tooltip("How far above the head the bubble sits, in world units.")]
    public float worldHeight = 1.1f;

    [Tooltip("Keeps the bubble this many pixels away from the screen edges.")]
    public float screenMargin = 28f;

    [Tooltip("Softens the follow so a hard throw does not make the bubble jitter. 0 = rigid.")]
    public float followSmoothing = 18f;

    [Tooltip("Slide the tail sideways so it keeps pointing at the head when the bubble is clamped to a screen edge.")]
    public bool tailTracksTarget = true;

    private Coroutine _routine;
    private Vector3 _baseScale;
    private bool _visible;
    private bool _placed;
    private Camera _cam;
    private Canvas _canvas;
    private RectTransform _canvasRect;

    public bool IsTyping { get; private set; }

    private void Awake()
    {
        if (panel == null) panel = transform as RectTransform;
        if (group == null) group = GetComponent<CanvasGroup>();

        _canvas = GetComponentInParent<Canvas>();
        if (_canvas != null) _canvasRect = _canvas.transform as RectTransform;

        _baseScale = panel.localScale;

        if (group != null)
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;   // never steal input from the game
            group.interactable = false;
        }

        if (label != null) label.text = "";
    }

    private void LateUpdate()
    {
        if (followTarget == null) return;
        if (_canvasRect == null) return;

        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        Vector3 world = followTarget.position + Vector3.up * worldHeight;
        Vector3 sp = _cam.WorldToScreenPoint(world);

        // Behind the camera, WorldToScreenPoint returns a mirrored point with a
        // negative z. Flipping it pushes the bubble to the correct screen edge
        // instead of having it appear on the wrong side of the screen.
        bool behind = sp.z < 0f;
        if (behind)
        {
            sp.x = Screen.width - sp.x;
            sp.y = Screen.height - sp.y;
        }

        Camera uiCam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : _canvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect, sp, uiCam, out Vector2 local))
            return;

        // Pivot is bottom-centre, so the bubble grows upward from the point just
        // above his head and never covers him.
        Vector2 half = _canvasRect.rect.size * 0.5f;
        Vector2 size = panel.sizeDelta;

        float minX = -half.x + screenMargin + size.x * 0.5f;
        float maxX = half.x - screenMargin - size.x * 0.5f;
        float minY = -half.y + screenMargin;
        float maxY = half.y - screenMargin - size.y;

        Vector2 clamped = new(
            maxX > minX ? Mathf.Clamp(local.x, minX, maxX) : 0f,
            maxY > minY ? Mathf.Clamp(local.y, minY, maxY) : minY);

        // Snap the first frame of a new line, smooth after that. Without the snap
        // the bubble visibly flies across the screen every time he speaks.
        bool smooth = _placed && followSmoothing > 0f && !behind;
        panel.anchoredPosition = smooth
            ? Vector2.Lerp(panel.anchoredPosition, clamped, followSmoothing * Time.deltaTime)
            : clamped;

        _placed = true;

        if (tailTracksTarget && tail != null)
        {
            float limit = Mathf.Max(0f, size.x * 0.5f - 44f);
            float dx = Mathf.Clamp(local.x - panel.anchoredPosition.x, -limit, limit);
            tail.anchoredPosition = new Vector2(dx, tail.anchoredPosition.y);
        }
    }

    /// <summary>Say a line. Pass a hold time, or leave it negative to use the default.</summary>
    public void Show(string text, float holdAfter = -1f)
    {
        if (label == null || string.IsNullOrWhiteSpace(text)) return;

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(Run(text, holdAfter < 0f ? defaultHold : holdAfter));
    }

    public void HideNow()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(FadeOut());
    }

    /// <summary>Reveal the rest of the current line immediately.</summary>
    public void Skip()
    {
        if (!IsTyping || label == null) return;
        label.maxVisibleCharacters = int.MaxValue;
        IsTyping = false;
    }

    private IEnumerator Run(string text, float hold)
    {
        // --- size the bubble to the text before it appears
        label.text = text;
        label.maxVisibleCharacters = 0;

        float textWidth = width - paddingX * 2f;
        Vector2 pref = label.GetPreferredValues(text, textWidth, 0f);
        float h = Mathf.Max(minHeight, pref.y + paddingY * 2f);

        panel.sizeDelta = new Vector2(width, h);
        if (label.rectTransform != null)
            label.rectTransform.sizeDelta = new Vector2(textWidth, h - paddingY * 2f);

        label.ForceMeshUpdate();
        int total = label.textInfo.characterCount;

        // --- appear
        if (!_visible) yield return Pop();
        _visible = true;

        // --- type
        IsTyping = true;
        float delay = 1f / Mathf.Max(1f, charsPerSecond);
        int sinceBlip = 0;

        for (int i = 0; i <= total; i++)
        {
            if (!IsTyping) break;          // Skip() was called

            label.maxVisibleCharacters = i;

            if (i > 0 && i <= total)
            {
                char c = CharAt(label, i - 1);

                // Blip on real characters only. Blipping on spaces sounds like a stutter.
                if (!char.IsWhiteSpace(c))
                {
                    sinceBlip++;
                    if (sinceBlip >= Mathf.Max(1, blipEveryNChars))
                    {
                        PlayBlip();
                        sinceBlip = 0;
                    }
                }

                yield return new WaitForSeconds(delay);

                if (c == '.' || c == '!' || c == '?' || c == ',')
                    yield return new WaitForSeconds(punctuationPause);
            }
        }

        label.maxVisibleCharacters = int.MaxValue;
        IsTyping = false;

        yield return new WaitForSeconds(hold);
        yield return FadeOut();
    }

    private static char CharAt(TMP_Text t, int index)
    {
        var info = t.textInfo;
        if (info == null || index < 0 || index >= info.characterCount) return ' ';
        return info.characterInfo[index].character;
    }

    private void PlayBlip()
    {
        if (blipSource == null || blips == null || blips.Length == 0) return;

        var clip = blips[Random.Range(0, blips.Length)];
        if (clip == null) return;

        // Random pitch per blip is what turns a repeating beep into speech.
        blipSource.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        blipSource.PlayOneShot(clip, blipVolume);
    }

    private IEnumerator Pop()
    {
        float t = 0f;
        while (t < popTime)
        {
            t += Time.deltaTime;
            float k = t / popTime;
            if (group != null) group.alpha = k;
            panel.localScale = _baseScale * Mathf.Lerp(0.9f, popScale, k);
            yield return null;
        }

        // settle back from the overshoot
        t = 0f;
        while (t < popTime * 0.6f)
        {
            t += Time.deltaTime;
            panel.localScale = _baseScale * Mathf.Lerp(popScale, 1f, t / (popTime * 0.6f));
            yield return null;
        }

        if (group != null) group.alpha = 1f;
        panel.localScale = _baseScale;
    }

    private IEnumerator FadeOut()
    {
        float from = group != null ? group.alpha : 1f;
        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            if (group != null) group.alpha = Mathf.Lerp(from, 0f, t / fadeTime);
            yield return null;
        }

        if (group != null) group.alpha = 0f;
        if (label != null) label.text = "";
        _visible = false;
        _placed = false;      // next line snaps into place instead of sliding across
        IsTyping = false;
        _routine = null;
    }
}
