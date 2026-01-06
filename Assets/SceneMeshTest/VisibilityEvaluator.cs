using System.Collections.Generic;
using UnityEngine;
using Meta.XR.MRUtilityKit;

public class VisibilityEvaluator
{
    public enum OcclusionMode { None, MRUKPrimitives, PhysicsColliders }

    public OcclusionMode occlusionMode = OcclusionMode.MRUKPrimitives;
    public LayerMask physicsMask = ~0;

    public int planeGrid = 3;
    public bool volumeUseFaceCenters = true;
    public float occlusionEpsilon = 0.02f;

    readonly List<Vector3> _samplePts = new();

    public AnchorObservation Evaluate(Camera cam, MRUKAnchor a, List<MRUKAnchor> allAnchors)
    {
        var obs = new AnchorObservation();

        obs.id = GetAnchorId(a);
        obs.label = a.Label.ToString();
        obs.position = a.GetAnchorCenter();
        obs.rotation = a.transform.rotation;
        obs.size = a.GetAnchorSize();

        GetSamplePoints(a, _samplePts);

        int total = 0, vis = 0;
        Vector3 camPos = cam.transform.position;

        for (int i = 0; i < _samplePts.Count; i++)
        {
            var p = _samplePts[i];
            if (!InFrustum(cam, p)) continue;
            total++;

            if (occlusionMode == OcclusionMode.None) { vis++; continue; }

            if (!IsOccluded(camPos, p, a, allAnchors))
                vis++;
        }

        obs.totalSamples = total;
        obs.visibleSamples = vis;
        obs.visibilityScore = (total == 0) ? 0f : (float)vis / total;

        return obs;
    }

    string GetAnchorId(MRUKAnchor a)
    {
        if (a.HasValidHandle) return a.Anchor.Uuid.ToString();
        return $"runtime:{a.gameObject.GetInstanceID()}";
    }

    static bool InFrustum(Camera cam, Vector3 wp)
    {
        var v = cam.WorldToViewportPoint(wp);
        return v.z > 0f && v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f;
    }

    bool IsOccluded(Vector3 camPos, Vector3 p, MRUKAnchor target, List<MRUKAnchor> all)
    {
        Vector3 dir = p - camPos;
        float dist = dir.magnitude;
        if (dist <= Mathf.Epsilon) return false;
        dir /= dist;

        var ray = new Ray(camPos, dir);

        if (occlusionMode == OcclusionMode.PhysicsColliders)
        {
            if (Physics.Raycast(ray, out var hit, dist - occlusionEpsilon, physicsMask, QueryTriggerInteraction.Ignore))
            {
                var ht = hit.transform;
                if (ht != null && (ht == target.transform || ht.IsChildOf(target.transform))) return false;
                return true;
            }
            return false;
        }

        // MRUKPrimitives: 가장 가까운 hit anchor를 찾기
        MRUKAnchor closest = null;
        float closestD = float.PositiveInfinity;

        float maxDist = dist + occlusionEpsilon;

        for (int i = 0; i < all.Count; i++)
        {
            var a = all[i];
            if (a == null) continue;

            if (MRUKRaycastCompat.Raycast(a, ray, maxDist, out var hit))
            {
                if (hit.distance < closestD)
                {
                    closestD = hit.distance;
                    closest = a;
                }
            }
        }

        if (closest == null) return false;
        return !SameOrRelated(closest, target);
    }

    static bool SameOrRelated(MRUKAnchor a, MRUKAnchor b)
    {
        if (a == b) return true;
        for (var p = a; p != null; p = p.ParentAnchor) if (p == b) return true;
        for (var p = b; p != null; p = p.ParentAnchor) if (p == a) return true;
        return false;
    }

    void GetSamplePoints(MRUKAnchor a, List<Vector3> outPts)
    {
        outPts.Clear();

        Vector3 center = a.GetAnchorCenter();
        Vector3 size = a.GetAnchorSize();
        Vector3 half = size * 0.5f;
        Vector3 localCenter = a.transform.InverseTransformPoint(center);

        bool hasPlane = a.PlaneRect.HasValue;
        bool hasVolume = a.VolumeBounds.HasValue;

        if (!hasPlane && !hasVolume)
        {
            outPts.Add(center);
            return;
        }

        if (hasVolume)
        {
            // 8 corners
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                outPts.Add(a.transform.TransformPoint(localCenter + new Vector3(sx*half.x, sy*half.y, sz*half.z)));

            if (volumeUseFaceCenters)
            {
                outPts.Add(a.transform.TransformPoint(localCenter + new Vector3(+half.x, 0, 0)));
                outPts.Add(a.transform.TransformPoint(localCenter + new Vector3(-half.x, 0, 0)));
                outPts.Add(a.transform.TransformPoint(localCenter + new Vector3(0, +half.y, 0)));
                outPts.Add(a.transform.TransformPoint(localCenter + new Vector3(0, -half.y, 0)));
                outPts.Add(a.transform.TransformPoint(localCenter + new Vector3(0, 0, +half.z)));
                outPts.Add(a.transform.TransformPoint(localCenter + new Vector3(0, 0, -half.z)));
            }
            return;
        }

        // plane grid: 가장 얇은 축(thickness) 제외하고 2축으로 grid
        float ax = Mathf.Abs(size.x), ay = Mathf.Abs(size.y), az = Mathf.Abs(size.z);
        int thickness = (ax <= ay && ax <= az) ? 0 : (ay <= ax && ay <= az) ? 1 : 2;
        int a0 = (thickness == 0) ? 1 : 0;
        int a1 = (thickness == 2) ? 1 : 2;
        if (a0 == thickness) a0 = 2;
        if (a1 == thickness) a1 = 0;
        if (a0 == a1) a1 = (a0 == 1) ? 2 : 1;

        int g = Mathf.Max(2, planeGrid);
        for (int iy = 0; iy < g; iy++)
        for (int ix = 0; ix < g; ix++)
        {
            float tx = ix / (float)(g - 1);
            float ty = iy / (float)(g - 1);

            float ox = Mathf.Lerp(-1f, +1f, tx) * GetAxis(half, a0);
            float oy = Mathf.Lerp(-1f, +1f, ty) * GetAxis(half, a1);

            Vector3 lp = localCenter;
            lp = SetAxis(lp, a0, GetAxis(lp, a0) + ox);
            lp = SetAxis(lp, a1, GetAxis(lp, a1) + oy);
            lp = SetAxis(lp, thickness, GetAxis(localCenter, thickness));

            outPts.Add(a.transform.TransformPoint(lp));
        }
    }

    static float GetAxis(Vector3 v, int axis) => axis == 0 ? v.x : axis == 1 ? v.y : v.z;
    static Vector3 SetAxis(Vector3 v, int axis, float value)
    {
        if (axis == 0) v.x = value;
        else if (axis == 1) v.y = value;
        else v.z = value;
        return v;
    }
}
