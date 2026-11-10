using UnityEngine;
using UnityEngine.XR;

// Put this on the camera root, next to PhotoCamera.
// Pulses the camera's LED, plays a soft tick, and buzzes the holding hand
// as you get closer to the next photo spot. Goes steady when the shot is framed.
// No XR Interaction Toolkit types used: "camera is out" = far from the holster,
// "holding hand" = whichever controller is closest.
public class PhotoHintIndicator : MonoBehaviour
{
    public enum Tier { Off, Warm, Hot, Framed }

    [Header("References")]
    [Tooltip("Found automatically if left empty. Used for its Viewfinder Camera.")]
    public PhotoCamera photoCamera;
    [Tooltip("The HipHolster object. Hints are off while the camera sits in it.")]
    public Transform holster;
    [Tooltip("Camera closer than this to the holster counts as holstered.")]
    public float holsteredDistance = 0.25f;
    [Tooltip("Left/Right Controller objects under your XR Origin. Used to pick which hand buzzes.")]
    public Transform leftController;
    public Transform rightController;
    [Tooltip("A hand must be this close to the camera to get haptics.")]
    public float holdRange = 0.35f;

    [Header("Ranges")]
    [Tooltip("Hot = within the spot's Max Distance. Warm starts this many meters beyond that.")]
    public float warmExtra = 4f;
    [Tooltip("Extra distance before dropping a tier, so it doesn't flicker at the edge.")]
    public float exitBuffer = 0.5f;
    [Tooltip("How often to check distance when far away.")]
    public float checkInterval = 0.25f;
    [Tooltip("How often to check when close (so the framed glow feels responsive).")]
    public float fastCheckInterval = 0.1f;

    [Header("Pulse Timing (seconds between pulses)")]
    public float warmInterval = 1.4f;
    public float hotIntervalFar = 0.7f;
    public float hotIntervalNear = 0.25f;

    [Header("LED")]
    [Tooltip("Small mesh on the camera. Its material needs Emission turned on.")]
    public Renderer ledRenderer;
    public Color ledColor = new Color(0f, 0.9f, 1f);
    [Tooltip("Glow while the camera is out but nothing is near.")]
    public float idleBrightness = 0.05f;
    public float pulseBrightness = 2f;
    public float framedBrightness = 1.4f;
    [Tooltip("How fast each pulse fades out.")]
    public float pulseFade = 0.2f;

    [Header("Viewfinder")]
    [Tooltip("A glowing border object around the viewfinder screen. Shown only when framed.")]
    public GameObject framedGlow;

    [Header("Sound")]
    [Tooltip("Keep this low-pitched and soft.")]
    public AudioClip tickSound;
    [Range(0f, 1f)] public float tickVolume = 0.25f;
    [Tooltip("Optional. Plays once when the shot becomes framed.")]
    public AudioClip framedSound;

    [Header("Haptics")]
    [Range(0f, 1f)] public float warmHaptic = 0.08f;
    [Range(0f, 1f)] public float hotHaptic = 0.25f;
    public float hapticDuration = 0.05f;

    [Header("Debug")]
    public bool debugLog = false;

    public Tier CurrentTier { get; private set; }

    private PhotoSpot target;
    private float currentDist;
    private bool cameraOut;
    private float nextCheck, nextPulse, pulseGlow;
    private AudioSource audioSource;
    private MaterialPropertyBlock mpb;
    private static readonly int EmissionID = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        if (photoCamera == null) photoCamera = GetComponent<PhotoCamera>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f;
            audioSource.playOnAwake = false;
        }
        mpb = new MaterialPropertyBlock();
        if (framedGlow != null) framedGlow.SetActive(false);
    }

    void OnDisable()
    {
        CurrentTier = Tier.Off;
        if (framedGlow != null) framedGlow.SetActive(false);
        ApplyLed(0f);
    }

    void Update()
    {
        if (Time.time >= nextCheck)
        {
            UpdateTier();
            nextCheck = Time.time + (CurrentTier >= Tier.Hot ? fastCheckInterval : checkInterval);
        }

        if ((CurrentTier == Tier.Warm || CurrentTier == Tier.Hot) && Time.time >= nextPulse)
        {
            Pulse();
            nextPulse = Time.time + CurrentInterval();
        }

        pulseGlow = Mathf.MoveTowards(pulseGlow, 0f, pulseBrightness * Time.deltaTime / Mathf.Max(0.01f, pulseFade));

        float brightness;
        if (!cameraOut) brightness = 0f;
        else if (CurrentTier == Tier.Framed) brightness = framedBrightness;
        else brightness = idleBrightness + pulseGlow;
        ApplyLed(brightness);
    }

    void UpdateTier()
    {
        cameraOut = holster == null || Vector3.Distance(transform.position, holster.position) > holsteredDistance;

        Tier newTier = Tier.Off;
        target = null;

        if (cameraOut && PhotoSpotManager.Instance != null)
        {
            target = PhotoSpotManager.Instance.GetHintTarget(transform.position);
            if (target != null)
            {
                currentDist = Vector3.Distance(transform.position, target.transform.position);
                float hotRange = target.maxDistance + (CurrentTier >= Tier.Hot ? exitBuffer : 0f);
                float warmRange = target.maxDistance + warmExtra + (CurrentTier >= Tier.Warm ? exitBuffer : 0f);

                if (currentDist <= hotRange) newTier = Tier.Hot;
                else if (currentDist <= warmRange) newTier = Tier.Warm;

                Camera cam = photoCamera != null ? photoCamera.viewfinderCamera : null;
                if (newTier == Tier.Hot && cam != null && target.IsFramedBy(cam, out _, false))
                    newTier = Tier.Framed;
            }
        }

        if (newTier != CurrentTier) SetTier(newTier);
    }

    void SetTier(Tier t)
    {
        if (debugLog) Debug.Log("[PhotoHint] " + CurrentTier + " -> " + t + (target != null ? " (" + target.spotId + ")" : ""));
        Tier old = CurrentTier;
        CurrentTier = t;

        if (framedGlow != null) framedGlow.SetActive(t == Tier.Framed);

        if (t == Tier.Framed)
        {
            if (framedSound != null) audioSource.PlayOneShot(framedSound, tickVolume);
            Haptic(hotHaptic, 0.12f);
        }

        // Pulse right away when first entering range.
        if ((t == Tier.Warm || t == Tier.Hot) && old < Tier.Warm) nextPulse = Time.time;
    }

    float CurrentInterval()
    {
        if (CurrentTier == Tier.Warm || target == null) return warmInterval;
        float t = Mathf.Clamp01(currentDist / Mathf.Max(0.01f, target.maxDistance));
        return Mathf.Lerp(hotIntervalNear, hotIntervalFar, t);
    }

    void Pulse()
    {
        pulseGlow = pulseBrightness;
        if (tickSound != null) audioSource.PlayOneShot(tickSound, tickVolume);
        Haptic(CurrentTier == Tier.Hot ? hotHaptic : warmHaptic, hapticDuration);
    }

    void Haptic(float amplitude, float duration)
    {
        if (amplitude <= 0f) return;

        float l = leftController != null ? Vector3.Distance(leftController.position, transform.position) : float.MaxValue;
        float r = rightController != null ? Vector3.Distance(rightController.position, transform.position) : float.MaxValue;
        if (Mathf.Min(l, r) > holdRange) return;

        InputDevice device = InputDevices.GetDeviceAtXRNode(l < r ? XRNode.LeftHand : XRNode.RightHand);
        if (device.isValid && device.TryGetHapticCapabilities(out HapticCapabilities caps) && caps.supportsImpulse)
            device.SendHapticImpulse(0, amplitude, duration);
    }

    void ApplyLed(float brightness)
    {
        if (ledRenderer == null) return;
        ledRenderer.GetPropertyBlock(mpb);
        mpb.SetColor(EmissionID, ledColor * brightness);
        ledRenderer.SetPropertyBlock(mpb);
    }
}