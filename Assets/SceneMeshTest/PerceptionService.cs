using System.Collections.Generic;
using UnityEngine;

public class PerceptionService : MonoBehaviour
{
    public PetMode currentMode = PetMode.LLM;
    public Transform petRoot;

    [Header("Refs")]
    public Camera petCamera;
    public MonoBehaviour anchorProviderBehaviour; // MRUKAnchorProvider
    IAnchorProvider _provider;

    [Header("Visibility Settings")]
    public VisibilityEvaluator.OcclusionMode occlusionMode = VisibilityEvaluator.OcclusionMode.MRUKPrimitives;
    public int planeGrid = 3;

    [Header("Snapshot")]
    public float snapshotWriteInterval = 0.2f; // “실시간” 느낌
    public string snapshotFileName = "SceneSnapshot.json";

    public SceneSnapshot CurrentSnapshot { get; private set; } = new SceneSnapshot();

    AnchorMemory _memory = new AnchorMemory();
    VisibilityEvaluator _vis = new VisibilityEvaluator();
    float _nextWrite;

    void Start() { Debug.Log("[PerceptionService] started"); }

    void Awake()
    {
        _provider = anchorProviderBehaviour as IAnchorProvider;
        _vis.occlusionMode = occlusionMode;
        _vis.planeGrid = planeGrid;
    }

    void Update()
    {
        if (_provider == null || petCamera == null) return;

        var anchors = _provider.GetAnchors();

        // 1) visible 계산
        var visible = new List<AnchorObservation>(anchors.Count);
        for (int i = 0; i < anchors.Count; i++)
        {
            var a = anchors[i];
            if (a == null) continue;

            var obs = _vis.Evaluate(petCamera, a, anchors);

            // 최소 가시성 필터(원하면 인스펙터로 빼도 됨)
            if (obs.visibilityScore > 0.15f)
                visible.Add(obs);
        }

        // 2) memory 업데이트
        _memory.UpdateFromVisible(visible, Time.time);

        // 3) snapshot 갱신(인메모리)
        CurrentSnapshot.time = Time.time;
        CurrentSnapshot.mode = currentMode;
        var root = (petRoot != null) ? petRoot : transform;
        CurrentSnapshot.petPosition = root.position;
        CurrentSnapshot.petRotation = root.rotation;
        CurrentSnapshot.visibleAnchors = visible;

        // memoryAnchors는 매 프레임 새 리스트로 만들어도 되고, 주기 갱신도 가능
        CurrentSnapshot.memoryAnchors = new List<AnchorRecord>(_memory.All);
        Debug.Log("[PerceptionService] snapshot path = " + SnapshotWriter.GetSnapshotPath(snapshotFileName));


        // 4) 파일 출력(외부 LLM/디버깅용)
        if (Time.time >= _nextWrite)
        {
            _nextWrite = Time.time + snapshotWriteInterval;
            SnapshotWriter.WriteSnapshotJson(snapshotFileName, CurrentSnapshot);
        }
    }

    public AnchorMemory Memory => _memory;
}
