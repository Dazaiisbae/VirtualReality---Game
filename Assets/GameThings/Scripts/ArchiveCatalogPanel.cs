using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

// The Archiver's left panel: a catalog entry for whatever cube is on the pad.
// Put this on the left panel (or anywhere) and drag its TextMeshPro into Catalog Text.
// On the PAD's XR Socket Interactor, add a SECOND entry under each event (DYNAMIC section):
//   Select Entered -> ArchiveCatalogPanel.OnCubeInserted
//   Select Exited  -> ArchiveCatalogPanel.OnCubeRemoved
public class ArchiveCatalogPanel : MonoBehaviour
{
    [Header("Setup")]
    public TMP_Text catalogText;
    [Tooltip("How many memories exist in the game right now.")]
    public int totalMemories = 8;

    [Header("Typing")]
    [Tooltip("Letters per second when a new entry appears. 0 = show instantly.")]
    public float typeSpeed = 60f;

    private Coroutine typeRoutine;

    void Start()
    {
        ShowIndex();
    }

    // ---------- Socket events ----------

    public void OnCubeInserted(SelectEnterEventArgs args)
    {
        MemoryCube cube = args.interactableObject.transform.GetComponentInParent<MemoryCube>();
        if (cube == null)
        {
            Show("ENTRY NOT FOUND");
            return;
        }
        ShowEntry(cube);
    }

    public void OnCubeRemoved(SelectExitEventArgs args)
    {
        ShowIndex();
    }

    // ---------- What the panel says ----------

    // No cube: an overview of the whole archive.
    private void ShowIndex()
    {
        Show(
            "ARCHIVE INDEX\n" +
            $"MEMORIES RESTORED: {GameProgress.CompletedCount} / {totalMemories}"
        );
    }

    // Cube on the pad: its catalog entry.
    private void ShowEntry(MemoryCube cube)
    {
        Show(
            $"{cube.memoryName.ToUpper()}\n" +
            $"CATEGORY: {cube.category.ToString().ToUpper()}\n" +
            $"STATUS: {GetStatus(cube)}"
        );
    }

    // UNVIEWED   = never loaded
    // INCOMPLETE = loaded before, not finished
    // RESTORED   = all photos taken
    private string GetStatus(MemoryCube cube)
    {
        if (GameProgress.IsWorldComplete(cube.sceneName)) return "RESTORED";
        if (GameProgress.IsCubeCollected(cube.sceneName)) return "INCOMPLETE";
        return "UNVIEWED";
    }

    // ---------- Typing effect ----------

    private void Show(string message)
    {
        if (catalogText == null) return;
        if (typeRoutine != null) StopCoroutine(typeRoutine);

        catalogText.text = message;

        if (typeSpeed <= 0f)
        {
            catalogText.maxVisibleCharacters = 99999; // show everything
            return;
        }
        typeRoutine = StartCoroutine(TypeIn());
    }

    // Reveals the text one letter at a time, like a terminal printing it.
    private IEnumerator TypeIn()
    {
        catalogText.ForceMeshUpdate();
        int total = catalogText.textInfo.characterCount;
        float shown = 0f;

        catalogText.maxVisibleCharacters = 0;
        while (shown < total)
        {
            shown += typeSpeed * Time.deltaTime;
            catalogText.maxVisibleCharacters = Mathf.Min((int)shown, total);
            yield return null;
        }
        catalogText.maxVisibleCharacters = total;
        typeRoutine = null;
    }
}