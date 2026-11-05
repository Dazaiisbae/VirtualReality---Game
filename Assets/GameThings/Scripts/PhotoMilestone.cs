using UnityEngine;
using UnityEngine.Events;

public class PhotoMilestone : MonoBehaviour
{
    public int photosNeeded = 3;
    public UnityEvent onReached;
    bool done;

    void Start()
    {
        foreach (var spot in PhotoSpot.Active)
            spot.onCaptured.AddListener(Check);
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