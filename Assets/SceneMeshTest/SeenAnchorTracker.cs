using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Meta.XR.MRUtilityKit;

public class SeenAnchorTracker : MonoBehaviour
{
    [Header("Camera used to determine what is 'seen'")]
    public Camera playerCamera;          // 비워두면 자동으로 Camera.main 사용

    [Header("View constraints")]
    public float maxViewDistance = 10f;  // 이 거리까지만 본 것으로 인정 (원하면 조절)
    
    // 저장할 정보 구조체
    [System.Serializable]
    public class SeenAnchorRecord
    {
        public MRUKAnchor anchor;
        public MRUKAnchor.SceneLabels labels;

        public Vector3 firstSeenPosition;
        public Vector3 lastSeenPosition;

        public float firstSeenTime;
        public float lastSeenTime;

        public int seenCount;
    }

    // 앵커 → 관찰 정보
    private Dictionary<MRUKAnchor, SeenAnchorRecord> _seen = new Dictionary<MRUKAnchor, SeenAnchorRecord>();
    private List<MRUKAnchor> _anchors = new List<MRUKAnchor>();

    private bool _ready = false;

    private IEnumerator Start()
    {
        Debug.Log("[SeenAnchorTracker] Start");

        // 카메라 자동 설정 (비워져 있으면 MainCamera 사용)
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        while (MRUK.Instance == null)
        {
            Debug.Log("[SeenAnchorTracker] Waiting for MRUK.Instance...");
            yield return null;
        }

        while (MRUK.Instance.GetCurrentRoom() == null)
        {
            Debug.Log("[SeenAnchorTracker] Waiting for MRUK room...");
            yield return null;
        }

        var room = MRUK.Instance.GetCurrentRoom();
        _anchors = room.Anchors.ToList();
        _ready = true;

        Debug.Log($"[SeenAnchorTracker] Room ready, anchors: {_anchors.Count}");
    }

    private void Update()
    {
        if (!_ready || playerCamera == null)
            return;

        foreach (var anchor in _anchors)
        {
            if (anchor == null)
                continue;

            Vector3 center = anchor.GetAnchorCenter();
            if (IsInView(playerCamera, center, maxViewDistance))
            {
                MarkSeen(anchor, center);
            }
        }
    }

    // 이 world 좌표가 카메라 프러스텀 안에 있고 거리 제한 안이면 "본 것"으로 처리
    private bool IsInView(Camera cam, Vector3 worldPos, float maxDistance)
    {
        Vector3 viewport = cam.WorldToViewportPoint(worldPos);

        // 앞쪽에 있는지(z>0)
        if (viewport.z <= 0f)
            return false;

        // 화면 안에 들어오는지 (0~1 사이)
        if (viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
            return false;

        // 거리 제한
        float dist = Vector3.Distance(cam.transform.position, worldPos);
        if (dist > maxDistance)
            return false;

        return true;
    }

    // anchor를 본 적이 있을 때 기록/업데이트
    private void MarkSeen(MRUKAnchor anchor, Vector3 pos)
    {
        SeenAnchorRecord record;
        if (!_seen.TryGetValue(anchor, out record))
        {
            record = new SeenAnchorRecord
            {
                anchor = anchor,
                labels = anchor.Label,
                firstSeenPosition = pos,
                lastSeenPosition = pos,
                firstSeenTime = Time.time,
                lastSeenTime = Time.time,
                seenCount = 1
            };
            _seen.Add(anchor, record);

            Debug.Log($"[SeenAnchorTracker] First time seeing {HumanReadableLabel(anchor.Label)} at {pos}");
        }
        else
        {
            record.lastSeenPosition = pos;
            record.lastSeenTime = Time.time;
            record.seenCount += 1;
        }
    }

    // LLM에 보낼 때 사용할 요약 문장들을 만들어주는 함수 (예시)
    public List<string> BuildSeenSummarySentences()
    {
        List<string> sentences = new List<string>();

        foreach (var kv in _seen.Values)
        {
            var r = kv;
            string name = HumanReadableLabel(r.labels);
            Vector3 p = r.lastSeenPosition;

            string sentence = $"{name} was seen at approximately ({p.x:F2}, {p.y:F2}, {p.z:F2}) " +
                              $"and observed {r.seenCount} times.";
            sentences.Add(sentence);
        }

        return sentences;
    }

    // 라벨 enum을 자연어로
    private string HumanReadableLabel(MRUKAnchor.SceneLabels labels)
    {
        string s = labels.ToString();
        var parts = s.Split(',');
        string first = parts[0].Trim();

        switch (first)
        {
            case "GLOBAL_MESH":      return "the room";
            case "FLOOR":            return "the floor";
            case "CEILING":          return "the ceiling";
            case "WALL_FACE":        return "the wall";
            case "INVISIBLE_WALL_FACE": return "the invisible wall";
            case "TABLE":            return "the table";
            case "COUCH":            return "the couch";
            case "BED":              return "the bed";
            case "STORAGE":          return "the storage";
            case "LAMP":             return "the lamp";
            case "PLANT":            return "the plant";
            case "SCREEN":           return "the screen";
            case "DOOR_FRAME":       return "the door";
            case "WINDOW_FRAME":     return "the window";
            default:
                return "the " + first.ToLower().Replace("_", " ");
        }
    }

    // 디버그용: 지금까지 본 앵커들 로그로 뿌리기
    [ContextMenu("Debug Dump Seen Anchors")]
    public void DebugDumpSeenAnchors()
    {
        Debug.Log("[SeenAnchorTracker] ---- Seen anchors dump ----");
        foreach (var r in _seen.Values)
        {
            Debug.Log($"Seen {HumanReadableLabel(r.labels)} " +
                      $"first at {r.firstSeenPosition}, last at {r.lastSeenPosition}, " +
                      $"count={r.seenCount}");
        }
    }
}
