using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

// Put this on each key (BACK, PREV, NEXT) next to an XR Simple Interactable and a Collider.
// No events to wire: pick the key's job in the Action dropdown.
// Hold PREV or NEXT to keep stepping, and it speeds up the longer you hold.
[RequireComponent(typeof(XRSimpleInteractable))]
public class TerminalKey : MonoBehaviour
{
    public enum KeyAction { Back, Prev, Next }

    public ArchiveTerminal terminal;
    public KeyAction action = KeyAction.Next;

    [Header("Holding (PREV / NEXT)")]
    [Tooltip("Seconds before holding starts repeating.")]
    public float holdDelay = 0.45f;
    [Tooltip("Time between steps when you first start holding.")]
    public float slowRepeat = 0.15f;
    [Tooltip("Fastest time between steps after holding a while.")]
    public float fastRepeat = 0.04f;

    [Header("Press animation (optional)")]
    [Tooltip("The part that moves down when pressed. Leave empty for no animation.")]
    public Transform keyVisual;
    public float pressDepth = 0.005f;
    public AudioClip clickSound;

    private XRSimpleInteractable interactable;
    private AudioSource audioSource;
    private bool held;
    private float heldTime, nextStep;
    private Vector3 restPos;

    void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        if (keyVisual != null) restPos = keyVisual.localPosition;
        if (clickSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f;
            audioSource.playOnAwake = false;
        }
    }

    void OnEnable()
    {
        interactable.selectEntered.AddListener(OnPress);
        interactable.selectExited.AddListener(OnRelease);
    }

    void OnDisable()
    {
        interactable.selectEntered.RemoveListener(OnPress);
        interactable.selectExited.RemoveListener(OnRelease);
        held = false;
        SetPressed(false);
    }

    void OnPress(SelectEnterEventArgs args)
    {
        held = true;
        heldTime = 0f;
        nextStep = holdDelay;
        SetPressed(true);
        if (audioSource != null) audioSource.PlayOneShot(clickSound);
        Do();
    }

    void OnRelease(SelectExitEventArgs args)
    {
        held = false;
        SetPressed(false);
    }

    void Update()
    {
        if (!held || action == KeyAction.Back) return;

        heldTime += Time.deltaTime;
        if (heldTime >= nextStep)
        {
            Do();
            // Starts slow, gets faster over about 2 seconds of holding.
            float speedUp = Mathf.Clamp01((heldTime - holdDelay) / 2f);
            nextStep = heldTime + Mathf.Lerp(slowRepeat, fastRepeat, speedUp);
        }
    }

    void Do()
    {
        if (terminal == null) return;
        if (action == KeyAction.Back) terminal.Back();
        else if (action == KeyAction.Prev) terminal.Prev();
        else terminal.Next();
    }

    void SetPressed(bool down)
    {
        if (keyVisual == null) return;
        keyVisual.localPosition = down ? restPos + Vector3.down * pressDepth : restPos;
    }
}