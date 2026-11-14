using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

// Hold the trigger on this object for holdTime seconds to quit.
// Needs an XR Simple Interactable (and a collider) on the same object.
[RequireComponent(typeof(XRSimpleInteractable))]
public class HoldToQuit : MonoBehaviour
{
    [Tooltip("Seconds the player has to hold before the game quits.")]
    public float holdTime = 2f;

    [Tooltip("Optional: an Image set to Filled, shows hold progress.")]
    public Image progressFill;

    XRSimpleInteractable interactable;
    bool holding;
    float timer;
    bool quitting;

    void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        if (progressFill != null) progressFill.fillAmount = 0f;
    }

    void OnEnable()
    {
        interactable.selectEntered.AddListener(OnPress);
        interactable.selectExited.AddListener(OnRelease);
    }

    void OnDisable()
    {
        interactable.selectEntered.RemoveListener(OnPress);
        interactable.selectExited.RemoveListener(OnRelease);
    }

    void OnPress(SelectEnterEventArgs args) { holding = true; timer = 0f; }
    void OnRelease(SelectExitEventArgs args) { holding = false; timer = 0f; }

    void Update()
    {
        if (quitting) return;

        if (holding)
        {
            timer += Time.deltaTime;
            if (timer >= holdTime) Quit();
        }

        if (progressFill != null)
            progressFill.fillAmount = Mathf.Clamp01(timer / holdTime);
    }

    void Quit()
    {
        quitting = true;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}