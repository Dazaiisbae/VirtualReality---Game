using System.Collections.Generic;
using UnityEngine;

// Put one of these in the archive room scene.
public class ArchiveRoomPersistence : MonoBehaviour
{
    [Tooltip("The same polaroid prefab your camera spawns.")]
    public PersistentPolaroid polaroidPrefab;

    [Tooltip("Where photos from memory worlds land. Stacks along this object's green (up) axis.")]
    public Transform trayPoint;

    public float stackSpacing = 0.005f;

    void Start()
    {
        RestorePolaroids();
    }

    void RestorePolaroids()
    {
        // FIX: say clearly what's wrong instead of failing silently
        if (polaroidPrefab == null)
        {
            Debug.LogError("[ArchiveRoomPersistence] Polaroid Prefab isn't assigned in the Inspector.");
            return;
        }
        if (trayPoint == null)
            Debug.LogWarning("[ArchiveRoomPersistence] Tray Point isn't assigned. Memory photos will appear where they were taken instead.");

        int stack = 0;
        foreach (var r in GameProgress.Polaroids)
        {
            var tex = GameProgress.GetPhoto(r.photoId);
            if (tex == null)
            {
                Debug.LogWarning("[ArchiveRoomPersistence] Photo missing on restore: " + r.photoId);
                continue;
            }

            // FIX: a missing tray point used to crash this loop, so no memory photos came back
            bool toTray = r.fromMemory && trayPoint != null;
            Vector3 pos = toTray ? trayPoint.position + trayPoint.up * stackSpacing * stack++ : r.position;
            Quaternion rot = toTray ? trayPoint.rotation : r.rotation;

            var p = Instantiate(polaroidPrefab, pos, rot);

            // Match the size it had in the world (camera photos inherit the camera's scale).
            if (r.scale != Vector3.zero) p.transform.localScale = r.scale;

            // Tray photos are held in place until grabbed, like pinned ones.
            p.Restore(r.photoId, tex, r.fromMemory || r.stuck);
        }
    }

    // Call this right before loading a memory world.
    public void SaveRoom()
    {
        var records = new List<GameProgress.PolaroidRecord>();
        foreach (var p in FindObjectsOfType<PersistentPolaroid>())
        {
            var rec = p.ToRecord(false);
            if (rec != null) records.Add(rec);
        }
        GameProgress.ReplaceAllPolaroids(records);
        GameProgress.Save();
    }

    // Saves if the headset sleeps or the game closes
    void OnApplicationPause(bool paused) { if (paused) SaveRoom(); }
    void OnApplicationQuit() { SaveRoom(); }
}