using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Holds all progress and saves it to disk.
// Not attached to anything. It works in every scene automatically.
public static class GameProgress
{
    [Serializable]
    public class PolaroidRecord
    {
        public string photoId;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;    // world size, so restored photos match camera photos
        public bool stuck;       // was it pinned / kinematic
        public bool fromMemory;  // taken in a memory world, goes to the tray
    }

    // NEW: the story line that belongs to a photo, kept by photo ID.
    [Serializable]
    public class PhotoRecord
    {
        public string photoId;
        public string sceneName; // the world it was taken in, used to sort it into a catalog section
        public string line;      // the PhotoSpot's line
        public bool filed;       // put into the polaroid machine
    }

    [Serializable]
    class SaveData
    {
        public List<string> completedWorlds = new List<string>();
        public List<string> capturedSpots = new List<string>();
        public List<PolaroidRecord> polaroids = new List<PolaroidRecord>();
        public List<string> collectedCubes = new List<string>();
        public string cubeInConsole;   // cube left plugged in when entering a memory
        public List<PhotoRecord> photoRecords = new List<PhotoRecord>(); // NEW
    }

    static SaveData data;
    static readonly Dictionary<string, Texture2D> photoCache = new Dictionary<string, Texture2D>();

    static string SaveFolder => Path.Combine(Application.persistentDataPath, "ArchiverSave");
    static string SaveFile => Path.Combine(SaveFolder, "progress.json");
    static string PhotoFolder => Path.Combine(SaveFolder, "photos");
    static string PhotoPath(string id) => Path.Combine(PhotoFolder, id + ".png");

    // Clears memory between Play sessions in the editor
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { data = null; photoCache.Clear(); }

    static SaveData Data
    {
        get
        {
            if (data == null) Load();
            // Older saves may be missing newer lists
            if (data.completedWorlds == null) data.completedWorlds = new List<string>();
            if (data.capturedSpots == null) data.capturedSpots = new List<string>();
            if (data.polaroids == null) data.polaroids = new List<PolaroidRecord>();
            if (data.collectedCubes == null) data.collectedCubes = new List<string>();
            if (data.photoRecords == null) data.photoRecords = new List<PhotoRecord>(); // NEW
            return data;
        }
    }

    // World/cube IDs ignore capitals and extra spaces,
    // so "CyberPunk City" and "Cyberpunk City" count as the same world.
    static string Norm(string id) => string.IsNullOrEmpty(id) ? "" : id.Trim().ToLowerInvariant();

    // ---------- Worlds ----------
    public static int CompletedCount => Data.completedWorlds.Count;
    public static bool IsWorldComplete(string id) => Data.completedWorlds.Contains(Norm(id));

    public static void CompleteWorld(string id)
    {
        id = Norm(id);
        if (Data.completedWorlds.Contains(id)) return;
        Data.completedWorlds.Add(id);
        Save();
    }

    // ---------- Memory cubes ----------
    // A cube is "collected" once it has been loaded in the console.
    // From then on it lives on the archive shelf instead of the room it was found in.
    public static bool IsCubeCollected(string id) => Data.collectedCubes.Contains(Norm(id));

    public static void MarkCubeCollected(string id)
    {
        id = Norm(id);
        if (id == "" || Data.collectedCubes.Contains(id)) return;
        Data.collectedCubes.Add(id);
        Save();
    }

    // The cube currently sitting in the console (empty = none).
    public static string CubeInConsole => Data.cubeInConsole;
    public static bool IsCubeInConsole(string id) => !string.IsNullOrEmpty(Data.cubeInConsole) && Data.cubeInConsole == Norm(id);

    public static void SetCubeInConsole(string id)
    {
        id = string.IsNullOrEmpty(id) ? null : Norm(id);
        if (Data.cubeInConsole == id) return;
        Data.cubeInConsole = id;
        Save();
    }

    // ---------- Photo spots ----------
    public static bool IsSpotCaptured(string id) => Data.capturedSpots.Contains(id);

    public static void MarkSpotCaptured(string id)
    {
        if (string.IsNullOrEmpty(id) || Data.capturedSpots.Contains(id)) return;
        Data.capturedSpots.Add(id);
        Save();
    }

    // ---------- Photo records (NEW) ----------
    public static PhotoRecord GetPhotoRecord(string photoId)
    {
        if (string.IsNullOrEmpty(photoId)) return null;
        return Data.photoRecords.Find(r => r.photoId == photoId);
    }

    // Not saved straight away: it's written with the next save, same as the polaroid itself.
    public static void SetPhotoRecord(string photoId, string sceneName, string line)
    {
        if (string.IsNullOrEmpty(photoId)) return;
        PhotoRecord existing = GetPhotoRecord(photoId);
        if (existing != null) { existing.sceneName = sceneName; existing.line = line; return; }
        Data.photoRecords.Add(new PhotoRecord { photoId = photoId, sceneName = sceneName, line = line });
    }

    // Every record put into the machine, in the order they were filed.
    public static List<PhotoRecord> FiledRecords => Data.photoRecords.FindAll(r => r.filed);

    // A story polaroid went into the machine: mark it filed, keep its picture for the
    // catalog thumbnails, and take it off the list of loose polaroids so it doesn't
    // come back on the next load. Saves right away.
    public static void FileRecord(string photoId, Texture2D photo)
    {
        PhotoRecord r = GetPhotoRecord(photoId);
        if (r == null) return;
        r.filed = true;

        // Move it to the end so the catalog shows records in the order they were filed.
        Data.photoRecords.Remove(r);
        Data.photoRecords.Add(r);

        if (photo != null) StorePhoto(photoId, photo);
        Data.polaroids.RemoveAll(p => p.photoId == photoId);
        Save();
    }

    // ---------- Polaroids ----------
    public static IReadOnlyList<PolaroidRecord> Polaroids => Data.polaroids;

    public static void ReplaceAllPolaroids(List<PolaroidRecord> records)
    {
        Data.polaroids = records;
    }

    public static void UpsertPolaroid(PolaroidRecord record)
    {
        int i = Data.polaroids.FindIndex(p => p.photoId == record.photoId);
        if (i >= 0) Data.polaroids[i] = record;
        else Data.polaroids.Add(record);
    }

    public static void StorePhoto(string photoId, Texture2D tex)
    {
        photoCache[photoId] = tex;
    }

    public static Texture2D GetPhoto(string photoId)
    {
        if (photoCache.TryGetValue(photoId, out var cached) && cached != null) return cached;

        string path = PhotoPath(photoId);
        if (!File.Exists(path)) return null;

        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(path));
        photoCache[photoId] = tex;
        return tex;
    }

    // ---------- Save / Load ----------
    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(PhotoFolder);

            // Write any new photos to disk: loose polaroids AND filed records
            // (filed ones are kept so the catalog can show their thumbnails).
            var keep = new HashSet<string>();
            void KeepPhoto(string id)
            {
                if (string.IsNullOrEmpty(id)) return;
                keep.Add(id);
                string path = PhotoPath(id);
                if (!File.Exists(path) && photoCache.TryGetValue(id, out var tex) && tex != null)
                    File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            foreach (var r in Data.polaroids) KeepPhoto(r.photoId);
            foreach (var r in Data.photoRecords) if (r.filed) KeepPhoto(r.photoId);

            // Delete photos that no longer belong to anything
            foreach (var file in Directory.GetFiles(PhotoFolder, "*.png"))
                if (!keep.Contains(Path.GetFileNameWithoutExtension(file)))
                    File.Delete(file);

            File.WriteAllText(SaveFile, JsonUtility.ToJson(Data, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[GameProgress] Save failed: " + e.Message);
        }
    }

    static void Load()
    {
        data = null;
        try
        {
            if (File.Exists(SaveFile))
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SaveFile));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[GameProgress] Load failed, starting fresh: " + e.Message);
        }
        if (data == null) data = new SaveData();
    }

    // Wipes everything. Use this later for New Game.
    public static void DeleteAll()
    {
        data = new SaveData();
        photoCache.Clear();
        if (Directory.Exists(SaveFolder)) Directory.Delete(SaveFolder, true);
    }
}