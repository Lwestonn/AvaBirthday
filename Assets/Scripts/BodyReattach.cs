using UnityEngine;
using UnityEngine.Events;
using TMPro;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Luke's headless body. Stands somewhere visible from the start as the goal
/// she can see but not yet use.
///
/// Flow:
///   locked   -> 8 marks dark, walking up says "not yet"
///   unlocked -> MemoryManager.onAllCollected calls Unlock(), marks all lit
///   reattach -> she walks over HOLDING the head, presses E, onReattached fires
///
/// Setup:
///   - put this on the body GameObject
///   - add a Collider with Is Trigger ticked, radius ~3
///   - make an empty child at the neck and drag it into neckSocket
///   - wire MemoryManager.onAllCollected -> BodyReattach.Unlock
///   - wire onReattached -> FinaleGrowth.Play
/// </summary>
public class BodyReattach : MonoBehaviour
{
    [Header("Wiring")]
    [Tooltip("Empty child positioned exactly where the head should sit.")]
    public Transform neckSocket;

    [Tooltip("World-space TextMeshPro (3D) prompt above the body. Optional.")]
    public TMP_Text promptLabel;

    [Tooltip("The 8 marks on the body that light up, one per memory. Optional but a great progress meter.")]
    public Renderer[] progressMarks;

    public Color markDarkColor = new(0.25f, 0.25f, 0.3f);
    public Color markLitColor = new(1f, 0.6f, 0.8f);
    [Tooltip("Emission strength on a lit mark. Bloom picks this up.")]
    public float markEmission = 2.5f;

    [Header("Prompts")]
    public string lockedPrompt = "He's missing something. Keep looking.";
    public string readyPrompt = "Press E to put him back together";
    public string needHeadPrompt = "You'll need his head for this";

    [Header("Reattach animation")]
    public float snapDuration = 0.7f;
    public AudioClip reattachSound;

    [Header("Events")]
    public UnityEvent onUnlocked;
    public UnityEvent onReattached;

    private bool _unlocked;
    private bool _done;
    private bool _playerInside;
    private HeadCarrier _carrier;
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    public bool IsUnlocked => _unlocked;

    private void Awake()
    {
        if (neckSocket == null) neckSocket = transform;
        SetPrompt("");
        RefreshMarks(0);
    }

    /// <summary>Hook this to MemoryManager.onProgressChanged (Dynamic int, int).</summary>
    public void SetProgress(int collected, int total)
    {
        int marks = progressMarks?.Length ?? 0;

        // The number of marks does not have to match the number of memories. With
        // 8 marks and 10 memories, lighting one per memory would stall the meter
        // for the last two, which reads as the game being broken right at the
        // moment she is closest to finishing. Scale instead.
        int lit = (total > 0 && marks > 0)
            ? Mathf.RoundToInt(marks * (collected / (float)total))
            : collected;

        // Never show a full chest before she is actually done.
        if (collected < total && lit >= marks) lit = marks - 1;

        RefreshMarks(lit);
    }

    /// <summary>
    /// Testing shortcut. Right-click the BodyReattach header in the Inspector
    /// while the game is running and pick this. No searching, no menu items, it
    /// acts on the exact component you right-clicked.
    /// </summary>
    [ContextMenu("Unlock Now (testing)")]
    private void UnlockFromInspector()
    {
        Unlock();
        Debug.Log($"[BodyReattach] '{name}' unlocked by hand. Carry the head over and press E.", this);
    }

    /// <summary>Hook this to MemoryManager.onAllCollected.</summary>
    public void Unlock()
    {
        if (_unlocked) return;
        _unlocked = true;

        if (progressMarks != null) RefreshMarks(progressMarks.Length);
        onUnlocked?.Invoke();

        if (_playerInside) SetPrompt(CurrentPrompt());
    }

    private void Update()
    {
        if (_done || !_playerInside) return;

        SetPrompt(CurrentPrompt());

        if (!_unlocked) return;
        if (_carrier == null || !_carrier.IsHolding) return;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && kb.eKey.wasPressedThisFrame) DoReattach();
#else
        if (Input.GetKeyDown(KeyCode.E)) DoReattach();
#endif
    }

    private string CurrentPrompt()
    {
        if (!_unlocked) return lockedPrompt;
        if (_carrier == null || !_carrier.IsHolding) return needHeadPrompt;
        return readyPrompt;
    }

    private void DoReattach()
    {
        var head = _carrier.Held;
        if (head == null) return;

        _done = true;
        SetPrompt("");

        // Silence him BEFORE the head leaves her hands. Drop() fires onDropped,
        // which HeadBarks listens to, so without this he says "Rude." at the exact
        // moment she is putting him back together. FinaleGrowth also calls
        // EndGameMode, but that happens after the snap, which is far too late.
        var barks = head.GetComponent<HeadBarks>();
        if (barks == null) barks = FindFirstObjectByType<HeadBarks>();
        if (barks != null) barks.EndGameMode();

        // Take the head off the carrier without dropping it into physics.
        head.Drop();
        StartCoroutine(SnapHome(head));
    }

    private System.Collections.IEnumerator SnapHome(HeadPickup head)
    {
        var rb = head.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        Vector3 from = head.transform.position;
        Quaternion fromRot = head.transform.rotation;

        float t = 0f;
        while (t < snapDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / snapDuration);
            head.transform.position = Vector3.Lerp(from, neckSocket.position, k);
            head.transform.rotation = Quaternion.Slerp(fromRot, neckSocket.rotation, k);
            yield return null;
        }

        head.transform.SetPositionAndRotation(neckSocket.position, neckSocket.rotation);
        head.transform.SetParent(neckSocket, true);

        // Turn the head into a normal child object now that it is attached.
        var col = head.GetComponent<Collider>();
        if (col != null) col.enabled = false;
        head.enabled = false;

        if (reattachSound != null)
            AudioSource.PlayClipAtPoint(reattachSound, neckSocket.position);

        onReattached?.Invoke();
    }

    private void RefreshMarks(int lit)
    {
        if (progressMarks == null) return;

        for (int i = 0; i < progressMarks.Length; i++)
        {
            var r = progressMarks[i];
            if (r == null) continue;

            bool on = i < lit;

            // Instance the material so lighting one mark does not light them all.
            var mat = r.material;
            mat.color = on ? markLitColor : markDarkColor;

            if (on)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor(EmissionColor, markLitColor * markEmission);
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                mat.SetColor(EmissionColor, Color.black);
            }
        }
    }

    private void SetPrompt(string text)
    {
        if (promptLabel == null) return;
        if (promptLabel.text != text) promptLabel.text = text;
    }

    private void OnTriggerEnter(Collider other)
    {
        var carrier = other.GetComponentInParent<HeadCarrier>();
        if (carrier == null) return;

        _carrier = carrier;
        _playerInside = true;
    }

    private void OnTriggerExit(Collider other)
    {
        var carrier = other.GetComponentInParent<HeadCarrier>();
        if (carrier == null || carrier != _carrier) return;

        _playerInside = false;
        _carrier = null;
        SetPrompt("");
    }
}
