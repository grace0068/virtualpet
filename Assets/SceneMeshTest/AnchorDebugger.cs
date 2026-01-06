using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Meta.XR.MRUtilityKit;

public class AnchorDebugger : MonoBehaviour
{
    private enum RelationType
    {
        InFrontOf,
        Behind,
        LeftOf,
        RightOf
    }

    private IEnumerator Start()
    {
        Debug.Log("[AnchorDebugger] Start called");

        // 1) MRUK 준비될 때까지 대기
        while (MRUK.Instance == null)
        {
            Debug.Log("[AnchorDebugger] Waiting for MRUK.Instance...");
            yield return null;
        }

        // 2) Room 준비될 때까지 대기
        while (MRUK.Instance.GetCurrentRoom() == null)
        {
            Debug.Log("[AnchorDebugger] Waiting for current room...");
            yield return null;
        }

        var room = MRUK.Instance.GetCurrentRoom();
        Debug.Log($"[AnchorDebugger] Got room: {room.name}");

        // 3) 앵커 리스트 찍기
        var anchors = room.Anchors.ToList();
        foreach (var anchor in anchors)
        {
            var label  = anchor.Label;
            var center = anchor.GetAnchorCenter();
            Debug.Log($"[AnchorDebugger] Anchor {label} at {center}");
        }

        // ---------- 여기서부터: 실제 좌표로 관계 계산해서 문장 출력 ----------

        if (anchors.Count < 2)
        {
            Debug.Log("[AnchorDebugger] Not enough anchors to describe relations.");
            yield break;
        }

        int maxSentences = 7;
        int sentencesMade = 0;
        int safetyCounter = 100; // 무한 루프 방지용

        // 이미 사용한 (A,B,relation) 조합 저장해서 중복 문장 방지
        var usedTriples = new HashSet<(MRUKAnchor a, MRUKAnchor b, RelationType r)>();

        Debug.Log("[AnchorDebugger] Spatial relations based on actual positions:");

        while (sentencesMade < maxSentences && safetyCounter-- > 0)
        {
            // 1) 관계 타입을 랜덤으로 선택
            RelationType relation = (RelationType)Random.Range(0, 4);

            // 2) 이 관계를 만족하는 (A,B) 후보 쌍을 모두 찾기
            var candidates = new List<(MRUKAnchor a, MRUKAnchor b)>();

            for (int i = 0; i < anchors.Count; i++)
            {
                for (int j = 0; j < anchors.Count; j++)
                {
                    if (i == j) continue;

                    var a = anchors[i];
                    var b = anchors[j];

                    if (CheckRelation(a, b, relation))
                    {
                        var triple = (a, b, relation);
                        if (!usedTriples.Contains(triple))
                        {
                            candidates.Add((a, b));
                        }
                    }
                }
            }

            // 3) 이 관계를 만족하는 쌍이 없다면, 다른 관계로 다시 시도
            if (candidates.Count == 0)
                continue;

            // 4) 후보 중 하나를 랜덤으로 선택
            var chosen = candidates[Random.Range(0, candidates.Count)];
            usedTriples.Add((chosen.a, chosen.b, relation));

            string nameA = HumanReadableLabel(chosen.a.Label);
            string nameB = HumanReadableLabel(chosen.b.Label);
            string phrase = RelationToPhrase(relation);

            Debug.Log($"[AnchorDebugger] {nameA} {phrase} {nameB}.");

            sentencesMade++;
        }

        if (sentencesMade == 0)
        {
            Debug.Log("[AnchorDebugger] Could not find any valid anchor pairs for front/behind/left/right.");
        }

        Debug.Log("[AnchorDebugger] Done listing anchors and relations.");
    }

    // 실제로 A가 B 기준으로 front/behind/left/right 에 있는지 판정
    private bool CheckRelation(MRUKAnchor a, MRUKAnchor b, RelationType relation)
    {
        // 기준점은 Anchor center 사용
        Vector3 posA = a.GetAnchorCenter();
        Vector3 posB = b.GetAnchorCenter();

        Vector3 delta = posA - posB;
        // 높이(Y)는 무시하고 평면 상에서만 관계 판단
        delta.y = 0f;

        if (delta.sqrMagnitude < 0.01f)
            return false; // 거의 같은 위치면 무시

        Vector3 dir = delta.normalized;

        // B의 forward/right 도 평면에 투영
        Vector3 forward = b.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        forward.Normalize();

        Vector3 right = b.transform.right;
        right.y = 0f;
        if (right.sqrMagnitude < 0.0001f)
            right = Vector3.right;
        right.Normalize();

        float forwardDot = Vector3.Dot(dir, forward);
        float rightDot   = Vector3.Dot(dir, right);

        const float threshold = 0.5f; // cos(60deg) 정도, 60도 이내면 해당 방향으로 본다고 가정

        switch (relation)
        {
            case RelationType.InFrontOf:
                return forwardDot > threshold;

            case RelationType.Behind:
                return forwardDot < -threshold;

            case RelationType.LeftOf:
                return rightDot < -threshold;

            case RelationType.RightOf:
                return rightDot > threshold;

            default:
                return false;
        }
    }

    // 라벨 enum을 자연어에 쓰기 좋은 문자열로 변환
    private string HumanReadableLabel(MRUKAnchor.SceneLabels labels)
    {
        // 플래그가 여러 개면 첫 번째만 사용 (예: "TABLE, GLOBAL_MESH" → "TABLE")
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

    // RelationType 을 자연어 문구로 변환
    private string RelationToPhrase(RelationType relation)
    {
        switch (relation)
        {
            case RelationType.InFrontOf: return "is in front of";
            case RelationType.Behind:    return "is behind";
            case RelationType.LeftOf:    return "is to the left of";
            case RelationType.RightOf:   return "is to the right of";
            default:                     return "is near";
        }
    }
}
