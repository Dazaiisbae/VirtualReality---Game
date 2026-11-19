using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Put this on your polaroid prefab, next to PolaroidPhoto. No setup needed.
[RequireComponent(typeof(PolaroidPhoto))]
public class PersistentPolaroid : MonoBehaviour
{
    [HideInInspector] public string photoId;
    [HideInInspector] public bool restored;

    PolaroidPhoto polaroid;

    void Awake()
    {
        polaroid = GetComponent<PolaroidPhoto>();
    }

    // Rebuild from a save: developed, and pinned or loose.
    public void Restore(string id, Texture2D tex, bool stuck)
    {
        photoId = id;
        restored = true;
        polaroid.RestoreFromSave(tex, stuck);
    }

    // NEW: PhotoCamera calls this when a story photo prints.
    // Saves the spot's line and the world it was taken in under this photo's ID,
    // so it survives saving and scene changes.
    public void SetRecord(string line)
    {
        if (string.IsNullOrEmpty(line)) return;
        if (string.IsNullOrEmpty(photoId))
            photoId = Guid.NewGuid().ToString("N");

        GameProgress.SetPhotoRecord(photoId, SceneManager.GetActiveScene().name, line);
    }

    // NEW: this photo's record, or null if it's a random shot.
    public GameProgress.PhotoRecord Record =>
        string.IsNullOrEmpty(photoId) ? null : GameProgress.GetPhotoRecord(photoId);

    public GameProgress.PolaroidRecord ToRecord(bool fromMemory)
    {
        var tex = polaroid.CurrentPhoto;
        if (tex == null) return null; // no photo on it yet

        if (string.IsNullOrEmpty(photoId))
            photoId = Guid.NewGuid().ToString("N");

        GameProgress.StorePhoto(photoId, tex);

        return new GameProgress.PolaroidRecord
        {
            photoId = photoId,
            position = transform.position,
            rotation = transform.rotation,
            scale = transform.lossyScale,
            stuck = polaroid.IsStuck,
            fromMemory = fromMemory
        };
    }
}