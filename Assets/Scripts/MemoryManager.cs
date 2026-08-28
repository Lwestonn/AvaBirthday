using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// The brain. One per scene, on a GameObject called "GameManager".
///
/// Memories no longer freeze the player. Collecting one hands the memory to the
/// MemoryNarrator, which shows the photo card and has the head tell the story
/// while she keeps walking around.
/// </summary>
public class MemoryManager : MonoBehaviour
{
    public static MemoryManager Instance { get; private set; }

    [Header("Wiring")]
    [Tooltip("Leave empty to find it automatically.")]
    public MemoryNarrator narrator;

    [Tooltip("Only used by the menus now. Memories do not lock the player.")]
    public PlayerControlLock playerLock;

    [Header("Progress")]
    [Tooltip("Leave at 0 to auto-count the DISTINCT MemoryData assets used by pickups in the scene.")]
    public int totalMemories = 0;

    [Header("Events")]
    [Tooltip("Fires on every collect. Hook the HUD counter and the body's marks here (Dynamic int, int).")]
    public UnityEvent<int, int> onProgressChanged;

    [Tooltip("Fires once when the last memory is collected. Hook BodyReattach.Unlock here.")]
    public UnityEvent onAllCollected;

    private readonly List<MemoryData> _collected = new();
    private bool _finaleFired;

    public int CollectedCount => _collected.Count;
    public IReadOnlyList<MemoryData> Collected => _collected;
    public bool AllCollected => _collected.Count >= totalMemories;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (narrator == null) narrator = FindFirstObjectByType<MemoryNarrator>();

        if (totalMemories <= 0)
        {
            // Count DISTINCT MemoryData, not pickups. If you duplicate a pickup and
            // forget to swap its asset, counting pickups makes the ending unreachable
            // with no error message.
            var pickups = FindObjectsByType<MemoryPickup>(FindObjectsSortMode.None);
            totalMemories = pickups.Select(p => p.memory)
                                   .Where(m => m != null)
                                   .Distinct()
                                   .Count();

            int missing = pickups.Count(p => p.memory == null);
            if (missing > 0)
                Debug.LogWarning($"[MemoryManager] {missing} pickup(s) have no MemoryData assigned.");

            if (pickups.Length != totalMemories)
                Debug.LogWarning($"[MemoryManager] {pickups.Length} pickups but only {totalMemories} distinct memories. Some share the same asset.");
        }

        onProgressChanged?.Invoke(0, totalMemories);
    }

    public void Collect(MemoryData memory)
    {
        if (memory == null) return;

        if (_collected.Contains(memory))
        {
            Debug.LogWarning($"[MemoryManager] '{memory.name}' was already collected. Two pickups share this asset.");
            return;
        }

        _collected.Add(memory);
        onProgressChanged?.Invoke(_collected.Count, totalMemories);

        // The narrator queues internally, so running into two orbs at once is fine.
        if (narrator != null) narrator.Narrate(memory);

        // Unlock the body immediately on the last one. She does not have to wait
        // for the head to finish talking before she can go put it back on.
        if (AllCollected && !_finaleFired)
        {
            _finaleFired = true;
            onAllCollected?.Invoke();
        }
    }
}
