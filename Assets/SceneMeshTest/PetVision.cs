using System.Collections.Generic;
using UnityEngine;
using Meta.XR.MRUtilityKit;

public class PetVision : MonoBehaviour
{
    public Camera petCamera;
    public float maxDistance = 8f;

    public List<MRUKAnchor> VisibleAnchors { get; private set; } = new();

    private MRUKRoom room;

    async void Start()
    {
        // 간단 대기
        while (MRUK.Instance == null) await System.Threading.Tasks.Task.Yield();
        while (MRUK.Instance.GetCurrentRoom() == null) await System.Threading.Tasks.Task.Yield();
        room = MRUK.Instance.GetCurrentRoom();
    }

    void LateUpdate()
    {
        if (petCamera == null || room == null) return;

        VisibleAnchors.Clear();

        foreach (var a in room.Anchors)
        {
            if (a == null) continue;

            Vector3 p = a.GetAnchorCenter();
            float dist = Vector3.Distance(petCamera.transform.position, p);
            if (dist > maxDistance) continue;

            Vector3 v = petCamera.WorldToViewportPoint(p);
            if (v.z <= 0f) continue;
            if (v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f) continue;

            VisibleAnchors.Add(a);
        }

        // 여기서 VisibleAnchors를 LLM 입력으로 변환하면 됨
        // Debug.Log($"Pet sees {VisibleAnchors.Count} anchors");
    }
}
