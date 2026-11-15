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

    // The story line that belongs to a photo, kept by photo ID.
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
        public List<PhotoRecord> photoRecords = new List<PhotoRecord>();
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
            if (data.photoRecords == null) data.photoRecords = new List<PhotoRecord>();
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

    // ---------- Photo records ----------
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

    // FIX: keeps its OWN readable copy of the photo and writes it to disk right away.
    // Before, it only kept a reference to the camera's texture. If that texture got
    // destroyed when the memory scene unloaded, or its format couldn't be saved as a
    // PNG (which can happen after changing platform or render settings), the photo
    // was lost and the archive room silently skipped it.
    public static void StorePhoto(string photoId, Texture2D tex)
    {
        if (string.IsNullOrEmpty(photoId) || tex == null) return;

        // Already have this photo saved, nothing to do
        if (photoCache.TryGetValue(photoId, out var existing) && existing != null && File.Exists(PhotoPath(photoId)))
            return;

        Texture2D copy = MakeReadableCopy(tex);
        if (copy == null) return;

        photoCache[photoId] = copy;
        WritePhoto(photoId, copy);
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

    // FIX: copies any texture (readable or not, any format) into a plain RGBA32 texture
    // by drawing it onto a temporary render texture and reading the pixels back.
    static Texture2D MakeReadableCopy(Texture src)
    {
        try
        {
            RenderTexture rt = RenderTexture.GetTemporary(src.width, src.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;

            Graphics.Blit(src, rt);
            RenderTexture.active = rt;

            var copy = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            copy.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return copy;
        }
        catch (Exception e)
        {
            Debug.LogError("[GameProgress] Couldn't copy photo: " + e.Message);
            return null;
        }
    }

    // FIX: writes one photo, and shows a red error (not a hidden warning) if it fails.
    static void WritePhoto(string photoId, Texture2D tex)
    {
        try
        {
            Directory.CreateDirectory(PhotoFolder);
            File.WriteAllBytes(PhotoPath(photoId), tex.EncodeToPNG());
        }
        catch (Exception e)
        {
            Debug.LogError("[GameProgress] Couldn't write photo " + photoId + ": " + e.Message);
        }
    }

    // ---------- Save / Load ----------
    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(PhotoFolder);

            // Make sure every photo that belongs to something is on disk:
            // loose polaroids AND filed records (kept for the catalog thumbnails).
            var keep = new HashSet<string>();
            void KeepPhoto(string id)
            {
                if (string.IsNullOrEmpty(id)) return;
                keep.Add(id);
                if (!File.Exists(PhotoPath(id)) && photoCache.TryGetValue(id, out var tex) && tex != null)
                    WritePhoto(id, tex); // FIX: one bad photo no longer stops the whole save
            }
            foreach (var r in Data.polaroids) KeepPhoto(r.photoId);
            foreach (var r in Data.photoRecords) if (r.filed) KeepPhoto(r.photoId);

            // Delete photos that no longer belong to anything.
            // (Photos still in the cache get rewritten by the next save if needed.)
            foreach (var file in Directory.GetFiles(PhotoFolder, "*.png"))
                if (!keep.Contains(Path.GetFileNameWithoutExtension(file)))
                    File.Delete(file);

            File.WriteAllText(SaveFile, JsonUtility.ToJson(Data, true));
        }
        catch (Exception e)
        {
            Debug.LogError("[GameProgress] Save failed: " + e.Message); // FIX: red error, easy to spot
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