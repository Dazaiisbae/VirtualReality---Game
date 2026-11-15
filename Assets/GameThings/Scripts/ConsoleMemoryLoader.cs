using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

// Put this on the console's socket object (the one with XR Socket Interactor).
// Socket Interactor Events (DYNAMIC section of the dropdown):
//   Select Entered -> ConsoleMemoryLoader.OnCubeInserted
//   Select Exited  -> ConsoleMemoryLoader.OnCubeRemoved
// Platform (MemoryPlatform) -> On Activated:
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
    [Tooltip("Desk panel text: shows the cube's status.")]
    public TMP_Text screenText;
    public string idleText = "INSERT MEMORY";

    // NEW: second screen on the platform
    [Header("Platform Screen")]
    [Tooltip("Platform screen text: shows where you're going, the hold progress, and loading.")]
    public TMP_Text platformText;
    public string platformIdleText = "NO MEMORY LOADED";
    public int linkBarLength = 5;

    [Header("Error Feedback")]
    public Color errorColor = Color.red;
    public int errorBlinks = 3;
    public float errorBlinkSpeed = 0.15f;

    [Header("Sounds (just drag clips in)")]
    [Tooltip("Leave empty: one is created automatically.")]
    public AudioSource audioSource;
    public AudioClip startupSound;
    public AudioClip errorSound;
    [Tooltip("Optional click when the platform activates.")]
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
        SetPlatformText(platformIdleText); // NEW
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
        SetPlatformText($"{cube.memoryName}\nSTAND TO ENTER"); // NEW
    }

    public void OnCubeRemoved(SelectExitEventArgs args)
    {
        // The real scene load has already begun (a split second at the very end): too late to cancel.
        if (sceneLoadStarted) return;

        // Player pulled the cube out: it's no longer saved as "in the console".
        MemoryCube removed = args.interactableObject.transform.GetComponentInParent<MemoryCube>();
        if (removed != null && GameProgress.IsCubeInConsole(removed.sceneName))
            GameProgress.SetCubeInConsole(null);

        SetPlatformText(platformIdleText); // NEW: no cube = nowhere to go

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

    // ---------- Platform ----------

    // Called by MemoryPlatform when the hold finishes. (Name kept so old wiring still works.)
    public void OnButtonPressed()
    {
        if (buttonVisual != null) StartCoroutine(ButtonPressAnim());
        if (isLoading) return;

        PlaySound(buttonSound);

        if (insertedCube == null)
        {
            // NEW: player is on the platform, so show this error there
            Reject("Platform activated with no valid memory in the socket.", "NO MEMORY INSERTED", true);
            return;
        }

        loadRoutine = StartCoroutine(LoadMemoryRoutine(insertedCube));
    }

    // NEW: MemoryPlatform calls this every frame while the player stands on it (p goes 0 -> 1).
    public void ShowLinkProgress(float p)
    {
        if (insertedCube == null || isLoading || errorRoutine != null) return;

        int filled = Mathf.RoundToInt(Mathf.Clamp01(p) * linkBarLength);
        string bar = new string('#', filled) + new string('-', linkBarLength - filled);
        SetPlatformText($"{insertedCube.memoryName}\nLINKING [{bar}]");
    }

    // NEW: MemoryPlatform calls this when the player steps off before it finishes.
    public void CancelLinkProgress()
    {
        if (insertedCube == null || isLoading || errorRoutine != null) return;
        SetPlatformText($"{insertedCube.memoryName}\nSTAND TO ENTER");
    }

    // ---------- Loading ----------

    private IEnumerator LoadMemoryRoutine(MemoryCube cube)
    {
        isLoading = true;
        PlaySound(startupSound);
        SetText($"{cube.memoryName}\nIN USE"); // NEW: desk just says it's busy

        // Loading screen phase. Pulling the cube out during this cancels everything.
        float elapsed = 0f, dotTimer = 0f;
        int dots = 0;
        while (elapsed < minLoadingTime)
        {
            elapsed += Time.deltaTime;
            dotTimer += Time.deltaTime;
            if (dotTimer >= dotInterval) { dotTimer = 0f; dots = (dots + 1) % 4; }

            int percent = Mathf.RoundToInt(Mathf.Clamp01(elapsed / minLoadingTime) * 99f);
            SetPlatformText($"LOADING MEMORY{new string('.', dots)}\n{cube.memoryName}\n{percent}%"); // NEW: platform
            yield return null;
        }

        SetPlatformText($"LOADING MEMORY...\n{cube.memoryName}\n100%"); // NEW: platform

        // Fade/glitch transition goes here if you have one, e.g.:
        // yield return fader.FadeOut();

        // Only now start the real load. Unity can't cancel a scene load once it begins,
        // which is why it waits until the loading screen is finished.
        sceneLoadStarted = true;
        LastLoadedMemory = cube.memoryName;

        // Save the archive room (polaroid positions, pinned photos) before leaving.
        // Done here, after the cancel window, so a cancelled load doesn't save early.
        GameProgress.MarkCubeCollected(cube.sceneName);
        GameProgress.SetCubeInConsole(cube.sceneName);
        SaveArchiveRoom();

        Debug.Log($"Loading {cube.memoryName} -> {cube.sceneName}");

        AsyncOperation op = SceneManager.LoadSceneAsync(cube.sceneName);
        if (op == null)
        {
            sceneLoadStarted = false;
            isLoading = false;
            Reject($"Unity couldn't start loading '{cube.sceneName}'.", "MEMORY UNREADABLE", true); // NEW: on platform
            yield break;
        }
        while (!op.isDone) yield return null;
    }

    private void SaveArchiveRoom()
    {
        var room = FindObjectOfType<ArchiveRoomPersistence>();
        if (room != null) room.SaveRoom();
        else Debug.LogWarning("[Console] No ArchiveRoomPersistence in this scene, so the archive room wasn't saved.");
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

    // NEW: onPlatform = show the message on the platform screen instead of the desk
    private void Reject(string reason, string screenMessage, bool onPlatform = false)
    {
        Debug.LogWarning($"Console rejected: {reason}");
        PlaySound(errorSound);
        StopError();
        errorRoutine = StartCoroutine(ErrorBlinkRoutine(screenMessage, onPlatform));
    }

    private void StopError()
    {
        if (errorRoutine != null) StopCoroutine(errorRoutine);
        errorRoutine = null;
    }

    private IEnumerator ErrorBlinkRoutine(string message, bool onPlatform)
    {
        if (onPlatform) SetPlatformText(message);
        else SetText(message);

        for (int i = 0; i < errorBlinks; i++)
        {
            SetScreenGlow(errorColor * screenGlowIntensity);
            yield return new WaitForSeconds(errorBlinkSpeed);
            SetScreenGlow(screenOriginalEmission);
            yield return new WaitForSeconds(errorBlinkSpeed);
        }

        // Go back to whatever both screens should show now.
        if (insertedCube != null)
        {
            SetScreenGlow(insertedCube.memoryColor * screenGlowIntensity);
            SetText($"{insertedCube.memoryName}\nREADY");
            SetPlatformText($"{insertedCube.memoryName}\nSTAND TO ENTER");
        }
        else
        {
            SetText(idleText);
            SetPlatformText(platformIdleText);
        }
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

    // NEW
    private void SetPlatformText(string t)
    {
        if (platformText != null) platformText.text = t;
    }

    private void SetScreenGlow(Color c)
    {
        if (screenMat != null) screenMat.SetColor("_EmissionColor", c);
    }
}