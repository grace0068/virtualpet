using System.Linq;
using UnityEngine;
using Meta.XR.MRUtilityKit;

public class TableMarkerDemo : MonoBehaviour
{
    public GameObject markerPrefab;

    void Awake()
    {
        // Subscribe to MRUK's scene-loaded event
        MRUK.Instance.SceneLoadedEvent.AddListener(OnSceneLoaded);
    }

    private void OnSceneLoaded()
    {
        var room = MRUK.Instance.GetCurrentRoom();
        if (room == null)
        {
            Debug.LogWarning("No MRUK room found.");
            return;
        }
    }
}
