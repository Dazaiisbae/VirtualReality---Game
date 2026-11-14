using UnityEngine;

// Wipes all save data so the next playthrough starts fresh.
// Two ways to use it:
//   1. Put it in your ending/credits scene with Clear On Start checked.
//   2. Or call ClearSave() from an event, like the final PhotoSpot's On Captured.
public class ClearSaveData : MonoBehaviour
{
    [Tooltip("Clear the save as soon as this scene loads.")]
    public bool clearOnStart = false;

    bool cleared;

    void Start()
    {
        if (clearOnStart) ClearSave();
    }

    public void ClearSave()
    {
        if (cleared) return;
        cleared = true;

        GameProgress.DeleteAll();
        PlayerPrefs.Save();   // makes sure the wipe is written to disk right away

        Debug.Log("[ClearSaveData] Save data cleared.");
    }
}