using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

// Put this on the console's socket object (the one with XR Socket Interactor).
// Socket Interactor Events (DYNAMIC section of the dropdown):
//   Select Entered -> ConsoleMemoryLoader.OnCubeInserted
//   Select Exited  -> ConsoleMemoryLoader.OnCubeRemoved
// Green button's XR Simple Interactable -> Select Entered:
//   ConsoleMemoryLoader.OnButtonPressed
public class ConsoleMemoryLoader : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Minimum seconds the LOADING MEMORY screen stays up, even if the scene loads faster.")]
    public float minLoadingTime = 3f;
    public float dotInterval = 0.4f;

    [Header("Screen")]
    public Renderer screenRenderer;
    public float screenGlowIntensity = 3f;
    [Tooltip("Optional TextMeshPro text on the screen.")]
    public TMP_Text screenText;
    public string idleText = "INSERT MEMORY";

    [Header("Error Feedback")]
    public Color errorColor = Color.red;
    public int errorBlinks = 3;
    public float errorBlinkSpeed = 0.15f;

    [Header("Sounds (just drag clips in)")]
    [Tooltip("Leave empty: one is created automatically.")]
    public AudioSource audioSource;
    public AudioClip startupSound;
    public AudioClip errorSound;
    [Tooltip("Optional click when the button is pressed.")]
    public AudioClip buttonSound;

    [Header("Button (optional)")]
    public Transform buttonVisual;
    public float pressDepth = 0.01f;

    public static string LastLoadedMemory { get; private set; }

    private Material screenMat;
    private Color screenOriginalEmission;
    private MemoryCube insertedCube;
    private bool isLoading;
    private Vector3 buttonRestPos;
    private Coroutine errorRoutine;
    private Coroutine loadRoutine;
    private bool sceneLoadStarted;

    void Awake()
    {
        // No need to set up an AudioSource by hand.
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f;
            audioSource.playOnAwake = false;
        }
    }

    void Start()
    {
        if (screenRenderer != null)
        {
            screenMat = screenRenderer.material;
            screenMat.EnableKeyword("_EMISSION");
            screenOriginalEmission = screenMat.HasProperty("_EmissionColor")
                ? screenMat.GetColor("_EmissionColor") : Color.black;
        }
        if (buttonVisual != null) buttonRestPos = buttonVisual.localPosition;
        SetText(idleText);
    }

    // ---------- Socket ----------

    public void OnCubeInserted(SelectEnterEventArgs args)
    {
        if (isLoading) return;

        MemoryCube cube = args.interactableObject.transform.GetComponentInParent<MemoryCube>();
        string problem = CheckCube(cube, args.interactableObject.transform.name);
        if (problem != null)
        {
            insertedCube = null;
            Reject(problem, "MEMORY UNREADABLE");
            return;
        }

        insertedCube = cube;
        StopError();
        SetScreenGlow(cube.memoryColor * screenGlowIntensity);
        SetText($"{cube.memoryName}\nREADY");
    }

    public void OnCubeRemoved(SelectExitEventArgs args)
    {
        // The real scene load has already begun (a split second at the very end): too late to cancel.
        if (sceneLoadStarted) return;

        if (isLoading)
        {
            // Cube pulled out during the loading screen: cancel and stay in the archive room.
            if (loadRoutine != null) StopCoroutine(loadRoutine);
            loadRoutine = null;
            isLoading = false;
            insertedCube = null;
            if (audioSource != null) audioSource.Stop();
            Reject("Cube removed during loading.", "LOADING INTERRUPTED");
            return;
        }

        insertedCube = null;
        StopError();
        SetScreenGlow(screenOriginalEmission);
        SetText(idleText);
    }

    // ---------- Button ----------

    public void OnButtonPressed()
    {
        if (buttonVisual != null) StartCoroutine(ButtonPressAnim());
        if (isLoading) return;

        PlaySound(buttonSound);

        if (insertedCube == null)
        {
            Reject("Button pressed with no valid memory in the socket.", "NO MEMORY INSERTED");
            return;
        }

        loadRoutine = StartCoroutine(LoadMemoryRoutine(insertedCube));
    }

    // ---------- Loading ----------

    private IEnumerator LoadMemoryRoutine(MemoryCube cube)
    {
        isLoading = true;
        PlaySound(startupSound);

        // Loading screen phase. Pulling the cube out during this cancels everything.
        float elapsed = 0f, dotTimer = 0f;
        int dots = 0;
        while (elapsed < minLoadingTime)
        {
            elapsed += Time.deltaTime;
            dotTimer += Time.deltaTime;
            if (dotTimer >= dotInterval) { dotTimer = 0f; dots = (dots + 1) % 4; }

            int percent = Mathf.RoundToInt(Mathf.Clamp01(elapsed / minLoadingTime) * 99f);
            SetText($"LOADING MEMORY{new string('.', dots)}\n{cube.memoryName}\n{percent}%");
            yield return null;
        }

        SetText($"LOADING MEMORY...\n{cube.memoryName}\n100%");

        // Fade/glitch transition goes here if you have one, e.g.:
        // yield return fader.FadeOut();

        // Only now start the real load. Unity can't cancel a scene load once it begins,
        // which is why it waits until the loading screen is finished.
        sceneLoadStarted = true;
        LastLoadedMemory = cube.memoryName;
        Debug.Log($"Loading {cube.memoryName} -> {cube.sceneName}");

        AsyncOperation op = SceneManager.LoadSceneAsync(cube.sceneName);
        if (op == null)
        {
            sceneLoadStarted = false;
            isLoading = false;
            Reject($"Unity couldn't start loading '{cube.sceneName}'.", "MEMORY UNREADABLE");
            yield break;
        }
        while (!op.isDone) yield return null;
    }

    // ---------- Errors ----------

    private string CheckCube(MemoryCube cube, string objectName)
    {
        if (cube == null) return $"'{objectName}' has no MemoryCube component.";
        if (string.IsNullOrWhiteSpace(cube.sceneName)) return $"{cube.memoryName} has no sceneName set.";
        if (!Application.CanStreamedLevelBeLoaded(cube.sceneName))
            return $"Scene '{cube.sceneName}' for {cube.memoryName} isn't in Build Settings or the name is wrong.";
        return null;
    }

    private void Reject(string reason, string screenMessage)
    {
        Debug.LogWarning($"Console rejected: {reason}");
        PlaySound(errorSound);
        StopError();
        errorRoutine = StartCoroutine(ErrorBlinkRoutine(screenMessage));
    }

    private void StopError()
    {
        if (errorRoutine != null) StopCoroutine(errorRoutine);
        errorRoutine = null;
    }

    private IEnumerator ErrorBlinkRoutine(string message)
    {
        SetText(message);
        for (int i = 0; i < errorBlinks; i++)
        {
            SetScreenGlow(errorColor * screenGlowIntensity);
            yield return new WaitForSeconds(errorBlinkSpeed);
            SetScreenGlow(screenOriginalEmission);
            yield return new WaitForSeconds(errorBlinkSpeed);
        }

        // Go back to whatever the screen should show now.
        if (insertedCube != null)
        {
            SetScreenGlow(insertedCube.memoryColor * screenGlowIntensity);
            SetText($"{insertedCube.memoryName}\nREADY");
        }
        else SetText(idleText);
        errorRoutine = null;
    }

    // ---------- Helpers ----------

    private IEnumerator ButtonPressAnim()
    {
        buttonVisual.localPosition = buttonRestPos + Vector3.down * pressDepth;
        yield return new WaitForSeconds(0.15f);
        buttonVisual.localPosition = buttonRestPos;
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource == null || clip == null) return;
        audioSource.Stop();
        audioSource.PlayOneShot(clip);
    }

    private void SetText(string t)
    {
        if (screenText != null) screenText.text = t;
    }

    private void SetScreenGlow(Color c)
    {
        if (screenMat != null) screenMat.SetColor("_EmissionColor", c);
    }
}