using UnityEngine;
using UnityEngine.Events;

/// One per scene (ship, or each memory world).
/// Tracks how many story photos are done and fires events you wire in the Inspector:
/// dialogue, beam brightness, door unlocks, etc.
public class PhotoSpotManager : MonoBehaviour
{
    public static PhotoSpotManager Instance { get; private set; }

    [Tooltip("If on, spot 2 can't be captured before spot 1, and so on.")]
    public bool enforceOrder = true;
    public int requiredCount = 3;

    [Header("Events")]
    [Tooltip("Fires after every capture with the new count (1, 2, 3).")]
    public UnityEvent<int> onProgress;
    [Tooltip("Fires once when all required photos are taken.")]
    public UnityEvent onAllCaptured;

    public int CapturedCount { get; private set; }
    public bool Complete => CapturedCount >= requiredCount;

    void Awake()
    {
        if (Instance != null && Instance != this)
            Debug.LogWarning("[PhotoSpotManager] More than one in the scene. Using the newest.");
        Instance = this;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    public bool CanCapture(PhotoSpot spot)
    {
        if (!enforceOrder || spot.storyOrder <= 0) return true;
        return spot.storyOrder == CapturedCount + 1;
    }

    public void NotifyCaptured(PhotoSpot spot)
    {
        CapturedCount++;
        Debug.Log("[PhotoSpotManager] Progress " + CapturedCount + "/" + requiredCount);
        onProgress.Invoke(CapturedCount);

        if (CapturedCount == requiredCount)
        {
            Debug.Log("[PhotoSpotManager] All photos captured");
            onAllCaptured.Invoke();
        }
    }
}