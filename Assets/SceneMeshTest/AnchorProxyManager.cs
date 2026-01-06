using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Meta.XR.MRUtilityKit;

public class AnchorProxyManager : MonoBehaviour
{
    public GameObject proxyPrefab;     // AnchorProxy prefab
    public float proxyScale = 0.2f;

    private readonly Dictionary<MRUKAnchor, GameObject> map = new();

    IEnumerator Start()
    {
        // MRUK room 준비 대기
        while (MRUK.Instance == null) yield return null;
        while (MRUK.Instance.GetCurrentRoom() == null) yield return null;

        var room = MRUK.Instance.GetCurrentRoom();

        foreach (var a in room.Anchors)
        {
            if (a == null) continue;

            var go = Instantiate(proxyPrefab);
            go.name = $"Proxy_{a.Label}_{a.GetInstanceID()}";
            go.transform.localScale = Vector3.one * proxyScale;

            // 꼭 PetVision 레이어로(프리팹에서 지정했어도 안전하게)
            go.layer = LayerMask.NameToLayer("PetVision");

            map[a] = go;
        }
    }

    void Update()
    {
        // anchor center 위치로 계속 갱신 (룸 업데이트가 없다면 사실 한 번만 해도 됨)
        foreach (var kv in map)
        {
            var a = kv.Key;
            var go = kv.Value;
            if (a == null || go == null) continue;
            go.transform.position = a.GetAnchorCenter();
        }
    }
}
