using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Call StartGame() from the ship PhotoSpot's On Captured event.
// It freezes the ship animation, waits for the polaroid to develop,
// fades to black, then loads the first scene.
public class TitleStartSequence : MonoBehaviour
{
    [Tooltip("Exact name of the scene to load (must be in Build Settings).")]
    public string sceneToLoad = "Medbay";

    [Tooltip("Seconds to wait so the polaroid can develop.")]
    public float delayBeforeFade = 2f;

    [Tooltip("Seconds the fade to black takes.")]
    public float fadeDuration = 1.5f;

    [Tooltip("Optional: CanvasGroup on a black Image in front of the camera.")]
    public CanvasGroup fadeGroup;

    [Tooltip("Optional: the ship's Animator, so the ship freezes when the photo is taken.")]
    public Animator shipAnimator;

    bool started;

    void Start()
    {
        if (fadeGroup != null)
        {
            fadeGroup.alpha = 0f;
            fadeGroup.blocksRaycasts = false;
        }
    }

    public void StartGame()
    {
        if (started) return;   // ignore extra photos
        started = true;
        StartCoroutine(Sequence());
    }

    IEnumerator Sequence()
    {
        if (shipAnimator != null) shipAnimator.speed = 0f;

        yield return new WaitForSeconds(delayBeforeFade);

        if (fadeGroup != null)
        {
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                fadeGroup.alpha = Mathf.Clamp01(t / fadeDuration);
                yield return null;
            }
            fadeGroup.alpha = 1f;
        }

        SceneManager.LoadScene(sceneToLoad);
    }
}