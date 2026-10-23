using UnityEngine;

// Add this to your BASE memory cube prefab, then set the values on each variant.
public class MemoryCube : MonoBehaviour
{
    [Tooltip("Display name, e.g. 'Memory 01 - Cyberpunk City'")]
    public string memoryName = "Memory 01";

    [Tooltip("Must exactly match the scene file name AND be in Build Settings.")]
    public string sceneName = "Memory_CyberpunkCity";

    [Tooltip("Optional: matches the cube's frame color, used to tint the console screen on insert.")]
    public Color memoryColor = Color.cyan;
}