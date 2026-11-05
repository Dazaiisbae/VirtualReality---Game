using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// Put this on an empty GameObject placed at the thing the player should photograph.
/// Works in any scene: the ship, or any memory world.
/// No trigger colliders needed: when a photo is taken, PhotoCamera asks
/// "which spots are in frame?" and this script checks it with math.
public class PhotoSpot : MonoBehaviour
{
    // Every enabled spot in the loaded scene(s) registers itself here.
    public static readonly List<PhotoSpot> Active = new List<PhotoSpot>();

    [Header("Identity")]
    public string spotId = "spot_01";
    [Tooltip("1, 2, 3 = story order. 0 = can be captured anytime.")]
    public int storyOrder = 1;
    [Tooltip("What the Archiver prints when this spot is captured. Machine catalog style.")]
    [TextArea(2, 4)] public string archiverLine = "ENTRY 0001 // SUBJECT LOGGED.";

    [Header("Framing Rules")]
    [Tooltip("Farthest the camera can be and still count.")]
    public float maxDistance = 6f;
    [Tooltip("Closest the camera can be (stops shoving the lens into it).")]
    public float minDistance = 0.3f;
    [Tooltip("How far from the photo edges the subject must be. 0.15 = inner 70% of the frame.")]
    [Range(0f, 0.45f)] public float screenMargin = 0.15f;
    [Tooltip("180 = any angle. Lower = must shoot from the front (uses this object's blue Z arrow).")]
    [Range(0f, 180f)] public float maxViewAngle = 180f;
    public bool requireLineOfSight = true;
    [Tooltip("Exclude Player / hands / polaroid layers here so they don't block the shot.")]
    public LayerMask occlusionMask = ~0;
    [Tooltip("Optional extra points that must ALSO be in frame (for big subjects).")]
    public Transform[] extraFramePoints;

    [Header("Events")]
    public UnityEvent onCaptured;

    [Header("Saving")]
    [Tooltip("When loading back in, fire On Captured again so doors wired to it re-open. " +
             "Turn OFF if On Captured also plays sounds or effects you don't want repeated.")]
    public bool replayOnCapturedWhenLoaded = true;

    [Header("Debug")]
    public bool debugLog = true;

    public bool Captured { get; private set; }
    public bool RestoredFromSave { get; private set; }

    /// Unique save name, made automatically from the scene and object name.
    /// Keep object names unique within a scene (e.g. Spot_Console, Spot_Window).
    public string SaveKey => gameObject.scene.name + "/" + gameObject.name;

    void OnEnable()  { if (!Active.Contains(this)) Active.Add(this); }
    void OnDisable() { Active.Remove(this); }

    void Awake()
    {
        // Runs before every Start, so the manager counts restored spots correctly.
        if (GameProgress.IsSpotCaptured(SaveKey))
        {
            Captured = true;
            RestoredFromSave = true;
            if (debugLog) Debug.Log("[PhotoSpot] " + spotId + " restored from save");
        }
    }

    IEnumerator Start()
    {
        foreach (var other in Active)
            if (other != this && other.gameObject.scene == gameObject.scene && other.gameObject.name == gameObject.name)
                Debug.LogWarning("[PhotoSpot] Two spots are named '" + gameObject.name + "'. Rename one or they'll share a save.");

        // Wait one frame so doors finish their own Start before we unlock them.
        yield return null;
        if (RestoredFromSave && replayOnCapturedWhenLoaded) onCaptured.Invoke();
    }

    /// Call this from PhotoCamera at the moment of capture.
    /// Returns the spot that got captured, or null if none.
    public static PhotoSpot TryCapture(Camera cam)
    {
        PhotoSpot best = null;
        float bestScore = float.MaxValue;

        foreach (var spot in Active)
        {
            if (spot.Captured) continue;
            if (PhotoSpotManager.Instance != null && !PhotoSpotManager.Instance.CanCapture(spot)) continue;
            if (!spot.IsFramedBy(cam, out float centerDist)) continue;

            // If two spots are in frame, the one closest to the center wins.
            if (centerDist < bestScore) { bestScore = centerDist; best = spot; }
        }

        if (best != null) best.MarkCaptured();
        return best;
    }

    public bool IsFramedBy(Camera cam, out float centerDist)
    {
        centerDist = float.MaxValue;
        Vector3 camPos = cam.transform.position;
        Vector3 target = transform.position;

        // Distance
        float dist = Vector3.Distance(camPos, target);
        if (dist > maxDistance || dist < minDistance) return Fail("out of range (" + dist.ToString("F1") + "m)");

        // In frame
        if (!InFrame(cam, target, out Vector3 vp)) return Fail("not in frame");
        foreach (var p in extraFramePoints)
            if (p != null && !InFrame(cam, p.position, out _)) return Fail("subject cut off");

        // Shooting angle
        if (maxViewAngle < 180f)
        {
            float angle = Vector3.Angle(transform.forward, camPos - target);
            if (angle > maxViewAngle) return Fail("bad angle (" + angle.ToString("F0") + "°)");
        }

        // Something in the way
        if (requireLineOfSight &&
            Physics.Linecast(camPos, target, out RaycastHit hit, occlusionMask, QueryTriggerInteraction.Ignore) &&
            !hit.transform.IsChildOf(transform))
            return Fail("blocked by " + hit.collider.name);

        centerDist = new Vector2(vp.x - 0.5f, vp.y - 0.5f).magnitude;
        return true;
    }

    bool InFrame(Camera cam, Vector3 worldPos, out Vector3 vp)
    {
        vp = cam.WorldToViewportPoint(worldPos);
        return vp.z > 0f &&
               vp.x > screenMargin && vp.x < 1f - screenMargin &&
               vp.y > screenMargin && vp.y < 1f - screenMargin;
    }

    bool Fail(string reason)
    {
        if (debugLog) Debug.Log("[PhotoSpot] " + spotId + " missed: " + reason);
        return false;
    }

    void MarkCaptured()
    {
        Captured = true;
        GameProgress.MarkSpotCaptured(SaveKey);
        if (debugLog) Debug.Log("[PhotoSpot] " + spotId + " CAPTURED");
        onCaptured.Invoke();
        if (PhotoSpotManager.Instance != null) PhotoSpotManager.Instance.NotifyCaptured(this);
    }

    public void ResetSpot() { Captured = false; RestoredFromSave = false; }

    void OnDrawGizmos()
    {
        Gizmos.color = Captured ? Color.green : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.15f);
        Gizmos.color = new Color(0f, 1f, 1f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, maxDistance);
        if (maxViewAngle < 180f)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, transform.forward * 1f);
        }
    }
}