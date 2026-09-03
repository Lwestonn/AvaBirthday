using System.Collections;
using UnityEngine;

/// <summary>
/// One AudioSource, three tracks, and crossfades between them.
///
/// Start screen music plays while the menu is up, the island track takes over
/// when she presses Play, and the finale track comes in when his head goes back
/// on. Nothing ever cuts, it always fades, because a hard cut between two pieces
/// of music is one of those things that reads as cheap without anyone being able
/// to say why.
///
/// Built and wired by Tools > Birthday > Build Audio.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class MusicDirector : MonoBehaviour
{
    [Header("Tracks")]
    public AudioClip startScreenTrack;
    public AudioClip islandTrack;
    public AudioClip finaleTrack;

    [Header("Levels")]
    [Range(0f, 1f)] public float musicVolume = 0.42f;

    [Tooltip("Seconds to fade from one track to the next.")]
    public float crossfadeTime = 2.0f;

    [Tooltip("Silence before the first track starts, so it does not slam in on load.")]
    public float startDelay = 0.3f;

    [Header("Behaviour")]
    [Tooltip("Play the start screen track on load. Turn off if you are testing with the menu skipped.")]
    public bool playOnAwake = true;

    private AudioSource _source;
    private Coroutine _fade;
    private AudioClip _current;

    private void Awake()
    {
        _source = GetComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.loop = true;
        _source.spatialBlend = 0f;      // music is not a thing in the world
        _source.volume = 0f;
    }

    private void Start()
    {
        if (!playOnAwake) return;

        // If the start screen was skipped, go straight to the island track rather
        // than sitting on menu music over gameplay.
        var menus = FindFirstObjectByType<GameMenus>();
        bool menuShowing = menus == null || menus.showStartScreenOnLoad;

        StartCoroutine(FirstTrack(menuShowing ? startScreenTrack : islandTrack));
    }

    private IEnumerator FirstTrack(AudioClip clip)
    {
        if (startDelay > 0f) yield return new WaitForSecondsRealtime(startDelay);
        Play(clip);
    }

    // ---- public hooks, safe to wire from the Inspector ------------------

    public void PlayStartScreen() => Play(startScreenTrack);
    public void PlayIsland() => Play(islandTrack);
    public void PlayFinale() => Play(finaleTrack);

    public void Play(AudioClip clip)
    {
        if (clip == null || clip == _current) return;

        _current = clip;

        if (_fade != null) StopCoroutine(_fade);
        _fade = StartCoroutine(Crossfade(clip));
    }

    public void SetVolume(float v)
    {
        musicVolume = Mathf.Clamp01(v);
        if (_source != null && _fade == null) _source.volume = musicVolume;
    }

    // ---------------------------------------------------------------------

    private IEnumerator Crossfade(AudioClip next)
    {
        float from = _source.volume;

        // Fade out whatever is playing. Unscaled time, or pausing the game mid
        // fade would freeze the music halfway down.
        if (_source.isPlaying && from > 0.001f)
        {
            float t = 0f;
            while (t < crossfadeTime * 0.5f)
            {
                t += Time.unscaledDeltaTime;
                _source.volume = Mathf.Lerp(from, 0f, t / (crossfadeTime * 0.5f));
                yield return null;
            }
        }

        _source.clip = next;
        _source.loop = true;
        _source.volume = 0f;
        _source.Play();

        float t2 = 0f;
        while (t2 < crossfadeTime * 0.5f)
        {
            t2 += Time.unscaledDeltaTime;
            _source.volume = Mathf.Lerp(0f, musicVolume, t2 / (crossfadeTime * 0.5f));
            yield return null;
        }

        _source.volume = musicVolume;
        _fade = null;
    }
}
