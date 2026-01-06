using System;
using System.Reflection;
using UnityEngine;
using Meta.XR.MRUtilityKit;

public static class MRUKRaycastCompat
{
    static bool _inited;
    static MethodInfo _raycast3;
    static MethodInfo _raycast4;
    static object _componentTypeAll;

    static void EnsureInit()
    {
        if (_inited) return;
        _inited = true;

        var t = typeof(MRUKAnchor);
        foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public))
        {
            if (m.Name != "Raycast") continue;
            var ps = m.GetParameters();

            if (ps.Length == 3 &&
                ps[0].ParameterType == typeof(Ray) &&
                ps[1].ParameterType == typeof(float) &&
                ps[2].IsOut)
            {
                _raycast3 = m;
            }
            else if (ps.Length == 4 &&
                     ps[0].ParameterType == typeof(Ray) &&
                     ps[1].ParameterType == typeof(float) &&
                     ps[2].IsOut)
            {
                _raycast4 = m;
                var enumType = ps[3].ParameterType;
                if (enumType.IsEnum)
                {
                    try { _componentTypeAll = Enum.Parse(enumType, "All"); }
                    catch { _componentTypeAll = Activator.CreateInstance(enumType); }
                }
            }
        }
    }

    public static bool Raycast(MRUKAnchor anchor, Ray ray, float maxDist, out RaycastHit hit)
    {
        EnsureInit();
        hit = default;
        if (anchor == null) return false;

        if (_raycast4 != null && _componentTypeAll != null)
        {
            object[] args = { ray, maxDist, hit, _componentTypeAll };
            bool ok = (bool)_raycast4.Invoke(anchor, args);
            hit = (RaycastHit)args[2];
            return ok;
        }

        if (_raycast3 != null)
        {
            object[] args = { ray, maxDist, hit };
            bool ok = (bool)_raycast3.Invoke(anchor, args);
            hit = (RaycastHit)args[2];
            return ok;
        }

        return false;
    }
}
