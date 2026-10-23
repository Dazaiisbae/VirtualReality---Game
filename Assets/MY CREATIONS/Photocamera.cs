using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Put this on the root of your camera model.
// Wire the shutter button's XR Simple Interactable -> Select Entered -> PhotoCamera.TakePhoto
public class PhotoCamera : MonoBehaviour
{
    [Header("Capture")]
    [Tooltip("The RenderTexture your viewfinder camera renders into.")]
    public RenderTexture viewfinderTexture;
    [Tooltip("Seconds before you can take another photo.")]
    public float cooldown = 1.5f;

    [Header("Polaroid")]
    [Tooltip("Your polaroid frame prefab (needs the PolaroidPhoto script).")]
    public PolaroidPhoto polaroidPrefab;
    [Tooltip("Empty child where the photo comes out. Its blue arrow (Z) points the way it slides.")]
    public Transform ejectPoint;
    public float ejectDistance = 0.08f;
    public float ejectTime = 0.8f;

    [Header("Flash (optional)")]
    [Tooltip("A Light on the front of the camera. Leave it turned off; the script flashes it.")]
    public Light flashLight;
    public float flashTime = 0.08f;

    [Header("Sounds (just drag clips in)")]
    public AudioClip shutterSound;
    public AudioClip ejectSound;

    [Header("Events")]
    [Tooltip("Fires after each photo. Useful later for story-beat photo spots.")]
    public UnityEvent onPhotoTaken;

    private AudioSource audioSource;
    private float nextPhotoTime;
    private Collider[] cameraColliders;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f;
            audioSource.playOnAwake = false;
        }
        if (flashLight != null) flashLight.enabled = false;

        // Grabbed now, before any polaroids exist, so it's only the camera's own colliders.
        cameraColliders = GetComponentsInChildren<Collider>(true);
    }

    public void TakePhoto()
    {
        if (Time.time < nextPhotoTime) return;
        if (viewfinderTexture == null || polaroidPrefab == null || ejectPoint == null)
        {
            Debug.LogWarning("PhotoCamera is missing the Viewfinder Texture, Polaroid Prefab, or Eject Point.");
            return;
        }
        nextPhotoTime = Time.time + cooldown;
        StartCoroutine(PhotoRoutine());
    }

    private IEnumerator PhotoRoutine()
    {
        // Wait for the frame to finish so we capture the latest image.
        yield return new WaitForEndOfFrame();
        Texture2D photo = CaptureViewfinder();

        if (shutterSound != null) audioSource.PlayOneShot(shutterSound);
        if (flashLight != null) StartCoroutine(FlashRoutine());

        // Spawn the polaroid inside the camera and slide it out.
        PolaroidPhoto polaroid = Instantiate(polaroidPrefab, ejectPoint.position, ejectPoint.rotation, ejectPoint);
        polaroid.SetPhoto(photo);
        polaroid.SetGrabbable(false);

        if (ejectSound != null) audioSource.PlayOneShot(ejectSound);

        Vector3 start = Vector3.zero;
        Vector3 end = Vector3.forward * ejectDistance;
        float t = 0f;
        while (t < ejectTime)
        {
            t += Time.deltaTime;
            float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / ejectTime), 2f);
            if (polaroid == null) yield break;
            polaroid.transform.localPosition = Vector3.Lerp(start, end, eased);
            yield return null;
        }

        // It sticks out of the camera until you grab it.
        polaroid.SetGrabbable(true, cameraColliders);
        polaroid.StartDeveloping();

        onPhotoTaken.Invoke();
    }

    private Texture2D CaptureViewfinder()
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = viewfinderTexture;

        // Match the RenderTexture's color space, otherwise the photo comes out darker
        // (or washed out) compared to the viewfinder.
        bool linear = !viewfinderTexture.sRGB;
        Texture2D photo = new Texture2D(viewfinderTexture.width, viewfinderTexture.height, TextureFormat.RGB24, false, linear);
        photo.ReadPixels(new Rect(0, 0, viewfinderTexture.width, viewfinderTexture.height), 0, 0);
        photo.Apply();

        RenderTexture.active = previous;
        return photo;
    }

    private IEnumerator FlashRoutine()
    {
        flashLight.enabled = true;
        yield return new WaitForSeconds(flashTime);
        flashLight.enabled = false;
    }
}
