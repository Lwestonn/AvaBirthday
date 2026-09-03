using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Start screen and pause menu in one component, because they share all the
/// same plumbing (lock the player, free the cursor, put a panel up).
///
/// The start screen is NOT a still image: the real 3D scene renders behind it
/// with Ava standing there, so it is the actual game world you are looking at.
///
/// Built by Tools > Birthday > Build UI.
/// </summary>
public class GameMenus : MonoBehaviour
{
    [Header("Panels")]
    public GameObject startPanel;
    public GameObject pausePanel;

    [Tooltip("The StartStage object: its own camera, its own light, and the spinning copy of Ava. " +
             "Shown with the start screen and switched off the moment she presses Play, so it costs " +
             "nothing during the game. Built by Tools > Birthday > Build Start Screen.")]
    public GameObject startStage;

    [Header("Start screen buttons")]
    public Button playButton;
    public Button quitButton;

    [Header("Pause buttons")]
    public Button resumeButton;
    public Button quitFromPauseButton;

    [Header("Wiring")]
    [Tooltip("Leave empty to find it automatically.")]
    public PlayerControlLock playerLock;

    [Tooltip("Optional label on the start screen.")]
    public TMP_Text titleLabel;
    public string titleText = "Happy Birthday, Ava";

    [Header("Behaviour")]
    [Tooltip("Show the start screen on load. Turn off while you are iterating so Play drops you straight in.")]
    public bool showStartScreenOnLoad = true;

    [Tooltip("Pause the whole game clock while paused. Leave on.")]
    public bool freezeTimeWhilePaused = true;

    private bool _started;
    private bool _paused;

    public bool IsPaused => _paused;
    public bool HasStarted => _started;

    private void Awake()
    {
        if (playerLock == null) playerLock = FindFirstObjectByType<PlayerControlLock>();

        if (titleLabel != null && !string.IsNullOrEmpty(titleText))
            titleLabel.text = titleText;

        if (playButton != null) playButton.onClick.AddListener(StartGame);
        if (quitButton != null) quitButton.onClick.AddListener(QuitGame);
        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (quitFromPauseButton != null) quitFromPauseButton.onClick.AddListener(QuitGame);

        if (pausePanel != null) pausePanel.SetActive(false);
    }

    private void Start()
    {
        if (showStartScreenOnLoad)
        {
            ShowStartScreen(true);
            SetPlayerActive(false);
        }
        else
        {
            _started = true;
            ShowStartScreen(false);
            SetPlayerActive(true);
        }
    }

    /// <summary>Panel and stage always move together, so they can never disagree.</summary>
    private void ShowStartScreen(bool on)
    {
        if (startPanel != null) startPanel.SetActive(on);
        if (startStage != null) startStage.SetActive(on);
    }

    private void Update()
    {
        if (!_started) return;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) TogglePause();
#else
        if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
#endif
    }

    // ------------------------------------------------------------------ start

    public void StartGame()
    {
        _started = true;
        ShowStartScreen(false);
        SetPlayerActive(true);
    }

    // ------------------------------------------------------------------ pause

    public void TogglePause()
    {
        if (_paused) Resume();
        else Pause();
    }

    public void Pause()
    {
        if (_paused) return;
        _paused = true;

        if (pausePanel != null) pausePanel.SetActive(true);
        SetPlayerActive(false);

        if (freezeTimeWhilePaused) Time.timeScale = 0f;
    }

    public void Resume()
    {
        if (!_paused) return;
        _paused = false;

        if (pausePanel != null) pausePanel.SetActive(false);

        // Unfreeze BEFORE handing control back, or the first frame runs at dt 0.
        if (freezeTimeWhilePaused) Time.timeScale = 1f;
        SetPlayerActive(true);
    }

    // ------------------------------------------------------------------- quit

    public void QuitGame()
    {
        if (freezeTimeWhilePaused) Time.timeScale = 1f;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
        // Application.Quit does nothing in a browser tab. Best we can do is put
        // her back on the start screen rather than pretending to close.
        _started = false;
        if (pausePanel != null) pausePanel.SetActive(false);
        ShowStartScreen(true);
        SetPlayerActive(false);
#else
        Application.Quit();
#endif
    }

    // ----------------------------------------------------------------- shared

    private void SetPlayerActive(bool active)
    {
        if (playerLock != null)
        {
            playerLock.SetLocked(!active);
        }
        else
        {
            // No lock component, at least handle the cursor.
            Cursor.lockState = active ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !active;
        }
    }
}
