using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit; // SelectEnterEventArgs lives here in XRI 2.x and 3.x

// Put this on the console's socket object (the one with XR Socket Interactor).
// Wire the socket's Interactor Events (pick from the DYNAMIC section of the dropdown):
//   Select Entered -> ConsoleMemoryLoader.OnCubeInserted
//   Select Exited  -> ConsoleMemoryLoader.OnCubeRemoved
public class ConsoleMemoryLoader : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Seconds between the cube seating and the scene loading. Pulling the cube out during this cancels the load.")]
    public float loadDelay = 1.5f;

    [Header("Audio")]
    [Tooltip("AudioSource on the console (set Spatial Blend to 1 for 3D sound).")]
    public AudioSource audioSource;
    [Tooltip("Plays when a valid memory cube is inserted.")]
    public AudioClip startupSound;
    [Tooltip("Plays when a cube has no scene, a wrong scene name, or no MemoryCube component.")]
    public AudioClip errorSound;

    [Header("Console Screen (optional)")]
    [Tooltip("The TV screen renderer. Its emission flashes on insert.")]
    public Renderer screenRenderer;
    public float screenGlowIntensity = 3f;
    public Color errorColor = Color.red;
    [Tooltip("How many times the screen blinks red on an error.")]
    public int errorBlinks = 3;
    public float errorBlinkSpeed = 0.12f;

    private Material screenMat;
    private Color screenOriginalEmission;
    private Coroutine loadRoutine;
    private Coroutine errorRoutine;
    private bool isLoading;

    // Remembers which memory was loaded, so the archive room can react when you return.
    public static string LastLoadedMemory { get; private set; }

    void Start()
    {
        if (screenRenderer != null)
        {
            screenMat = screenRenderer.material;
            screenMat.EnableKeyword("_EMISSION");
            screenOriginalEmission = screenMat.HasProperty("_EmissionColor")
                ? screenMat.GetColor("_EmissionColor") : Color.black;
        }
    }

    public void OnCubeInserted(SelectEnterEventArgs args)
    {
        if (isLoading) return;

        Transform inserted = args.interactableObject.transform;

        // The collider may be on a child, so search up from the inserted object.
        MemoryCube cube = inserted.GetComponentInParent<MemoryCube>();
        if (cube == null)
        {
            RejectCube($"'{inserted.name}' has no MemoryCube component.");
            return;
        }

        if (string.IsNullOrWhiteSpace(cube.sceneName))
        {
            RejectCube($"{cube.memoryName} has no scene assigned.");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(cube.sceneName))
        {
            RejectCube($"Scene '{cube.sceneName}' for {cube.memoryName} isn't in Build Settings or the name is wrong.");
            return;
        }

        loadRoutine = StartCoroutine(LoadMemoryRoutine(cube));
    }

    public void OnCubeRemoved(SelectExitEventArgs args)
    {
        // Player pulled the cube out before the load started: cancel it.
        if (loadRoutine != null && !isLoading)
        {
            StopCoroutine(loadRoutine);
            loadRoutine = null;
            if (audioSource != null) audioSource.Stop();
            SetScreenGlow(screenOriginalEmission);
        }
    }

    private void RejectCube(string reason)
    {
        // A warning (yellow), not an error, since this is expected for unfinished cubes.
        Debug.LogWarning($"Console rejected cube: {reason}");
        PlaySound(errorSound);

        if (errorRoutine != null) StopCoroutine(errorRoutine);
        errorRoutine = StartCoroutine(ErrorBlinkRoutine());
    }

    private IEnumerator LoadMemoryRoutine(MemoryCube cube)
    {
        PlaySound(startupSound);
        SetScreenGlow(cube.memoryColor * screenGlowIntensity);

        yield return new WaitForSeconds(loadDelay);

        isLoading = true;
        LastLoadedMemory = cube.memoryName;
        Debug.Log($"Loading {cube.memoryName} -> {cube.sceneName}");

        AsyncOperation op = SceneManager.LoadSceneAsync(cube.sceneName);
        if (op == null)
        {
            // Safety net: should never hit this thanks to the checks above.
            isLoading = false;
            RejectCube($"Unity couldn't start loading '{cube.sceneName}'.");
            yield break;
        }
        while (!op.isDone) yield return null;
    }

    private IEnumerator ErrorBlinkRoutine()
    {
        for (int i = 0; i < errorBlinks; i++)
        {
            SetScreenGlow(errorColor * screenGlowIntensity);
            yield return new WaitForSeconds(errorBlinkSpeed);
            SetScreenGlow(screenOriginalEmission);
            yield return new WaitForSeconds(errorBlinkSpeed);
        }
        errorRoutine = null;
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource == null || clip == null) return;
        audioSource.Stop();
        audioSource.PlayOneShot(clip);
    }

    private void SetScreenGlow(Color c)
    {
        if (screenMat != null) screenMat.SetColor("_EmissionColor", c);
    }
}
