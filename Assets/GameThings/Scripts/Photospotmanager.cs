using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// One per scene (ship, or each memory world).
/// Tracks how many story photos are done and fires events you wire in the Inspector:
/// dialogue, beam brightness, door unlocks, etc.
/// Also tells PhotoHintIndicator which spot to guide the player toward.
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

    [Header("Saving")]
    [Tooltip("When loading back in with saved photos, fire On Progress (current count) and " +
             "On All Captured (if complete) so doors and beams return to the right state.")]
    public bool replayEventsWhenLoaded = true;

    public int CapturedCount { get; private set; }
    public bool Complete => CapturedCount >= requiredCount;

    void Awake()
    {
        if (Instance != null && Instance != this)
            Debug.LogWarning("[PhotoSpotManager] More than one in the scene. Using the newest.");
        Instance = this;
    }

    IEnumerator Start()
    {
        // Spots restore themselves in Awake, so by now Captured is correct.
        CapturedCount = 0;
        foreach (var spot in PhotoSpot.Active)
            if (spot.Captured) CapturedCount++;

        if (CapturedCount > 0)
            Debug.Log("[PhotoSpotManager] Restored " + CapturedCount + "/" + requiredCount + " from save");

        // Wait one frame so doors finish their own Start before we unlock them.
        yield return null;

        if (replayEventsWhenLoaded && CapturedCount > 0)
        {
            onProgress.Invoke(CapturedCount);
            if (Complete) onAllCaptured.Invoke();
        }
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // A spot can be captured once every spot with a LOWER story order is captured.
    // Spots sharing the same order (e.g. three spots at 3) can be taken in any order.
    // Gaps in numbering (1, 2, 5) are fine too.
    public bool CanCapture(PhotoSpot spot)
    {
        if (!enforceOrder || spot.storyOrder <= 0) return true;
        foreach (var other in PhotoSpot.Active)
            if (!other.Captured && other.storyOrder > 0 && other.storyOrder < spot.storyOrder)
                return false;
        return true;
    }

    /// The spot the camera hint should point toward, or null if none.
    /// Uses CanCapture, so it follows story order: the next spot in sequence,
    /// or the nearest one if several share the same order.
    public PhotoSpot GetHintTarget(Vector3 fromPos)
    {
        PhotoSpot best = null;
        float bestDist = float.MaxValue;

        foreach (var spot in PhotoSpot.Active)
        {
            if (spot.Captured || !spot.showHint) continue;
            if (!CanCapture(spot)) continue;
            if (!spot.InHintArea(fromPos)) continue;

            float d = Vector3.Distance(fromPos, spot.transform.position);
            if (d < bestDist) { bestDist = d; best = spot; }
        }
        return best;
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