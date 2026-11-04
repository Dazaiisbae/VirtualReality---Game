using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Put this on the title screen cube (alongside an XR Simple Interactable).
// Wire the XR Simple Interactable's events to the public methods below:
//   Hover Entered  -> TitleCubeLoader.OnHoverEnter
//   Hover Exited   -> TitleCubeLoader.OnHoverExit
//   Select Entered -> TitleCubeLoader.OnClicked
public class TitleCubeLoader : MonoBehaviour
{
    [Header("Scene")]
    [Tooltip("Must match the scene name exactly, and the scene must be in Build Settings.")]
    public string sceneToLoad = "Archive Room";
    [Tooltip("Short pause so the player sees the click register before the load.")]
    public float loadDelay = 0.5f;

    [Header("Hover Effect")]
    public Renderer cubeRenderer;
    public Color hoverGlowColor = new Color(0.3f, 0.8f, 1f);
    public float hoverGlowIntensity = 2f;
    public float hoverScaleMultiplier = 1.1f;
    public float scaleSpeed = 8f;

    [Header("Idle Motion (optional)")]
    public float spinSpeed = 20f;

    private Material mat;
    private Color originalEmission;
    private Vector3 baseScale;
    private Vector3 targetScale;
    private bool isLoading;

    void Start()
    {
        if (cubeRenderer == null) cubeRenderer = GetComponentInChildren<Renderer>();

        // .material makes an instance so other objects sharing the material aren't affected
        mat = cubeRenderer.material;
        mat.EnableKeyword("_EMISSION");
        originalEmission = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;

        baseScale = transform.localScale;
        targetScale = baseScale;
    }

    void Update()
    {
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * scaleSpeed);
    }

    public void OnHoverEnter()
    {
        if (isLoading) return;
        mat.SetColor("_EmissionColor", hoverGlowColor * hoverGlowIntensity);
        targetScale = baseScale * hoverScaleMultiplier;
    }

    public void OnHoverExit()
    {
        if (isLoading) return;
        mat.SetColor("_EmissionColor", originalEmission);
        targetScale = baseScale;
    }

    public void OnClicked()
    {
        if (isLoading) return;
        isLoading = true;
        StartCoroutine(LoadSceneRoutine());
    }

    private IEnumerator LoadSceneRoutine()
    {
        // Brief bright flash as click feedback
        mat.SetColor("_EmissionColor", Color.white * hoverGlowIntensity * 1.5f);
        yield return new WaitForSeconds(loadDelay);

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneToLoad);
        while (!op.isDone) yield return null;
    }
}
