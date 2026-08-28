using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Replaces the old full-screen memory panel. When she finds a memory:
///   - the photo card slides into the corner
///   - the head speaks the note aloud, one sentence at a time, above itself
///   - she keeps full control the whole time
///
/// If she runs into another orb mid-story, that memory is queued and told next
/// rather than cutting the current one off.
/// </summary>
public class MemoryNarrator : MonoBehaviour
{
    [Header("Wiring")]
    [Tooltip("Leave empty to find them automatically at startup.")]
    public HeadBarks head;
    public MemoryCardUI card;

    [Header("Pacing")]
    [Tooltip("Seconds each sentence stays up, before the per-character bonus below.")]
    public float baseLineTime = 1.6f;

    [Tooltip("Extra seconds per character, so long sentences linger.")]
    public float perCharTime = 0.045f;

    [Tooltip("Gap between sentences.")]
    public float lineGap = 0.35f;

    [Tooltip("How long the card hangs around after the last line.")]
    public float cardHoldAfterSpeech = 1.5f;

    [Header("Audio")]
    [Tooltip("Optional AudioSource for MemoryData.voiceClip. Falls back to one on the head.")]
    public AudioSource voiceSource;

    private readonly Queue<(MemoryData memory, Action done)> _queue = new();
    private bool _busy;

    public bool IsBusy => _busy;

    private void Start()
    {
        if (head == null) head = FindFirstObjectByType<HeadBarks>();
        if (card == null) card = FindFirstObjectByType<MemoryCardUI>(FindObjectsInactive.Include);
        if (voiceSource == null && head != null) voiceSource = head.GetComponent<AudioSource>();
    }

    /// <summary>Tell a memory. Queues if one is already being told.</summary>
    public void Narrate(MemoryData memory, Action onComplete = null)
    {
        if (memory == null) { onComplete?.Invoke(); return; }

        _queue.Enqueue((memory, onComplete));
        if (!_busy) StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        _busy = true;

        while (_queue.Count > 0)
        {
            var (memory, done) = _queue.Dequeue();
            yield return NarrateOne(memory);
            done?.Invoke();
        }

        _busy = false;
    }

    private IEnumerator NarrateOne(MemoryData memory)
    {
        if (card != null) card.Show(memory);

        if (voiceSource != null && memory.voiceClip != null)
        {
            voiceSource.clip = memory.voiceClip;
            voiceSource.Play();
        }

        foreach (string line in SplitIntoLines(memory.note))
        {
            if (head != null) head.Say(line);

            float hold = baseLineTime + line.Length * perCharTime;
            yield return new WaitForSeconds(hold);
            yield return new WaitForSeconds(lineGap);
        }

        yield return new WaitForSeconds(cardHoldAfterSpeech);
        if (card != null) card.Hide();
    }

    /// <summary>
    /// Breaks the note into speakable chunks. Splits on sentence endings and on
    /// blank lines, keeping the punctuation, so you can just write naturally in
    /// the MemoryData note field and it paces itself.
    /// </summary>
    public static IEnumerable<string> SplitIntoLines(string note)
    {
        if (string.IsNullOrWhiteSpace(note)) yield break;

        // Split after . ! ? or a line break, keeping the terminator attached.
        var parts = Regex.Split(note.Trim(), @"(?<=[\.\!\?])\s+|\n+");

        foreach (var raw in parts)
        {
            string s = raw.Trim();
            if (s.Length == 0) continue;

            // Very long sentences get broken at a comma so the bubble stays readable.
            if (s.Length > 140)
            {
                int cut = s.LastIndexOf(", ", Mathf.Min(140, s.Length - 1), StringComparison.Ordinal);
                if (cut > 40)
                {
                    yield return s[..(cut + 1)].Trim();
                    yield return s[(cut + 1)..].Trim();
                    continue;
                }
            }

            yield return s;
        }
    }
}
