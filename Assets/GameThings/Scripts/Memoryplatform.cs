using UnityEngine;
using UnityEngine.Events;

// Put this on the platform. Needs a BoxCollider (tall enough to reach head height).
// Works like a button you stand on: when the player's head has been inside the box
// for holdTime seconds, it fires onActivated ONCE. Stepping off resets it.
// Hook onActivated -> ConsoleMemoryLoader.OnButtonPressed
[RequireComponent(typeof(BoxCollider))]
public class MemoryPlatform : MonoBehaviour
{
    [Tooltip("Seconds the player has to stand on the platform before it activates.")]
    public float holdTime = 1.0f;

    [Tooltip("Drag the console in here so the platform screen can show the LINKING bar.")]
    public ConsoleMemoryLoader console;

    [Tooltip("Called once when the player has stood on the platform long enough.")]
    public UnityEvent onActivated;

    [Tooltip("Optional: called when the player steps on / off (for sounds or lights).")]
    public UnityEvent onPlayerEnter;
    public UnityEvent onPlayerExit;

    private BoxCollider zone;
    private Transform playerHead;
    private bool playerInside = false; // is the player standing on it right now
    private float timer = 0f;          // how long they've been standing there
    private bool hasActivated = false; // stops it firing again until they step off

    void Start()
    {
        zone = GetComponent<BoxCollider>();
        zone.isTrigger = true;
        playerHead = Camera.main.transform; // VR headset or flat-screen camera
    }

    void Update()
    {
        // Is the player's head inside the platform's box?
        bool insideNow = zone.bounds.Contains(playerHead.position);

        // Stepped on
        if (insideNow && !playerInside)
        {
            onPlayerEnter.Invoke();
        }

        // Stepped off: reset so it can be used again
        if (!insideNow && playerInside)
        {
            onPlayerExit.Invoke();
            if (!hasActivated && console != null) console.CancelLinkProgress(); // left before it finished
            timer = 0f;
            hasActivated = false;
        }

        playerInside = insideNow;

        // Count up while standing on it, then "press" once
        if (playerInside && !hasActivated)
        {
            timer += Time.deltaTime;
            if (console != null) console.ShowLinkProgress(timer / holdTime);

            if (timer >= holdTime)
            {
                hasActivated = true;
                onActivated.Invoke();
            }
        }
    }
}