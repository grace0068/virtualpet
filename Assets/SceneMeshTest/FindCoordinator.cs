using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI; // ✅ 추가

namespace PetBrain
{
    public class FindCoordinator
    {
        public int maxAttempts = 6;
        public float candidateRadiusMin = 2.0f;
        public float candidateRadiusMax = 4.0f;
        public int candidatesPerStep = 10;
        public float exploredNeighborhoodRadius = 2.0f;

        // ✅ NavMesh 관련 (최소 추가)
        public bool useNavMesh = true;
        public float navMeshSnapDistance = 1.2f;
        public float navMeshSnapDistanceForKnownPos = 2.0f;

        bool _active;
        string _targetLabelNorm;
        bool _knownAtStart;
        bool _movedToKnown;
        int _attempts;

        readonly List<Vector3> _searchHistory = new List<Vector3>();

        public bool IsActive => _active;
        public string TargetLabel => _targetLabelNorm;

        public void BeginFind(string targetLabel, global::SceneSnapshot snapshot)
        {
            _active = true;
            _attempts = 0;
            _movedToKnown = false;
            _searchHistory.Clear();

            _targetLabelNorm = NormalizeLabel(targetLabel);

            _knownAtStart = TryFindInMemory(snapshot, _targetLabelNorm, out _);
            Debug.Log($"[FindCoordinator] BeginFind label='{targetLabel}' norm='{_targetLabelNorm}' knownAtStart={_knownAtStart}");
        }

        public void Stop()
        {
            _active = false;
        }

        public FindDecision DecideNext(global::SceneSnapshot snapshot, Vector3 petPos)
        {
            if (!_active)
                return FindDecision.None();

            // if now visible -> found
            if (TryFindInVisible(snapshot, _targetLabelNorm, out var visPos))
            {
                _active = false;
                return FindDecision.Found(visPos, "target visible now");
            }

            // if in memory -> found (explore-mode success)
            if (!_knownAtStart && TryFindInMemory(snapshot, _targetLabelNorm, out var memPosNow))
            {
                _active = false;
                return FindDecision.Found(memPosNow, "target discovered in memory");
            }

            // known at start: go to known position once, then if still not visible -> notfound (confused)
            if (_knownAtStart)
            {
                if (!_movedToKnown)
                {
                    if (TryFindInMemory(snapshot, _targetLabelNorm, out var knownPos))
                    {
                        // ✅ knownPos도 NavMesh 위로 스냅 시도 (최소 추가)
                        if (useNavMesh && TryProjectToNavMesh(knownPos, navMeshSnapDistanceForKnownPos, out var snappedKnown))
                            knownPos = snappedKnown;

                        _movedToKnown = true;
                        _attempts++;
                        _searchHistory.Add(knownPos);
                        return FindDecision.Search(knownPos, "go to known location confidently");
                    }

                    // fallback: if memory missing unexpectedly
                    _active = false;
                    return FindDecision.NotFound("knownAtStart but no memory record");
                }

                // moved there but still not visible => confused stop
                _active = false;
                return FindDecision.NotFound("went to known spot but target not visible");
            }

            // explore-mode loop
            if (_attempts >= maxAttempts)
            {
                _active = false;
                return FindDecision.NotFound("max attempts exceeded");
            }

            var next = PickLeastExploredPoint(snapshot, petPos);
            _attempts++;
            _searchHistory.Add(next);
            return FindDecision.Search(next, $"explore attempt {_attempts}/{maxAttempts}");
        }

        Vector3 PickLeastExploredPoint(global::SceneSnapshot snapshot, Vector3 petPos)
        {
            // memory anchor positions
            var memoryPositions = new List<Vector3>(64);
            if (snapshot.memoryAnchors != null)
            {
                foreach (var a in snapshot.memoryAnchors)
                {
                    if (a == null) continue;
                    memoryPositions.Add(AnchorReflection.GetVector3(a, "position", Vector3.zero));
                }
            }

            Vector3 best = petPos + new Vector3(0, 0, 2f);
            float bestScore = float.MaxValue;

            bool foundAnyValid = false;

            for (int i = 0; i < candidatesPerStep; i++)
            {
                float r = UnityEngine.Random.Range(candidateRadiusMin, candidateRadiusMax);
                float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

                var cand = petPos + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                cand.y = petPos.y;

                // ✅ (1) NavMesh 위로 스냅 + (2) 경로 유효성 체크 (최소 핵심 추가)
                if (useNavMesh)
                {
                    if (!TryProjectToNavMesh(cand, navMeshSnapDistance, out var snapped))
                        continue;

                    if (!HasCompletePath(petPos, snapped))
                        continue;

                    cand = snapped; // 스냅된 좌표를 최종 후보로 사용
                }

                // score = how many memory anchors are nearby (lower => less explored)
                float score = 0f;
                for (int k = 0; k < memoryPositions.Count; k++)
                {
                    if (Vector3.Distance(cand, memoryPositions[k]) <= exploredNeighborhoodRadius)
                        score += 1f;
                }

                // avoid repeating near previous search points
                for (int h = 0; h < _searchHistory.Count; h++)
                {
                    float d = Vector3.Distance(cand, _searchHistory[h]);
                    if (d < 1.0f) score += 5f;
                    else if (d < 2.0f) score += 1.5f;
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    best = cand;
                    foundAnyValid = true;
                }
            }

            // ✅ NavMesh 후보를 하나도 못 찾았으면 기존 방식의 fallback (그대로)
            if (!foundAnyValid)
                return petPos + new Vector3(0, 0, 2f);

            return best;
        }

        // ✅ NavMesh helper들 (최소 추가)
        static bool TryProjectToNavMesh(Vector3 pos, float maxDistance, out Vector3 snapped)
        {
            snapped = pos;
            if (NavMesh.SamplePosition(pos, out var hit, maxDistance, NavMesh.AllAreas))
            {
                snapped = hit.position;
                return true;
            }
            return false;
        }

        static bool HasCompletePath(Vector3 from, Vector3 to)
        {
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path))
                return false;
            return path.status == NavMeshPathStatus.PathComplete;
        }

        static bool TryFindInVisible(global::SceneSnapshot snapshot, string labelNorm, out Vector3 pos)
        {
            pos = Vector3.zero;
            if (snapshot.visibleAnchors == null) return false;

            foreach (var a in snapshot.visibleAnchors)
            {
                if (a == null) continue;
                var label = AnchorReflection.GetString(a, "label") ?? AnchorReflection.GetString(a, "classification");
                if (NormalizeLabel(label) == labelNorm)
                {
                    pos = AnchorReflection.GetVector3(a, "position", Vector3.zero);
                    return true;
                }
            }
            return false;
        }

        static bool TryFindInMemory(global::SceneSnapshot snapshot, string labelNorm, out Vector3 pos)
        {
            pos = Vector3.zero;
            if (snapshot.memoryAnchors == null) return false;

            foreach (var a in snapshot.memoryAnchors)
            {
                if (a == null) continue;
                var label = AnchorReflection.GetString(a, "label") ?? AnchorReflection.GetString(a, "classification");
                if (NormalizeLabel(label) == labelNorm)
                {
                    pos = AnchorReflection.GetVector3(a, "position", Vector3.zero);
                    return true;
                }
            }
            return false;
        }

        static string NormalizeLabel(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            s = s.Trim().ToLowerInvariant();
            s = s.Replace("_", "").Replace(" ", "");
            return s;
        }

        // reflection helpers (AnchorRecord/AnchorObservation 변화에 안전)
        static class AnchorReflection
        {
            public static string GetString(object obj, string member)
            {
                if (obj == null) return null;
                var t = obj.GetType();
                var f = t.GetField(member);
                if (f != null && f.FieldType == typeof(string)) return (string)f.GetValue(obj);
                var p = t.GetProperty(member);
                if (p != null && p.PropertyType == typeof(string)) return (string)p.GetValue(obj);
                return null;
            }

            public static Vector3 GetVector3(object obj, string member, Vector3 fallback)
            {
                if (obj == null) return fallback;
                var t = obj.GetType();
                var f = t.GetField(member);
                if (f != null && f.FieldType == typeof(Vector3)) return (Vector3)f.GetValue(obj);
                var p = t.GetProperty(member);
                if (p != null && p.PropertyType == typeof(Vector3)) return (Vector3)p.GetValue(obj);
                return fallback;
            }
        }
    }
}
