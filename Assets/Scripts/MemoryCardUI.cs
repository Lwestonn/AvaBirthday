using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The small photo card that slides in from the side while the head talks.
/// Deliberately NOT modal: no backdrop, no input capture, she keeps playing.
///
/// Built by Tools > Birthday > Build UI.
/// </summary>
public class MemoryCardUI : MonoBehaviour
{
    [Header("Wiring")]
    public RectTransform panel;
    public CanvasGroup group;
    public Image photo;
    public TMP_Text title;

    [Header("Slide")]
    [Tooltip("Where the card sits when hidden, relative to its shown position.")]
    public Vector2 hiddenOffset = new(-520f, 0f);
    public float slideTime = 0.45f;

    private Vector2 _shownPos;
    private Coroutine _anim;
    private bool _visible;

    private void Awake()
    {
        if (panel == null) panel = transform as RectTransform;
        if (group == null) group = GetComponent<CanvasGroup>();

        _shownPos = panel.anchoredPosition;
        panel.anchoredPosition = _shownPos + hiddenOffset;

        if (group != null)
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;   // never steal clicks from the game
            group.interactable = false;
        }
    }

    public void Show(MemoryData memory)
    {
        if (memory == null) return;

        if (title != null) title.text = memory.title;

        if (photo != null)
        {
            photo.sprite = memory.photo;
            photo.preserveAspect = true;
            photo.gameObject.SetActive(memory.photo != null);
        }

        Animate(true);
    }

    public void Hide() => Animate(false);

    private void Animate(bool show)
    {
        if (_visible == show) return;
        _visible = show;

        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(Slide(show));
    }

    private IEnumerator Slide(bool show)
    {
        Vector2 from = panel.anchoredPosition;
        Vector2 to = show ? _shownPos : _shownPos + hiddenOffset;

        float fromA = group != null ? group.alpha : 1f;
        float toA = show ? 1f : 0f;

        float t = 0f;
        while (t < slideTime)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / slideTime);

            panel.anchoredPosition = Vector2.Lerp(from, to, k);
            if (group != null) group.alpha = Mathf.Lerp(fromA, toA, k);

            yield return null;
        }

        panel.anchoredPosition = to;
        if (group != null) group.alpha = toA;
        _anim = null;
    }
}
