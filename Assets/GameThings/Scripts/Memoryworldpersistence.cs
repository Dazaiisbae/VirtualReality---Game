using UnityEngine;

// Put one of these in each memory world scene.
// The world counts as complete when that scene's PhotoSpotManager is complete.
public class MemoryWorldPersistence : MonoBehaviour
{
    // The world's ID is its scene name, the same name the memory cube loads.
    // That way cubes and doors can check completion without typing any IDs.
    string worldID => gameObject.scene.name;

    bool completed;

    void Start()
    {
        completed = GameProgress.IsWorldComplete(worldID);
        InvokeRepeating(nameof(CheckComplete), 0.5f, 0.5f);
    }

    void CheckComplete()
    {
        if (completed) return;
        var manager = PhotoSpotManager.Instance;
        if (manager == null || !manager.Complete) return;

        completed = true;
        GameProgress.CompleteWorld(worldID);
        Debug.Log($"[MemoryWorld] {worldID} completed.");
    }

    // Call this right before loading the archive room.
    public void ExitWorld()
    {
        CheckComplete();
        foreach (var p in FindObjectsOfType<PersistentPolaroid>())
        {
            var rec = p.ToRecord(true);
            if (rec != null) GameProgress.UpsertPolaroid(rec);
        }
        GameProgress.Save();
    }

    // Saves photos if the game closes while inside the world
    void OnApplicationPause(bool paused) { if (paused) ExitWorld(); }
    void OnApplicationQuit() { ExitWorld(); }
}