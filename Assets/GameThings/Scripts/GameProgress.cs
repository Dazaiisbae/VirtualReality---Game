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
        public List<string> collectedCubes = new List<string>();
        public string cubeInConsole;   // cube left plugged in when entering a memory
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