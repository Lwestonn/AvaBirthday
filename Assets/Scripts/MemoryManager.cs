using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// The brain. Put this on one empty GameObject in the scene called "GameManager".
/// Tracks what has been collected, drives the UI, and fires the finale.
/// </summary>
public class MemoryManager : MonoBehaviour
{
    public static MemoryManager Instance { get; private set; }

    [Header("Wiring")]
    public MemoryPanelUI panel;
    public PlayerControlLock playerLock;

    [Header("Progress")]
    [Tooltip("Leave at 0 to auto-count the DISTINCT MemoryData assets used by pickups in the scene.")]
    public int totalMemories = 0;

    [Header("Events")]
    [Tooltip("Fires every time a memory is collected. Hook your HUD counter here (Dynamic int, int).")]
    public UnityEvent<int, int> onProgressChanged;

    [Tooltip("Fires once, after the LAST memory panel is closed. Hook FinaleSequence.Play here.")]
    public UnityEvent onAllCollected;

    private readonly List<MemoryData> _collected = new();
    private readonly Queue<MemoryData> _pending = new();
    private bool _finaleFired;

    public int CollectedCount => _collected.Count;
    public IReadOnlyList<MemoryData> Collected => _collected;

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
        if (totalMemories <= 0)
        {
            // Count DISTINCT MemoryData, not pickups. If you duplicate a pickup and
            // forget to swap its memory asset, counting pickups makes the finale
            // unreachable with no error message.
            var pickups = FindObjectsByType<MemoryPickup>(FindObjectsSortMode.None);
            totalMemories = pickups.Select(p => p.memory)
                                   .Where(m => m != null)
                                   .Distinct()
                                   .Count();

            int missing = pickups.Count(p => p.memory == null);
            if (missing > 0)
                Debug.LogWarning($"[MemoryManager] {missing} pickup(s) have no MemoryData assigned.");

            if (pickups.Length != totalMemories)
                Debug.LogWarning($"[MemoryManager] {pickups.Length} pickups but only {totalMemories} distinct memories. Some pickups share the same asset.");
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

        if (playerLock != null) playerLock.SetLocked(true);

        if (panel == null)
        {
            OnPanelClosed();   // no UI wired yet, do not soft-lock the game
            return;
        }

        // Overlapping triggers can fire two pickups in one frame. Queue the second
        // instead of stomping the first panel mid-fade.
        if (panel.IsOpen) _pending.Enqueue(memory);
        else panel.Show(memory, OnPanelClosed);
    }

    private void OnPanelClosed()
    {
        if (_pending.Count > 0 && panel != null)
        {
            panel.Show(_pending.Dequeue(), OnPanelClosed);
            return;   // stay locked, another memory is coming
        }

        bool done = _collected.Count >= totalMemories;

        // Only give control back if we are NOT about to run the finale,
        // otherwise she can walk around during the ending.
        if (playerLock != null && !done)
            playerLock.SetLocked(false);

        if (done && !_finaleFired)
        {
            _finaleFired = true;
            onAllCollected?.Invoke();
        }
    }
}
