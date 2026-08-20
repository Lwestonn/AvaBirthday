using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The payoff. Hook this up to MemoryManager.onAllCollected in the Inspector
/// (drag the GameManager object into the event slot, pick FinaleSequence.Play).
///
/// Does three things: fades to a soft color, shows your birthday message,
/// and optionally swaps the music and fires confetti.
/// </summary>
public class FinaleSequence : MonoBehaviour
{
    [Header("Wiring")]
    [Tooltip("Full-screen Image used to fade out. Put it on your Canvas, above everything, starting transparent.")]
    public Image fadeImage;

    [Tooltip("A panel with your final message. Starts inactive.")]
    public GameObject finalePanel;

    public TMP_Text finaleText;
    public CanvasGroup finaleGroup;

    [Header("Content")]
    [TextArea(4, 12)]
    public string message = "Happy birthday, Ava.\n\nI made you a whole world just to say it.";

    [Header("Extras")]
    public ParticleSystem confetti;
    public AudioSource music;
    public AudioClip finaleMusic;
    public float musicFadeTime = 2f;

    [Header("Timing")]
    public Color fadeColor = new Color(1f, 0.92f, 0.95f, 1f);
    public float fadeOutTime = 2f;
    public float holdBeforeText = 0.75f;
    public float textFadeTime = 2.5f;

    private bool _played;

    public void Play()
    {
        if (_played) return;
        _played = true;

        // Common wiring mistake: a CanvasGroup sits on finalePanel at alpha 0 but was
        // never dragged into the finaleGroup slot, so the panel activates invisible.
        if (finaleGroup == null && finalePanel != null)
            finaleGroup = finalePanel.GetComponent<CanvasGroup>();

        // The fade image covers the screen, so it must not eat clicks.
        if (fadeImage != null) fadeImage.raycastTarget = false;

        // The message has to render ON TOP of the fade, not behind it.
        if (finalePanel != null) finalePanel.transform.SetAsLastSibling();

        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        if (confetti != null) confetti.Play();

        if (music != null && finaleMusic != null)
            StartCoroutine(CrossfadeMusic());

        // Fade the world out to a soft warm color.
        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            Color from = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
            float t = 0f;
            while (t < fadeOutTime)
            {
                t += Time.unscaledDeltaTime;
                fadeImage.color = Color.Lerp(from, fadeColor, t / fadeOutTime);
                yield return null;
            }
            fadeImage.color = fadeColor;
        }

        yield return new WaitForSecondsRealtime(holdBeforeText);

        // Fade the message in.
        if (finalePanel != null) finalePanel.SetActive(true);
        if (finaleText != null) finaleText.text = message;

        if (finaleGroup != null)
        {
            finaleGroup.alpha = 0f;
            float t = 0f;
            while (t < textFadeTime)
            {
                t += Time.unscaledDeltaTime;
                finaleGroup.alpha = t / textFadeTime;
                yield return null;
            }
            finaleGroup.alpha = 1f;
        }
    }

    private IEnumerator CrossfadeMusic()
    {
        // If music was already silent, capturing 0 would leave the finale track muted.
        float startVol = music.volume > 0.01f ? music.volume : 1f;
        float t = 0f;
        while (t < musicFadeTime)
        {
            t += Time.unscaledDeltaTime;
            music.volume = Mathf.Lerp(startVol, 0f, t / musicFadeTime);
            yield return null;
        }

        music.clip = finaleMusic;
        music.loop = true;
        music.Play();

        t = 0f;
        while (t < musicFadeTime)
        {
            t += Time.unscaledDeltaTime;
            music.volume = Mathf.Lerp(0f, startVol, t / musicFadeTime);
            yield return null;
        }
        music.volume = startVol;
    }
}
