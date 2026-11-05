using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

// Put this on every memory cube, next to MemoryCube.
// - The cube you entered a memory with is still plugged into the console when you come back.
// - Once you take it out, it lives on its shelf slot from then on
//   (instead of reappearing in the room it was found in).
// - Completed cubes turn on their "complete" marker.
[RequireComponent(typeof(MemoryCube))]
public class CubePersistence : MonoBehaviour
{
    [Tooltip("The shelf socket (or empty object) where this cube lives once collected. " +
             "Leave empty for cubes that start on the shelf.")]
    public Transform shelfSlot;

    [Header("Completed look (optional)")]
    [Tooltip("Child object turned on when this cube's world is complete (a ring, glow, icon...).")]
    public GameObject completeMarker;
    [Tooltip("Cube renderer whose glow gets brighter when complete. Leave empty to skip.")]
    public Renderer glowRenderer;
    public float completeGlowBoost = 2f;

    MemoryCube cube;

    void Awake()
    {
        cube = GetComponent<MemoryCube>();
        if (completeMarker != null) completeMarker.SetActive(false);
    }

    IEnumerator Start()
    {
        string id = cube.sceneName;

        if (GameProgress.IsWorldComplete(id)) ShowComplete();

        if (GameProgress.IsCubeInConsole(id))
        {
            // Wait a frame so the console's socket is fully set up.
            yield return null;
            PutInConsole();
        }
        else if (shelfSlot != null && GameProgress.IsCubeCollected(id))
        {
            yield return null;
            PutOnShelf();
        }
    }

    // If the shelf slot is an XR Socket Interactor, plug the cube in properly.
    // If it's just an empty object, place the cube there.
    void PutOnShelf()
    {
        var socket = shelfSlot.GetComponent<XRSocketInteractor>();
        var grab = GetComponent<XRGrabInteractable>();

        if (socket != null && grab != null)
        {
            MoveTo(socket.attachTransform != null ? socket.attachTransform : socket.transform);
            if (socket.interactionManager != null && !socket.hasSelection)
                socket.interactionManager.SelectEnter((IXRSelectInteractor)socket, (IXRSelectInteractable)grab);
        }
        else
        {
            MoveTo(shelfSlot);
        }
    }

    void PutInConsole()
    {
        var loader = FindObjectOfType<ConsoleMemoryLoader>();
        var socket = loader != null ? loader.GetComponent<XRSocketInteractor>() : null;
        var grab = GetComponent<XRGrabInteractable>();

        if (socket == null || grab == null)
        {
            Debug.LogWarning("[CubePersistence] Couldn't find the console socket or this cube's XR Grab Interactable. Sending cube to its shelf instead.");
            GameProgress.SetCubeInConsole(null);
            if (shelfSlot != null) PutOnShelf();
            return;
        }

        // Line it up with the socket, then plug it in properly so the console shows it as READY.
        MoveTo(socket.attachTransform != null ? socket.attachTransform : socket.transform);
        if (socket.interactionManager != null)
            socket.interactionManager.SelectEnter((IXRSelectInteractor)socket, (IXRSelectInteractable)grab);
    }

    void MoveTo(Transform target)
    {
        transform.SetPositionAndRotation(target.position, target.rotation);
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    void ShowComplete()
    {
        if (completeMarker != null) completeMarker.SetActive(true);
        if (glowRenderer != null)
        {
            var mat = glowRenderer.material;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", cube.memoryColor * completeGlowBoost);
        }
    }
}