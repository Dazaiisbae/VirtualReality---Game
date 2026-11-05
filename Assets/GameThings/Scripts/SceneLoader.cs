using UnityEngine;
using UnityEngine.SceneManagement;

// Put this on any object (the exit cube is fine).
// Hook LoadTargetScene into ExitCube's On Exit event.
public class SceneLoader : MonoBehaviour
{
    [Tooltip("Exact scene name, as listed in File > Build Settings.")]
    public string targetScene = "ArchiveRoom";

    public void LoadTargetScene()
    {
        SceneManager.LoadScene(targetScene);
    }

    // Lets you pick a scene by name straight from a UnityEvent.
    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
}