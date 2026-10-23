using UnityEngine;

// Put this on the holster object (the one with the XR Socket Interactor).
// Make it a child of the XR Origin, NOT the Main Camera, so it doesn't tilt with your head.
// It follows your head's position and the direction you face, staying at hip height.
public class HipHolster : MonoBehaviour
{
    [Tooltip("The Main Camera inside your XR Origin. Found automatically if left empty.")]
    public Transform head;

    [Header("Position")]
    [Tooltip("How far below your eyes the holster sits. About 0.6 for standing, 0.45 if seated.")]
    public float dropBelowHead = 0.6f;
    [Tooltip("Positive = right hip, negative = left hip.")]
    public float sideOffset = 0.2f;
    [Tooltip("Small forward nudge so it's easy to see when you look down.")]
    public float forwardOffset = 0.05f;

    [Header("Rotation")]
    [Tooltip("Tilts the holster forward so the camera faces up toward you a bit.")]
    public float tiltAngle = 0f;
    [Tooltip("How quickly the holster turns to match your facing. Lower = quick glances don't swing it around.")]
    public float turnSpeed = 4f;

    private Quaternion currentYaw;

    void Start()
    {
        if (head == null && Camera.main != null) head = Camera.main.transform;
        if (head != null) currentYaw = GetYaw();
    }

    void LateUpdate()
    {
        if (head == null) return;

        // Turn smoothly toward where you're facing (ignores looking up/down).
        currentYaw = Quaternion.Slerp(currentYaw, GetYaw(), turnSpeed * Time.deltaTime);

        Vector3 offset = currentYaw * new Vector3(sideOffset, 0f, forwardOffset);
        transform.position = head.position + Vector3.down * dropBelowHead + offset;
        transform.rotation = currentYaw * Quaternion.Euler(tiltAngle, 0f, 0f);
    }

    private Quaternion GetYaw()
    {
        Vector3 flatForward = Vector3.ProjectOnPlane(head.forward, Vector3.up);

        // Looking straight down: use the top of your head as "forward" instead.
        if (flatForward.sqrMagnitude < 0.001f)
            flatForward = Vector3.ProjectOnPlane(head.up, Vector3.up);

        return Quaternion.LookRotation(flatForward.normalized, Vector3.up);
    }
}