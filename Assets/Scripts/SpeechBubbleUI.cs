using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Animal Crossing style speech bubble, pinned to the right side of the screen.
///
/// Screen-space rather than floating in the world, so it can never be blocked by
/// the memory photo card, never gets lost behind terrain, and stays readable
/// however far the head has been thrown.
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

    private Coroutine _routine;
    private Vector3 _baseScale;
    private bool _visible;

    public bool IsTyping { get; private set; }

    private void Awake()
    {
        if (panel == null) panel = transform as RectTransform;
        if (group == null) group = GetComponent<CanvasGroup>();

        _baseScale = panel.localScale;

        if (group != null)
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;   // never steal input from the game
            group.interactable = false;
        }

        if (label != null) label.text = "";
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
        IsTyping = false;
        _routine = null;
    }
}
