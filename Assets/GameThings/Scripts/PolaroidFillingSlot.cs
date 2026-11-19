using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

// The slot on the Archivist's desk terminal. Put this on the slot object with an XR Socket Interactor.
// No events to wire: it listens to its own socket.
//   Story photo  -> slides into the machine and is gone, the terminal opens its record ("FILED REC-014").
//   Random photo -> "NO RECORD FOUND", and it lifts back out so you can take it.
[RequireComponent(typeof(XRSocketInteractor))]
public class PolaroidFilingSlot : MonoBehaviour
{
    [Tooltip("The terminal that shows the catalog and record viewer.")]
    public ArchiveTerminal terminal;

    [Header("Movement")]
    [Tooltip("Which way the photo slides into the machine. Uses this object's blue arrow (Z) if empty.")]
    public Transform slideDirection;
    [Tooltip("Seconds to straighten the photo into the slot's angle before it slides in.")]
    public float snapTime = 0.15f;
    public float slideDistance = 0.16f;
    public float slideTime = 0.42f;
    [Tooltip("How far a random photo lifts back out of the slot.")]
    public float liftHeight = 0.1f;
    public float liftTime = 0.45f;

    [Header("Slot glow (optional)")]
    [Tooltip("The thin cyan strip by the slot. It thickens when a photo is near and pulses on filing.")]
    public Transform slotGlow;
    public Light slotLight;
    public float slotLightMax = 1.6f;

    [Header("Sounds (optional)")]
    public AudioClip fileSound;
    public AudioClip noRecordSound;

    private XRSocketInteractor socket;
    private AudioSource audioSource;
    private Vector3 glowBaseScale;
    private float pulse;
    private int hovering;

    void Awake()
    {
        socket = GetComponent<XRSocketInteractor>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f;
            audioSource.playOnAwake = false;
        }
        if (slotGlow != null) glowBaseScale = slotGlow.localScale;
        if (slotLight != null) slotLight.intensity = 0f;
    }

    void OnEnable()
    {
        socket.selectEntered.AddListener(OnInserted);
        socket.hoverEntered.AddListener(OnHoverIn);
        socket.hoverExited.AddListener(OnHoverOut);
    }

    void OnDisable()
    {
        socket.selectEntered.RemoveListener(OnInserted);
        socket.hoverEntered.RemoveListener(OnHoverIn);
        socket.hoverExited.RemoveListener(OnHoverOut);
    }

    void OnHoverIn(HoverEnterEventArgs a) { hovering++; }
    void OnHoverOut(HoverExitEventArgs a) { hovering = Mathf.Max(0, hovering - 1); }

    void Update()
    {
        // Same as the HTML: the strip grows while a photo is near, and pulses after filing.
        pulse = Mathf.Max(0f, pulse - 0.72f * Time.deltaTime);
        if (slotGlow != null)
        {
            float y = Mathf.Max(hovering > 0 ? 4f : 1f, 1f + pulse * 5f);
            slotGlow.localScale = new Vector3(glowBaseScale.x, glowBaseScale.y * y, glowBaseScale.z);
        }
        if (slotLight != null) slotLight.intensity = pulse * slotLightMax;
    }

    private void OnInserted(SelectEnterEventArgs args)
    {
        PersistentPolaroid photo = args.interactableObject.transform.GetComponentInParent<PersistentPolaroid>();
        if (photo == null) return; // not a polaroid, ignore it

        GameProgress.PhotoRecord record = photo.Record;
        if (record != null) StartCoroutine(FileRoutine(photo, record, args.interactableObject));
        else StartCoroutine(RejectRoutine(photo, args.interactableObject));
    }

    // ---------- Story photo: in it goes ----------

    private IEnumerator FileRoutine(PersistentPolaroid photo, GameProgress.PhotoRecord record, IXRSelectInteractable interactable)
    {
        GameObject obj = photo.gameObject;
        yield return null; // XRI doesn't like changing a selection inside its own event
        if (obj == null) yield break;

        // Work out the slot's pose BEFORE letting go (the socket was still easing
        // the photo in from your hand's angle, which is why it kept your rotation).
        SlotPose(obj, interactable, out Vector3 slotPos, out Quaternion slotRot);
        PolaroidPhoto polaroid = Release(obj, interactable);

        // Make sure the picture is kept for the catalog thumbnail, then file it.
        Texture2D tex = polaroid != null ? polaroid.CurrentPhoto : null;
        GameProgress.FileRecord(photo.photoId, tex);

        // Disable it completely so it can't be grabbed on the way in.
        XRGrabInteractable grab = obj.GetComponent<XRGrabInteractable>();
        if (grab != null) grab.enabled = false;
        foreach (Collider c in obj.GetComponentsInChildren<Collider>()) c.enabled = false;
        if (polaroid != null) polaroid.enabled = false;

        // Straighten it into the slot's angle (PhotoAttach), then slide it down into the machine.
        yield return Move(obj.transform, slotPos, slotRot, snapTime);
        Vector3 dir = (slideDirection != null ? slideDirection : transform).forward;
        yield return Move(obj.transform, slotPos + dir * slideDistance, slotRot, slideTime);

        Destroy(obj);
        pulse = 1f;
        PlaySound(fileSound);
        if (terminal != null) terminal.OnFiled(record);
    }

    // ---------- Random photo: back out it comes ----------

    private IEnumerator RejectRoutine(PersistentPolaroid photo, IXRSelectInteractable interactable)
    {
        GameObject obj = photo.gameObject;
        yield return null;
        if (obj == null) yield break;

        // Stop the slot from grabbing it again straight away.
        socket.socketActive = false;
        SlotPose(obj, interactable, out Vector3 slotPos, out Quaternion slotRot);
        PolaroidPhoto polaroid = Release(obj, interactable);

        if (terminal != null) terminal.Flash("NO RECORD FOUND");
        PlaySound(noRecordSound);

        // Lift it back out and leave it hovering there, ready to take.
        yield return Move(obj.transform, slotPos, slotRot, snapTime);
        Vector3 up = (slideDirection != null ? slideDirection : transform).forward * -1f;
        yield return Move(obj.transform, slotPos + up * liftHeight, slotRot, liftTime);
        if (polaroid != null) polaroid.SetGrabbable(true);

        // Turn the slot back on once the photo has been taken away.
        while (obj != null && Vector3.Distance(obj.transform.position, slotPos) < liftHeight + 0.08f)
            yield return null;
        socket.socketActive = true;
    }

    // ---------- Helpers ----------

    // Makes the socket let go, and stops the polaroid reacting to being let go
    // (otherwise it turns its physics back on or tries to stick to a wall).
    private PolaroidPhoto Release(GameObject obj, IXRSelectInteractable interactable)
    {
        if (socket.interactionManager != null && interactable.isSelected)
            socket.interactionManager.CancelInteractableSelection(interactable);

        PolaroidPhoto polaroid = obj.GetComponent<PolaroidPhoto>();
        if (polaroid != null) polaroid.StopAllCoroutines();

        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        return polaroid;
    }

    private IEnumerator Move(Transform t, Vector3 endPos, Quaternion endRot, float time)
    {
        if (t == null) yield break;
        Vector3 startPos = t.position;
        Quaternion startRot = t.rotation;
        float k = 0f;
        while (k < time)
        {
            if (t == null) yield break;
            k += Time.deltaTime;
            float e = Mathf.SmoothStep(0f, 1f, k / time);
            t.position = Vector3.Lerp(startPos, endPos, e);
            t.rotation = Quaternion.Slerp(startRot, endRot, e);
            yield return null;
        }
        if (t != null) { t.position = endPos; t.rotation = endRot; }
    }

    // Where the photo should be so that it sits exactly on the slot's attach point
    // (PhotoAttach), no matter how you were holding it.
    private void SlotPose(GameObject obj, IXRSelectInteractable interactable, out Vector3 pos, out Quaternion rot)
    {
        Transform slotAttach = socket.GetAttachTransform(interactable);
        Transform photoAttach = interactable.GetAttachTransform(socket);
        Transform t = obj.transform;

        // The photo's own grab point, relative to the photo.
        Quaternion localRot = Quaternion.Inverse(t.rotation) * photoAttach.rotation;
        Vector3 localPos = Quaternion.Inverse(t.rotation) * (photoAttach.position - t.position);

        rot = slotAttach.rotation * Quaternion.Inverse(localRot);
        pos = slotAttach.position - rot * localPos;
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null) audioSource.PlayOneShot(clip);
    }
}