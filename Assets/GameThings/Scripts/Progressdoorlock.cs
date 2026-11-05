using UnityEngine;
using UnityEngine.Events;

// Put this on any door that opens from progress.
// All three checks must pass. Leave any of them empty / 0 to skip it.
public class ProgressDoorLock : MonoBehaviour
{
    [Tooltip("Specific memory cubes whose worlds must be completed. Drag the cube objects in.")]
    public MemoryCube[] requiredMemories;

    [Tooltip("Extra: how many memory worlds must be completed in total (any worlds). Usually 0.")]
    public int memoriesRequired = 0;

    [Tooltip("Photo spots that must be captured. Drag the spot objects in.")]
    public PhotoSpot[] requiredSpots;

    [Tooltip("Drag your sliding door here and pick Unlock.")]
    public UnityEvent onUnlock;

    bool unlocked;

    void Start()
    {
        InvokeRepeating(nameof(Check), 0.1f, 0.5f);
    }

    void Check()
    {
        if (unlocked) return;

        foreach (var cube in requiredMemories)
            if (cube != null && !GameProgress.IsWorldComplete(cube.sceneName)) return;

        if (GameProgress.CompletedCount < memoriesRequired) return;

        foreach (var spot in requiredSpots)
            if (spot != null && !spot.Captured) return;

        unlocked = true;
        CancelInvoke(nameof(Check));
        onUnlock.Invoke();
    }
}