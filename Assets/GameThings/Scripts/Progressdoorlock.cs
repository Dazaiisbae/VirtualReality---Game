using UnityEngine;
using UnityEngine.Events;

// Put this on any door that needs completed memories (cockpit now, others later).
// Doors that only need photos can keep using PhotoMilestone or On Captured.
public class ProgressDoorLock : MonoBehaviour
{
    [Tooltip("How many memory worlds must be completed.")]
    public int memoriesRequired = 1;

    [Tooltip("Photo spots in this scene that must also be captured. Drag the spot objects in.")]
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
        if (GameProgress.CompletedCount < memoriesRequired) return;
        foreach (var spot in requiredSpots)
            if (spot != null && !spot.Captured) return;

        unlocked = true;
        CancelInvoke(nameof(Check));
        onUnlock.Invoke();
    }
}