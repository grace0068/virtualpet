using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class BrainOrchestrator : MonoBehaviour
{
    [Header("Refs")]
    public PerceptionService perception;      // 너가 만든 PerceptionService
    public Transform petRoot;                 // 실제 이동하는 펫 루트(없으면 this.transform)
    public LLMBridgeBase llmBridge;           // MockLLMBridge 또는 HttpLLMBridge

    [Header("Mode")]
    public PetMode mode = PetMode.LLM;

    [Header("Find Mode")]
    public string findTargetLabel;            // 예: "TABLE", "COUCH" 등

    [Header("Command Output")]
    public bool writeToAssetsInEditor = false;
    public string assetsRelativePath = "Assets/Code/Scripts/LLMCommands.json";
    public string persistentFileName = "LLMCommands.json";

    [Header("Think Control")]
    public float minThinkInterval = 0.5f;     // 너무 자주 쓰지 않기
    float _lastThinkTime = -999f;
    bool _thinking;

    void OnEnable()
    {
        PetBrainEvents.BrainTickRequested += OnBrainTickRequested;
        PetBrainEvents.ActionFinished += OnActionFinished;
    }

    void OnDisable()
    {
        PetBrainEvents.BrainTickRequested -= OnBrainTickRequested;
        PetBrainEvents.ActionFinished -= OnActionFinished;
    }

    async void Start()
    {
        // 시작하자마자 1회 생성
        await Task.Yield();
        PetBrainEvents.RequestBrainTick();
    }

    void OnActionFinished(ActionFinishedArgs args)
    {
        // Action이 끝나면 다음 행동 생성
        PetBrainEvents.RequestBrainTick();
    }

    void OnBrainTickRequested()
    {
        if (_thinking) return;
        if (Time.time - _lastThinkTime < minThinkInterval) return;
        _ = ThinkAndWriteAsync();
    }

    async Task ThinkAndWriteAsync()
    {
        _thinking = true;
        _lastThinkTime = Time.time;

        // Perception 모드도 같이 맞춰두면 스냅샷에 mode 기록됨
        if (perception != null) perception.currentMode = mode;

        var snap = (perception != null) ? perception.CurrentSnapshot : new SceneSnapshot();
        // pet pose 보정: perception이 root를 못 잡는 상황 대비
        var root = (petRoot != null) ? petRoot : transform;
        snap.petPosition = root.position;
        snap.petRotation = root.rotation;
        snap.mode = mode;

        List<LLMCommand> commands;

        switch (mode)
        {
            case PetMode.Manual:
                _thinking = false;
                return;

            case PetMode.Find:
                commands = BuildFindCommands(snap);
                break;

            case PetMode.Explore:
                commands = BuildExploreCommands(snap);
                break;

            case PetMode.LLM:
            default:
                commands = await BuildLLMCommands(snap);
                break;
        }

        // 최종 저장
        var path = ResolveCommandsPath();
        LLMCommandsWriter.WriteCommands(path, commands);

        PetBrainEvents.RaiseCommandsCommitted(path, commands.Count, $"mode={mode}");

        _thinking = false;
    }

    async Task<List<LLMCommand>> BuildLLMCommands(SceneSnapshot snap)
    {
        if (llmBridge == null)
        {
            Debug.LogWarning("[BrainOrchestrator] llmBridge is null. Falling back to Explore policy.");
            return BuildExploreCommands(snap);
        }

        var prompt = LLMPromptBuilder.Build(snap);

        var raw = await llmBridge.GenerateAsync(prompt);

        // 서버가 {"output":"[...]"} 형태면 그냥 통째로 들어올 수 있음 → Parser가 일부 처리
        var parsed = LLMCommandParser.Parse(raw);

        // LLM이 target/speed 형식으로 준 movement를 (x,y,z)로 변환
        PostProcessToActionContract(parsed, snap);

        // 3~5로 clamp
        if (parsed.Count > 5) parsed.RemoveRange(5, parsed.Count - 5);
        if (parsed.Count < 3)
            parsed.AddRange(BuildExploreCommands(snap)); // 부족하면 채우기(간단)

        return parsed;
    }

    List<LLMCommand> BuildFindCommands(SceneSnapshot snap)
    {
        // memory에서 label 매칭되는 걸 찾고, 있으면 그 위치로 search/objectfound 시퀀스
        var cmds = new List<LLMCommand>();

        var mem = perception != null ? perception.Memory : null;
        AnchorRecord rec = null;

        if (mem != null && !string.IsNullOrWhiteSpace(findTargetLabel))
            rec = mem.FindByLabel(findTargetLabel);

        if (rec != null)
        {
            cmds.Add(new LLMCommand { action = "showemoji", emoji = "🤔", duration = 2.5f });
            cmds.Add(new LLMCommand { action = "search", x = rec.position.x, y = rec.position.y, z = rec.position.z });
            cmds.Add(new LLMCommand { action = "objectfound", x = rec.position.x, y = rec.position.y, z = rec.position.z });
            cmds.Add(new LLMCommand { action = "sit", duration = 3.5f });
        }
        else
        {
            // 못 찾으면 explore로 전환하는 식의 “의사결정”
            cmds.Add(new LLMCommand { action = "showemoji", emoji = "❓", duration = 2.5f });
            cmds.AddRange(BuildExploreCommands(snap));
        }

        return TrimTo3to5(cmds);
    }

    List<LLMCommand> BuildExploreCommands(SceneSnapshot snap)
    {
        var cmds = new List<LLMCommand>();
        var p = snap.petPosition;

        // 랜덤 탐색 → 관찰 → 휴식
        var (tx, ty, tz) = RandomNear(p, radius: 3.5f);
        cmds.Add(new LLMCommand { action = "walk", x = tx, y = ty, z = tz });

        if (Random.value < 0.3f)
            cmds.Add(new LLMCommand { action = "lookatuser" });

        if (Random.value < 0.4f)
            cmds.Add(new LLMCommand { action = "showemoji", emoji = Random.value < 0.5f ? "👀" : "🐾", duration = 2.5f });

        cmds.Add(new LLMCommand { action = "sit", duration = Random.Range(3f, 6f) });

        return TrimTo3to5(cmds);
    }

    void PostProcessToActionContract(List<LLMCommand> cmds, SceneSnapshot snap)
    {
        var p = snap.petPosition;

        for (int i = 0; i < cmds.Count; i++)
        {
            var c = cmds[i];
            if (c == null || string.IsNullOrWhiteSpace(c.action)) continue;

            var a = c.action.ToLowerInvariant();

            // LLM이 {"action":"walk","target":"floor","speed":"walk"}로 준 경우
            if ((a == "walk" || a == "run") && (!c.x.HasValue || !c.z.HasValue))
            {
                // target=floor 이면 랜덤 좌표 생성
                var (tx, ty, tz) = RandomNear(p, radius: 5f);
                c.x = tx; c.y = ty; c.z = tz;
            }

            // duration 없는 showemoji는 기본 2.5
            if (a == "showemoji" && !c.duration.HasValue)
                c.duration = 2.5f;
        }
    }

    (float x, float y, float z) RandomNear(Vector3 origin, float radius)
    {
        var v = Random.insideUnitCircle * radius;
        float x = origin.x + v.x;
        float z = origin.z + v.y;
        float y = origin.y; // 보통 바닥 0이지만, 펫 y로 맞춤
        return (x, y, z);
    }

    List<LLMCommand> TrimTo3to5(List<LLMCommand> cmds)
    {
        // 최소 3개 보장
        while (cmds.Count < 3)
            cmds.Add(new LLMCommand { action = "idle", duration = 2.5f });

        if (cmds.Count > 5)
            cmds.RemoveRange(5, cmds.Count - 5);

        return cmds;
    }

    string ResolveCommandsPath()
    {
#if UNITY_EDITOR
        if (writeToAssetsInEditor && !string.IsNullOrWhiteSpace(assetsRelativePath))
            return LLMCommandsWriter.ResolveAssetsPath(assetsRelativePath);
#endif
        return LLMCommandsWriter.ResolveDefaultPath(persistentFileName);
    }
}
