using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;

// Put this on the exit cube in each memory world (with an XR Simple Interactable).
// Grab and hold for holdDuration seconds to exit. Letting go early cancels.
// Hook your "return to archive" code into On Exit in the Inspector.
[RequireComponent(typeof(XRSimpleInteractable))]
public class ExitCube : MonoBehaviour
{
    [Tooltip("How long the player must hold before exiting.")]
    public float holdDuration = 1.75f;

    [Tooltip("The cube's visual (the mesh, not this object). Shrinks while held.")]
    public Transform visual;

    [Tooltip("How small the visual gets right before exiting (1 = no shrink).")]
    public float endScale = 0.6f;

    [Tooltip("Haptic strength while holding (0 to 1).")]
    public float hapticStrength = 0.3f;

    [Tooltip("Called once when the hold completes.")]
    public UnityEvent onExit;

    XRSimpleInteractable interactable;
    XRBaseControllerInteractor holdingHand;
    Vector3 startScale;
    float holdTimer;
    bool holding;
    bool done;

    void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        if (visual != null) startScale = visual.localScale;
    }

    void OnEnable()
    {
        interactable.selectEntered.AddListener(OnGrab);
        interactable.selectExited.AddListener(OnRelease);
    }

    void OnDisable()
    {
        interactable.selectEntered.RemoveListener(OnGrab);
        interactable.selectExited.RemoveListener(OnRelease);
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        if (done) return;
        holding = true;
        holdTimer = 0f;
        holdingHand = args.interactorObject as XRBaseControllerInteractor;
    }

    void OnRelease(SelectExitEventArgs args)
    {
        holding = false;
        holdingHand = null;
    }

    void Update()
    {
        if (done) return;

        if (holding)
        {
            holdTimer += Time.deltaTime;
            if (holdingHand != null)
                holdingHand.SendHapticImpulse(hapticStrength * Progress, Time.deltaTime);

            if (holdTimer >= holdDuration)
            {
                done = true;
                SaveBeforeExit();
                onExit.Invoke();
            }
        }
        else
        {
            // Ease back if they let go early.
            holdTimer = Mathf.Max(0f, holdTimer - Time.deltaTime * 2f);
        }

        if (visual != null)
            visual.localScale = startScale * Mathf.Lerp(1f, endScale, Progress);
    }

    // Saves world completion and collects this world's polaroids for the archive tray.
    // Runs before On Exit, so it happens before the archive room starts loading.
    void SaveBeforeExit()
    {
        var world = FindObjectOfType<MemoryWorldPersistence>();
        if (world != null) world.ExitWorld();
        else Debug.LogWarning("[ExitCube] No MemoryWorldPersistence in this scene, so nothing was saved.");
    }

    float Progress => Mathf.Clamp01(holdTimer / holdDuration);
}