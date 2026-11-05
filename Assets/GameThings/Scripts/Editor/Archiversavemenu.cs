#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Goes in an "Editor" folder. Adds an "Archiver" menu at the top of Unity.
public static class ArchiverSaveMenu
{
    [MenuItem("Archiver/Delete Save Data")]
    static void DeleteSave()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("Stop Play mode before deleting the save.");
            return;
        }
        GameProgress.DeleteAll();
        Debug.Log("Archiver save deleted. Next Play starts fresh.");
    }

    [MenuItem("Archiver/Open Save Folder")]
    static void OpenFolder()
    {
        EditorUtility.RevealInFinder(Application.persistentDataPath);
    }
}
#endif