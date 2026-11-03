using UnityEngine;
using TMPro;

// Shows the Archiver's line in front of the player when a photo spot is captured.
// Letters fly in from different depths and settle into place, then drift back out and fade.
public class ArchiverText : MonoBehaviour
{
    public static ArchiverText Instance;

    [Header("Setup")]
    public TextMeshPro textDisplay;
    public float distance = 1.2f;       // how far in front of the player
    public float height = -0.15f;       // a little below eye level
    public LayerMask wallMask = ~0;     // uncheck the camera/hands layer

    [Header("Animation")]
    public float letterDelay = 0.03f;   // time between each letter starting
    public float flyTime = 0.4f;        // how long each letter takes to arrive
    public float maxDepth = 0.5f;       // how far in front/behind letters start
    public float holdTime = 4f;         // how long the full line stays up
    public float fadeTime = 0.6f;       // how long it takes to leave

    [Header("Colors")]
    public Color dimColor = new Color(0.1f, 0.4f, 0.5f);
    public Color mainColor = new Color(0.45f, 1f, 1f);

    Transform head;
    float startTime;
    float[] depths;
    TMP_MeshInfo[] original;
    bool showing;

    void Awake()
    {
        Instance = this;
        head = Camera.main.transform;
        textDisplay.gameObject.SetActive(false);
    }

    // PhotoCamera calls this. (polaroid isn't used here, it's just passed along.)
    public void Show(PhotoSpot spot, PolaroidPhoto polaroid = null)
    {
        if (string.IsNullOrEmpty(spot.archiverLine)) return;

        // Place it in front of the player, or closer if a wall is in the way
        Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
        float d = distance;
        if (Physics.Raycast(head.position, forward, out RaycastHit hit, distance, wallMask))
            d = Mathf.Max(hit.distance - 0.15f, 0.4f);

        textDisplay.transform.position = head.position + forward * d + Vector3.up * height;
        textDisplay.transform.rotation = Quaternion.LookRotation(forward);

        // Set the text and save each letter's resting position
        textDisplay.gameObject.SetActive(true);
        textDisplay.text = spot.archiverLine;
        textDisplay.ForceMeshUpdate();
        original = textDisplay.textInfo.CopyMeshInfoVertexData();

        // Give every letter a random starting depth
        depths = new float[textDisplay.textInfo.characterCount];
        for (int i = 0; i < depths.Length; i++)
            depths[i] = Random.Range(-maxDepth, maxDepth);

        startTime = Time.time;
        showing = true;
    }

    void Update()
    {
        if (!showing) return;

        float t = Time.time - startTime;
        TMP_TextInfo info = textDisplay.textInfo;

        // fade goes 0 -> 1 once the hold time is over
        float revealEnd = info.characterCount * letterDelay + flyTime;
        float fade = Mathf.Clamp01((t - revealEnd - holdTime) / fadeTime);

        for (int i = 0; i < info.characterCount; i++)
        {
            TMP_CharacterInfo c = info.characterInfo[i];
            if (!c.isVisible) continue; // skip spaces

            // p goes 0 -> 1 as this letter arrives (each letter starts a bit later)
            float p = Mathf.Clamp01((t - i * letterDelay) / flyTime);
            float ease = 1f - (1f - p) * (1f - p); // fast start, slow landing

            // Start at its random depth, land at 0, then drift back out when fading
            float z = depths[i] * (1f - ease) + depths[i] * fade;

            Color col = Color.Lerp(dimColor, mainColor, ease);
            col.a = ease * (1f - fade);
            if (p < 1f && Random.value < 0.3f) col.a *= 0.4f; // flicker while arriving

            // Each letter is 4 corners (vertices). Move and color all 4.
            int m = c.materialReferenceIndex;
            int v = c.vertexIndex;
            for (int k = 0; k < 4; k++)
            {
                info.meshInfo[m].vertices[v + k] = original[m].vertices[v + k] + new Vector3(0f, 0f, z);
                info.meshInfo[m].colors32[v + k] = col;
            }
        }

        textDisplay.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);

        if (fade >= 1f)
        {
            showing = false;
            textDisplay.gameObject.SetActive(false);
        }
    }
}