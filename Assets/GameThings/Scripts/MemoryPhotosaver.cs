using UnityEngine;

// Saves every polaroid in the current scene so it survives a scene change.
// Call MemoryPhotoSaver.SaveAll(); right before loading another scene.
//
// - In a memory world: each photo is saved as "from memory", so it lands
//   on the tray when you get back to the ship.
// - In the archive room: it just runs ArchiveRoomPersistence.SaveRoom().
// - Anywhere else (like the title screen): nothing to save, so it does nothing harmful.
public static class MemoryPhotoSaver
{
    public static void SaveAll()
    {
        // In the archive room, let the room's own saver handle it
        var room = Object.FindObjectOfType<ArchiveRoomPersistence>();
        if (room != null)
        {
            room.SaveRoom();
            return;
        }

        // Otherwise we're in a memory world
        int saved = 0;
        foreach (var p in Object.FindObjectsOfType<PersistentPolaroid>())
        {
            var rec = p.ToRecord(true);   // true = taken in a memory, goes to the tray
            if (rec == null) continue;    // polaroid with no photo on it yet
            GameProgress.UpsertPolaroid(rec);
            saved++;
        }
        GameProgress.Save();
        Debug.Log("[MemoryPhotoSaver] Saved " + saved + " photo(s) before leaving the memory.");
    }
}