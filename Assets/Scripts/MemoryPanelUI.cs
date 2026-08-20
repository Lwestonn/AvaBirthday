using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// The popup that shows a photo and a note.
///
/// Hierarchy to build in the scene:
///   Canvas (Screen Space - Overlay)
///     └── MemoryPanel            <- this script + CanvasGroup. Leave it INACTIVE in the scene.
///           ├── Backdrop         (Image, black, alpha ~0.75, stretched full screen)
///           ├── PhotoImage       (Image)
///           ├── TitleText        (TextMeshPro - Text (UI))
///           ├── NoteText         (TextMeshPro - Text (UI))
///           └── ContinueButton   (Button)
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class MemoryPanelUI : MonoBehaviour
{
    [Header("Wiring")]
    public Image photoImage;
    public TMP_Text titleText;
    public TMP_Text noteText;
    public Button continueButton;
    public AudioSource voiceSource;

    [Header("Feel")]
    public float fadeDuration = 0.35f;
    [Tooltip("Reveals the note one character at a time. Set to 0 to show it instantly.")]
    public float typewriterCharsPerSecond = 40f;

    private CanvasGroup _cg;
    private Action _onClosed;
    private Coroutine _typing;
    private Coroutine _fade;
    private bool _isOpen;
    private bool _canClose;
    private bool _init;

    /// <summary>True while a memory is on screen. MemoryManager checks this before showing another.</summary>
    public bool IsOpen => _isOpen;

    /// <summary>
    /// Idempotent setup. Must be safe to call BEFORE the GameObject is ever activated,
    /// because the panel starts inactive and Awake would otherwise not have run yet.
    /// </summary>
    private void Init()
    {
        if (_init) return;
        _init = true;

        _cg = GetComponent<CanvasGroup>();
        _cg.alpha = 0f;

        if (continueButton != null)
            continueButton.onClick.AddListener(Close);
    }

    private void Awake() => Init();

    public void Show(MemoryData memory, Action onClosed)
    {
        Init();

        _onClosed = onClosed;
        _isOpen = true;
        _canClose = false;

        gameObject.SetActive(true);

        if (titleText != null) titleText.text = memory.title;

        if (photoImage != null)
        {
            photoImage.sprite = memory.photo;
            photoImage.gameObject.SetActive(memory.photo != null);
            photoImage.preserveAspect = true;
        }

        if (voiceSource != null && memory.voiceClip != null)
        {
            voiceSource.clip = memory.voiceClip;
            voiceSource.Play();
        }

        if (noteText != null)
        {
            if (_typing != null) { StopCoroutine(_typing); _typing = null; }

            if (typewriterCharsPerSecond > 0f)
                _typing = StartCoroutine(Typewriter(memory.note));
            else
                noteText.text = memory.note;
        }

        if (_fade != null) StopCoroutine(_fade);
        _fade = StartCoroutine(Fade(0f, 1f, markClosable: true));
    }

    private void Update()
    {
        if (!_isOpen || !_canClose) return;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.spaceKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame ||
            kb.enterKey.wasPressedThisFrame)
            HandleAdvance();
#else
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape) ||
            Input.GetKeyDown(KeyCode.Return))
            HandleAdvance();
#endif
    }

    /// <summary>
    /// First press finishes the typewriter, second press closes. Stops her from
    /// blowing past a note she has not read yet.
    /// </summary>
    private void HandleAdvance()
    {
        if (_typing != null)
        {
            StopCoroutine(_typing);
            _typing = null;
            if (noteText != null) noteText.maxVisibleCharacters = int.MaxValue;
            return;
        }
        Close();
    }

    public void Close()
    {
        if (!_isOpen || !_canClose) return;
        _isOpen = false;
        _canClose = false;

        if (_fade != null) StopCoroutine(_fade);
        _fade = StartCoroutine(CloseRoutine());
    }

    private IEnumerator CloseRoutine()
    {
        yield return Fade(1f, 0f, markClosable: false);

        var cb = _onClosed;
        _onClosed = null;

        // Invoke BEFORE deactivating. Deactivating kills this coroutine, so anything
        // after the SetActive is not guaranteed to run.
        cb?.Invoke();

        gameObject.SetActive(false);
    }

    private IEnumerator Fade(float from, float to, bool markClosable)
    {
        _cg.alpha = from;
        float t = 0f;
        while (t < fadeDuration)
        {
            // unscaledDeltaTime so this still works if you ever pause with Time.timeScale = 0
            t += Time.unscaledDeltaTime;
            _cg.alpha = Mathf.Lerp(from, to, t / fadeDuration);
            yield return null;
        }
        _cg.alpha = to;

        if (markClosable) _canClose = true;
        _fade = null;
    }

    private IEnumerator Typewriter(string full)
    {
        // Set the text once and reveal it with maxVisibleCharacters. Appending
        // char by char reallocates the string every frame and rebuilds the mesh.
        noteText.text = full;
        noteText.maxVisibleCharacters = 0;
        noteText.ForceMeshUpdate();

        int total = noteText.textInfo.characterCount;
        float delay = 1f / Mathf.Max(1f, typewriterCharsPerSecond);

        for (int i = 0; i <= total; i++)
        {
            noteText.maxVisibleCharacters = i;
            yield return new WaitForSecondsRealtime(delay);
        }

        noteText.maxVisibleCharacters = int.MaxValue;
        _typing = null;
    }
}
