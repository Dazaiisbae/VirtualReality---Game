using UnityEngine;

// Put one of these in each memory world scene.
// The world counts as complete when that scene's PhotoSpotManager is complete.
public class MemoryWorldPersistence : MonoBehaviour
{
    [Tooltip("Unique name for this world, e.g. CyberpunkCity")]
    public string worldID = "CyberpunkCity";

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