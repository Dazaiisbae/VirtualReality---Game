using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class PhotoMilestone : MonoBehaviour
{
    public int photosNeeded = 3;
    public UnityEvent onReached;
    bool done;

    IEnumerator Start()
    {
        foreach (var spot in PhotoSpot.Active)
            spot.onCaptured.AddListener(Check);

        // Wait one frame, then check saved photos so milestone doors re-open on load.
        yield return null;
        Check();
    }

    void Check()
    {
        if (done) return;
        int count = 0;
        foreach (var spot in PhotoSpot.Active)
            if (spot.Captured) count++;
        if (count >= photosNeeded) { done = true; onReached.Invoke(); }
    }
}