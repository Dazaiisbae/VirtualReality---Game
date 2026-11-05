using System;
using UnityEngine;

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