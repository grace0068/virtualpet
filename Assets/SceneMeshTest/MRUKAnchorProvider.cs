using System.Collections.Generic;
using UnityEngine;
using Meta.XR.MRUtilityKit;

public class MRUKAnchorProvider : MonoBehaviour, IAnchorProvider
{
    [Tooltip("true면 현재 방만, false면 씬 전체에서 앵커를 찾음")]
    public bool currentRoomOnly = false;

    public List<MRUKAnchor> GetAnchors()
    {
        var list = new List<MRUKAnchor>();

        // 1) current room만 쓰고 싶으면 먼저 시도
        if (currentRoomOnly && MRUK.Instance != null)
        {
            var cur = MRUK.Instance.GetCurrentRoom();
            if (cur != null)
                list.AddRange(cur.GetComponentsInChildren<MRUKAnchor>(true));
        }

        // 2) 그래도 없으면 전체에서 스캔(가장 robust)
        if (list.Count == 0)
            list.AddRange(FindObjectsOfType<MRUKAnchor>(true));

        return list;
    }
}
