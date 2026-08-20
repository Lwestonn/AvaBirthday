using UnityEngine;

/// <summary>
/// One memory. Create these via Assets > Create > Birthday > Memory.
/// This is the ONLY place you author content. No code changes needed to add
/// a new memory: make a new asset, fill it in, drop it on a pickup.
/// </summary>
[CreateAssetMenu(fileName = "Memory_", menuName = "Birthday/Memory")]
public class MemoryData : ScriptableObject
{
    [Header("Shown to Ava")]
    public string title = "That night at the beach";

    [TextArea(4, 10)]
    public string note = "Write the actual memory here. Two or three sentences is plenty.";

    [Tooltip("Drag a photo here. Set its Texture Type to 'Sprite (2D and UI)' in the import settings first.")]
    public Sprite photo;

    [Header("Optional")]
    [Tooltip("A short voice clip or sound that plays when she opens this memory.")]
    public AudioClip voiceClip;

    [Tooltip("Tint for this memory's glow in the world. Purely cosmetic.")]
    public Color glowColor = new Color(1f, 0.75f, 0.85f);
}
