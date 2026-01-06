using System;
using System.Collections.Generic;
using UnityEngine;

public enum PetMode { Manual, LLM, Find, Explore }

[Serializable]
public class AnchorObservation
{
    // ID가 있으면 이걸 “주키(primary key)”로 사용
    public string id;         // device: uuid, prefab fallback: runtime:...
    public string label;

    public Vector3 position;
    public Quaternion rotation;

    // visibility
    public float visibilityScore; // 0..1
    public int visibleSamples;
    public int totalSamples;

    // optional (샘플 포인트 만들 때 유용)
    public Vector3 size;
}

[Serializable]
public class AnchorRecord
{
    public string id;
    public string label;

    public Vector3 position;
    public Quaternion rotation;

    public float firstSeenTime;
    public float lastSeenTime;
    public int seenCount;

    // 최근 visibility(마지막 관측 기준)
    public float lastVisibilityScore;

    public Vector3 size;
}

[Serializable]
public class SceneSnapshot
{
    public float time;
    public PetMode mode;

    public Vector3 petPosition;
    public Quaternion petRotation;

    public List<AnchorObservation> visibleAnchors = new();
    public List<AnchorRecord> memoryAnchors = new();
}
