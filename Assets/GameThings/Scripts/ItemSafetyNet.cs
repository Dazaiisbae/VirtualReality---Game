using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

// Put this on any grabbable item that needs a home: memory cubes, the slate camera, etc.
// Sends the item back to its home socket if it falls below the floor
// or sits loose (not held, not in any socket) for too long.
[RequireComponent(typeof(XRGrabInteractable), typeof(Rigidbody))]
public class ItemSafetyNet : MonoBehaviour
{
    [Tooltip("The socket this item belongs in (shelf spot for cubes, hip holster for the camera).")]
    public XRSocketInteractor homeSocket;

    [Tooltip("If the item drops below this world Y position, it returns home.")]
    public float killY = -2f;

    [Tooltip("Seconds the item can sit loose before returning home. 0 = never.")]
    public float idleReturnDelay = 10f;

    XRGrabInteractable grab;
    Rigidbody rb;
    float looseTimer;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        // Held by a hand, or sitting in any socket = safe.
        if (grab.isSelected)
        {
            looseTimer = 0f;
            return;
        }

        if (transform.position.y < killY)
        {
            ReturnHome();
            return;
        }

        if (idleReturnDelay > 0f)
        {
            looseTimer += Time.deltaTime;
            if (looseTimer >= idleReturnDelay)
                ReturnHome();
        }
    }

    public void ReturnHome()
    {
        looseTimer = 0f;
        if (homeSocket == null) return;

        // Stop any motion so it doesn't fly back out.
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        Transform target = homeSocket.attachTransform != null
            ? homeSocket.attachTransform
            : homeSocket.transform;

        transform.SetPositionAndRotation(target.position, target.rotation);

        // Snap it into the socket right away, if the spot is empty.
        if (!homeSocket.hasSelection && grab.interactionManager != null)
            grab.interactionManager.SelectEnter(
                (IXRSelectInteractor)homeSocket,
                (IXRSelectInteractable)grab);
    }
}