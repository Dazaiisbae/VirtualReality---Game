using UnityEngine;

// Sliding sci-fi door that opens when the player's headset gets close.
// Attach to an empty "Door" parent object. Assign the left/right panel meshes as children.
public class SlidingDoor : MonoBehaviour
{
    [System.Serializable]
    public class IndicatorPart
    {
        public Renderer renderer;
        [Tooltip("Which material slot is the green one. 0 if the object only has one material.")]
        public int materialIndex = 0;
    }

    [Header("Panels")]
    public Transform leftPanel;
    public Transform rightPanel;           // leave empty for a single-panel door
    public float slideDistance = 0.8f;     // how far each panel moves (local X)
    public float slideSpeed = 2.5f;        // meters per second
    public Vector3 slideAxis = Vector3.right; // direction the RIGHT panel moves (local space). Left panel moves opposite.
    public bool flipDirection = false;     // tick if panels move into each other instead of apart

    [Header("Proximity")]
    public bool autoOpen = true;           // false = only opens via Open()/Toggle() (button)
    public float openRadius = 2.0f;        // opens when player is closer than this
    public float closeRadius = 2.6f;       // closes when player is farther than this (prevents flicker)

    [Header("Lock")]
    public bool locked = false;
    public IndicatorPart[] indicators;     // every green part that should turn red when locked
    public Color unlockedColor = Color.green;
    public Color lockedColor = Color.red;

    [Header("Audio (optional)")]
    public AudioSource audioSource;
    public AudioClip openSound;
    public AudioClip closeSound;
    public AudioClip lockedSound;

    Vector3 leftClosed, rightClosed, leftOpen, rightOpen;
    bool isOpen;
    bool lastLocked;
    Transform player;

    void Start()
    {
        if (Camera.main != null) player = Camera.main.transform;

        Vector3 dir = slideAxis.normalized * (flipDirection ? -1f : 1f);
        if (leftPanel)
        {
            leftClosed = leftPanel.localPosition;
            leftOpen = leftClosed - dir * slideDistance;
        }
        if (rightPanel)
        {
            rightClosed = rightPanel.localPosition;
            rightOpen = rightClosed + dir * slideDistance;
        }
        lastLocked = locked;
        UpdateIndicator();
    }

    void Update()
    {
        // Catch the Locked checkbox being changed in the Inspector during Play mode
        if (locked != lastLocked) SetLocked(locked);

        if (autoOpen && player != null)
        {
            // Ignore height so it works the same sitting or standing
            Vector3 a = player.position; a.y = 0;
            Vector3 b = transform.position; b.y = 0;
            float dist = Vector3.Distance(a, b);

            if (!isOpen && dist < openRadius) Open();
            else if (isOpen && dist > closeRadius) Close();
        }

        float step = slideSpeed * Time.deltaTime;
        if (leftPanel)
            leftPanel.localPosition = Vector3.MoveTowards(leftPanel.localPosition, isOpen ? leftOpen : leftClosed, step);
        if (rightPanel)
            rightPanel.localPosition = Vector3.MoveTowards(rightPanel.localPosition, isOpen ? rightOpen : rightClosed, step);
    }

    public void Open()
    {
        if (isOpen) return;
        if (locked) { Play(lockedSound); return; }
        isOpen = true;
        Play(openSound);
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        Play(closeSound);
    }

    public void Toggle()
    {
        if (isOpen) Close(); else Open();
    }

    public void SetLocked(bool value)
    {
        locked = value;
        lastLocked = value;
        if (locked) Close();
        UpdateIndicator();
    }

    public void Lock() { SetLocked(true); }
    public void Unlock() { SetLocked(false); }
    public void ToggleLock() { SetLocked(!locked); }

    void UpdateIndicator()
    {
        if (indicators == null) return;
        Color c = locked ? lockedColor : unlockedColor;

        foreach (var part in indicators)
        {
            if (part == null || part.renderer == null) continue;
            Material[] mats = part.renderer.materials; // per-object copies, so other doors aren't affected
            if (part.materialIndex < 0 || part.materialIndex >= mats.Length) continue;

            Material m = mats[part.materialIndex];
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); // URP
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);         // Built-in
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * 2f);
            }
        }
    }

    void Play(AudioClip clip)
    {
        if (audioSource && clip) audioSource.PlayOneShot(clip);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, openRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, closeRadius);
    }
}