using System.Reflection;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Stops the camera teleporting in WebGL.
///
/// In a browser, mouse look runs on the Pointer Lock API, and browsers
/// occasionally report a single enormous movement value: when lock is acquired or
/// re-acquired, when the tab regains focus, when the cursor crosses the window
/// edge, or simply as a hiccup. Unity hands that straight to the camera as
/// degrees, so one bad frame spins you 180 degrees and it feels like the game
/// broke.
///
/// This watches the look input and throws away frames that are wildly out of
/// character compared to how fast the mouse has actually been moving. Genuine
/// fast turns still work, because those build up over several frames; a spike
/// appears from nothing in one.
///
/// Put it on the same object as your player controller. It finds the input
/// component by reflection, so it does not care which version of Starter Assets
/// you have or which assembly it lives in.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class MouseLookGuard : MonoBehaviour
{
    [Header("Spike rejection")]
    [Tooltip("Any single frame larger than this is treated as a glitch, no matter what. " +
             "Normal fast mouse turns sit well under it.")]
    public float absoluteCeiling = 140f;

    [Tooltip("Also reject anything this many times bigger than the recent average. Catches spikes " +
             "that are under the ceiling but still obviously wrong.")]
    public float spikeMultiplier = 6f;

    [Tooltip("Ignore the multiplier test below this size, so tiny movements are never judged against " +
             "an even tinier average.")]
    public float ignoreBelow = 12f;

    [Header("Cursor")]
    [Tooltip("Re-lock the cursor when she clicks in the page. Browsers drop pointer lock on their own, " +
             "and without this the camera starts behaving oddly until she clicks anyway.")]
    public bool relockOnClick = true;

    [Header("Diagnostics")]
    [Tooltip("Logs the biggest look delta seen, so you can tune the ceiling if it feels restrictive.")]
    public bool logSpikes;

    private Component _inputs;
    private FieldInfo _lookField;

    private float _average = 8f;
    private float _biggestSeen;
    private int _rejected;

    private void Awake()
    {
        FindLookField();
    }

    /// <summary>
    /// Finds whatever component holds the look vector. Starter Assets calls it
    /// "look" on StarterAssetsInputs, but that type sometimes lives in its own
    /// assembly, which a normal script cannot reference directly.
    /// </summary>
    private void FindLookField()
    {
        foreach (var mb in GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null || mb == this) continue;

            var f = mb.GetType().GetField("look", BindingFlags.Public | BindingFlags.Instance);
            if (f == null || f.FieldType != typeof(Vector2)) continue;

            _inputs = mb;
            _lookField = f;
            return;
        }

        Debug.LogWarning("[MouseLookGuard] Could not find a 'look' field on this object or its children. " +
                         "Put this component on the same object as your player input component.", this);
    }

    private void Update()
    {
        HandleCursor();
        Filter();
    }

    // The controller reads look again when it rotates the camera, so filter twice.
    private void LateUpdate() => Filter();

    private void Filter()
    {
        if (_lookField == null || _inputs == null) return;

        var look = (Vector2)_lookField.GetValue(_inputs);
        float mag = look.magnitude;

        if (mag > _biggestSeen) _biggestSeen = mag;
        if (mag <= 0.0001f) return;

        bool spike = mag > absoluteCeiling
                  || (mag > ignoreBelow && mag > _average * spikeMultiplier);

        if (spike)
        {
            // Drop it entirely rather than clamping. A clamped spike still snaps
            // the camera a long way, just less far; discarding costs one frame of
            // camera movement and is invisible.
            _lookField.SetValue(_inputs, Vector2.zero);
            _rejected++;

            if (logSpikes)
                Debug.Log($"[MouseLookGuard] Rejected a {mag:0} unit look spike " +
                          $"(recent average {_average:0.0}). {_rejected} so far.");

            return;
        }

        // Slow rolling average of what normal movement looks like for this player.
        _average = Mathf.Lerp(_average, mag, 0.06f);
    }

    private void HandleCursor()
    {
        if (!relockOnClick) return;

#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (!mouse.leftButton.wasPressedThisFrame && !mouse.rightButton.wasPressedThisFrame) return;
#else
        if (!Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1)) return;
#endif

        // Only re-lock if the game is actually meant to have the cursor captured.
        var menus = FindFirstObjectByType<GameMenus>();
        if (menus != null && (!menus.HasStarted || menus.IsPaused)) return;

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    /// <summary>Biggest look delta seen this session. Handy when tuning the ceiling.</summary>
    public float BiggestSeen => _biggestSeen;

    [ContextMenu("Log Diagnostics")]
    private void LogDiagnostics()
    {
        Debug.Log($"[MouseLookGuard] biggest look delta seen: {_biggestSeen:0.0}\n" +
                  $"  recent average: {_average:0.0}\n" +
                  $"  spikes rejected: {_rejected}\n" +
                  $"  input component: {(_inputs != null ? _inputs.GetType().Name : "NOT FOUND")}");
    }
}
