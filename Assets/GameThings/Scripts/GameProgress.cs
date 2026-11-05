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

    [Serializable]
    class SaveData
    {
        public List<string> completedWorlds = new List<string>();
        public List<string> capturedSpots = new List<string>();
        public List<PolaroidRecord> polaroids = new List<PolaroidRecord>();
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
        get { if (data == null) Load(); return data; }
    }

    // ---------- Worlds ----------
    public static int CompletedCount => Data.completedWorlds.Count;
    public static bool IsWorldComplete(string id) => Data.completedWorlds.Contains(id);

    public static void CompleteWorld(string id)
    {
        if (Data.completedWorlds.Contains(id)) return;
        Data.completedWorlds.Add(id);
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

            // Write any new photos to disk
            var keep = new HashSet<string>();
            foreach (var r in Data.polaroids)
            {
                keep.Add(r.photoId);
                string path = PhotoPath(r.photoId);
                if (!File.Exists(path) && photoCache.TryGetValue(r.photoId, out var tex) && tex != null)
                    File.WriteAllBytes(path, tex.EncodeToPNG());
            }

            // Delete photos that no longer belong to any polaroid
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