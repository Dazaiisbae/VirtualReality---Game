using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

// Put this on the root of your polaroid frame prefab.
// The root also needs: Rigidbody, a Collider, and XR Grab Interactable.
public class PolaroidPhoto : MonoBehaviour
{
    [Tooltip("The quad/plane inside the frame where the picture shows. Use an Unlit material.")]
    public Renderer photoRenderer;

    [Header("Developing effect")]
    [Tooltip("Seconds for the photo to fade in, like a real polaroid. 0 = instant.")]
    public float developTime = 3f;
    public Color undevelopedColor = new Color(0.12f, 0.12f, 0.14f);

    [Header("Throwing")]
    [Tooltip("1 = throws at your hand's speed. Raise for stronger throws.")]
    public float throwStrength = 1f;

    [Header("Sticky walls")]
    [Tooltip("Only surfaces with this tag (or a parent with it) can hold photos.")]
    public string stickyTag = "Stickable";
    [Tooltip("How close to a wall the photo must be when you let go.")]
    public float stickDistance = 0.1f;
    [Tooltip("Small gap from the wall so the photo doesn't sink into it or flicker.")]
    public float wallGap = 0.005f;
    [Tooltip("Also stick when a thrown photo hits a sticky wall.")]
    public bool stickOnThrow = false;
    [Tooltip("Straightens photos on walls to the nearest upright/sideways angle instead of keeping your hand's tilt.")]
    public bool straighten = true;
    [Tooltip("Turn on if photos end up stuck with the picture facing the wall.")]
    public bool flipFacing = false;
    public AudioClip stickSound;

    private bool isStuck;
    private Material photoMat;
    private Rigidbody rb;
    private XRGrabInteractable grab;
    private Collider[] colliders;
    private Vector3 lastPosition;
    private Vector3 heldVelocity;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();
        colliders = GetComponentsInChildren<Collider>();
        if (photoRenderer != null) photoMat = photoRenderer.material;

        if (grab != null)
        {
            // Otherwise XRI puts it back inside the camera when you let go.
            grab.retainTransformParent = false;
            // XRI can't throw it (it's kinematic while stuck in the camera), so this script does the throwing.
            grab.throwOnDetach = false;
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnReleased);
        }
    }

    // The player's body capsule (CharacterController) shoves dropped photos along
    // as you walk and can push them through the floor, so photos ignore it.
    private void IgnorePlayerBody()
    {
        foreach (CharacterController body in FindObjectsOfType<CharacterController>())
            IgnoreCollider(body);
    }

    private void IgnoreCollider(Collider other)
    {
        if (other == null) return;
        foreach (Collider c in colliders) Physics.IgnoreCollision(c, other, true);
    }

    public void SetPhoto(Texture2D photo)
    {
        if (photoMat == null) return;
        photoMat.mainTexture = photo;
        SetTint(developTime > 0f ? undevelopedColor : Color.white);
    }

    public void StartDeveloping()
    {
        if (developTime > 0f && photoMat != null) StartCoroutine(DevelopRoutine());
    }

    // alsoIgnore: the camera's colliders. If the photo and camera can touch,
    // the stuck-in photo shoves the camera away when you let go of it.
    public void SetGrabbable(bool canGrab, Collider[] alsoIgnore = null)
    {
        // While ejecting it's frozen and can't be grabbed.
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        foreach (Collider c in colliders) c.enabled = canGrab;

        // Unity forgets ignore settings when a collider is turned off,
        // so set them up again every time the colliders turn on.
        if (canGrab)
        {
            IgnorePlayerBody();
            if (alsoIgnore != null)
                foreach (Collider other in alsoIgnore) IgnoreCollider(other);
        }
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        // Detach from the camera once you pull it out.
        transform.SetParent(null, true);
        isStuck = false;
        lastPosition = transform.position;
        heldVelocity = Vector3.zero;
    }

    void Update()
    {
        // While held, track how fast your hand is moving it.
        if (grab == null || !grab.isSelected || Time.deltaTime <= 0f) return;
        Vector3 frameVelocity = (transform.position - lastPosition) / Time.deltaTime;
        heldVelocity = Vector3.Lerp(heldVelocity, frameVelocity, 0.5f);
        lastPosition = transform.position;
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        StartCoroutine(EnablePhysicsNextFrame());
    }

    private IEnumerator EnablePhysicsNextFrame()
    {
        // Wait a frame so the grab system finishes its own cleanup first.
        yield return null;
        if (rb != null && !grab.isSelected)
        {
            if (TryStickToNearbyWall()) yield break;

            rb.isKinematic = false;
            rb.useGravity = true;
            // Thin objects can skip through the floor between physics steps; this stops that.
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.velocity = heldVelocity * throwStrength;
        }
    }

    // ---------- Sticky walls ----------

    // The direction the picture faces (out of the photo, toward whoever is looking at it).
    private Vector3 PhotoFront()
    {
        // A Unity Quad shows its picture on the side facing its -Z.
        Vector3 quadFront = photoRenderer != null ? -photoRenderer.transform.forward : transform.forward;
        if (flipFacing) quadFront = -quadFront;

        // Use the frame's thinnest side as the true flat face, so it lies flush
        // even if the photo Quad is rotated slightly inside the frame.
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) return quadFront;

        Vector3 size = Vector3.Scale(box.size, transform.lossyScale);
        size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
        Vector3 axis = size.x <= size.y && size.x <= size.z ? transform.right
                     : size.y <= size.z ? transform.up
                     : transform.forward;

        return Vector3.Dot(axis, quadFront) >= 0f ? axis : -axis;
    }

    private Vector3 PhotoUp()
    {
        return photoRenderer != null ? photoRenderer.transform.up : transform.up;
    }

    private bool TryStickToNearbyWall()
    {
        Vector3 front = PhotoFront();

        // Normal case: the wall is behind the photo.
        if (FindStickyWall(-front, out RaycastHit hit))
        {
            StickTo(hit.point, hit.normal);
            return true;
        }

        // You pressed it picture-first into the wall: flip it around, then stick.
        if (FindStickyWall(front, out hit))
        {
            transform.rotation = Quaternion.AngleAxis(180f, PhotoUp()) * transform.rotation;
            StickTo(hit.point, hit.normal);
            return true;
        }
        return false;
    }

    private bool FindStickyWall(Vector3 direction, out RaycastHit best)
    {
        best = default;
        // Start a little in front so it still works if the photo is slightly inside the wall.
        Vector3 origin = transform.position - direction * 0.05f;
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, stickDistance + 0.05f, ~0, QueryTriggerInteraction.Ignore);

        float closest = float.MaxValue;
        foreach (RaycastHit h in hits)
        {
            if (h.distance < closest && IsSticky(h.collider))
            {
                closest = h.distance;
                best = h;
            }
        }
        return closest < float.MaxValue;
    }

    private bool IsSticky(Collider c)
    {
        // Checks the object and its parents, so tagging a whole wall group works.
        for (Transform t = c.transform; t != null; t = t.parent)
            if (t.gameObject.tag == stickyTag) return true;
        return false;
    }

    private void StickTo(Vector3 point, Vector3 wallNormal)
    {
        // Turn it flat against the wall.
        transform.rotation = Quaternion.FromToRotation(PhotoFront(), wallNormal) * transform.rotation;

        // Snap the tilt to the nearest 90 degrees (skipped on floors and ceilings).
        if (straighten && Mathf.Abs(Vector3.Dot(wallNormal, Vector3.up)) < 0.9f)
        {
            Vector3 wallUp = Vector3.ProjectOnPlane(Vector3.up, wallNormal).normalized;
            Vector3 photoUp = Vector3.ProjectOnPlane(PhotoUp(), wallNormal).normalized;
            float angle = Vector3.SignedAngle(photoUp, wallUp, wallNormal);
            float correction = angle - 90f * Mathf.Round(angle / 90f);
            transform.rotation = Quaternion.AngleAxis(correction, wallNormal) * transform.rotation;
        }

        transform.position = point;
        PushOutOfWall(point, wallNormal);

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.isKinematic = true;
        rb.useGravity = false;
        isStuck = true;

        if (stickSound != null) AudioSource.PlayClipAtPoint(stickSound, point, 0.6f);
    }

    // Moves the photo out so its back sits exactly on the wall, whatever its thickness or pivot.
    private void PushOutOfWall(Vector3 point, Vector3 wallNormal)
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null)
        {
            transform.position = point + wallNormal * wallGap;
            return;
        }

        Vector3 half = Vector3.Scale(box.size * 0.5f, transform.lossyScale);
        float backExtent =
            Mathf.Abs(Vector3.Dot(wallNormal, transform.right)) * Mathf.Abs(half.x) +
            Mathf.Abs(Vector3.Dot(wallNormal, transform.up)) * Mathf.Abs(half.y) +
            Mathf.Abs(Vector3.Dot(wallNormal, transform.forward)) * Mathf.Abs(half.z);

        Vector3 center = transform.TransformPoint(box.center);
        float currentDistance = Vector3.Dot(center - point, wallNormal);
        transform.position += wallNormal * (backExtent + wallGap - currentDistance);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!stickOnThrow || isStuck || rb == null || rb.isKinematic) return;
        if (grab != null && grab.isSelected) return;
        if (!IsSticky(collision.collider)) return;

        ContactPoint contact = collision.GetContact(0);
        // Make sure the picture side ends up facing out of the wall.
        if (Vector3.Dot(PhotoFront(), contact.normal) < 0f)
            transform.rotation = Quaternion.AngleAxis(180f, PhotoUp()) * transform.rotation;
        StickTo(contact.point, contact.normal);
    }

    // ---------- Developing ----------

    private IEnumerator DevelopRoutine()
    {
        float t = 0f;
        while (t < developTime)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / developTime);
            SetTint(Color.Lerp(undevelopedColor, Color.white, k));
            yield return null;
        }
        SetTint(Color.white);
    }

    private bool warnedNoTint;

    // Works with URP shaders (_BaseColor) and built-in/legacy ones (_Color).
    private void SetTint(Color c)
    {
        if (photoMat == null) return;
        bool found = false;
        if (photoMat.HasProperty("_BaseColor")) { photoMat.SetColor("_BaseColor", c); found = true; }
        if (photoMat.HasProperty("_Color")) { photoMat.SetColor("_Color", c); found = true; }

        if (!found && !warnedNoTint)
        {
            warnedNoTint = true;
            Debug.LogWarning($"Polaroid photo shader '{photoMat.shader.name}' has no color tint, so it can't develop. Use Universal Render Pipeline/Unlit.");
        }
    }
}