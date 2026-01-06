using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace PetBrain
{
    public class PetBrainOrchestrator : MonoBehaviour
    {
        [Header("Refs")]
        public global::PerceptionService perception;
        public PetActionDispatcher dispatcher;

        [Header("LLM Bridge (assign your OllamaLLMBridge component)")]
        public MonoBehaviour llmBridgeBehaviour;

        [Header("User input file")]
        public string userInputFileName = "UserInput.txt";

        [Header("Behavior")]
        [Range(1, 5)] public int maxCommandsPerCycle = 5;
        public bool enableLLM = true;

        [Header("Find loop settings")]
        public int findMaxAttempts = 6;

        readonly Queue<BrainCommand> _queue = new Queue<BrainCommand>();
        FindCoordinator _find = new FindCoordinator();

        bool _thinking;
        bool _waitingAction;

        void Awake()
        {
            if (perception == null) perception = FindFirstObjectByType<global::PerceptionService>();
            if (dispatcher == null) dispatcher = FindFirstObjectByType<PetActionDispatcher>();

            _find.maxAttempts = findMaxAttempts;
        }

        void OnEnable()
        {
            PetBrainEvents.ActionCompleted += OnActionCompleted;
        }

        void OnDisable()
        {
            PetBrainEvents.ActionCompleted -= OnActionCompleted;
        }

        async void Start()
        {
            Debug.Log("[PetBrainOrchestrator] started");

            // snapshot이 조금이라도 갱신될 시간을 줌
            await Task.Yield();
            RequestThink("startup");
        }

        void OnActionCompleted(PetActionResult result)
        {
            _waitingAction = false;

            // 1) Find 진행 중이면: 다음 스텝 결정
            if (_find.IsActive)
            {
                StepFindLoop();
                return;
            }

            // 2) LLM 커맨드 큐가 남아있으면 계속 실행
            if (_queue.Count > 0)
            {
                DispatchNextQueuedCommand();
                return;
            }

            // 3) 다음 Think
            RequestThink("action_completed");
        }

        void StepFindLoop()
        {
            if (perception == null)
            {
                Debug.LogWarning("[Brain] No perception; abort find.");
                _find.Stop();
                RequestThink("find_abort_no_perception");
                return;
            }

            var snap = perception.CurrentSnapshot;
            var d = _find.DecideNext(snap, snap.petPosition);

            if (d.type == FindDecisionType.None)
            {
                _find.Stop();
                RequestThink("find_none");
                return;
            }

            _waitingAction = true;
            dispatcher.ExecuteFindDecision(d);

            if (d.type == FindDecisionType.ObjectFound || d.type == FindDecisionType.ObjectNotFound)
            {
                // 이 커맨드도 action 완료 이벤트가 올 텐데,
                // 완료 후에는 OnActionCompleted에서 다시 Think하도록 흐름이 이어짐
            }
        }

        void RequestThink(string reason)
        {
            if (_thinking || _waitingAction) return;
            _ = ThinkAsync(reason);
        }

        async Task ThinkAsync(string reason)
        {
            if (_thinking) return;
            _thinking = true;

            try
            {
                if (perception == null) perception = FindFirstObjectByType<global::PerceptionService>();
                if (dispatcher == null) dispatcher = FindFirstObjectByType<PetActionDispatcher>();

                if (perception == null || dispatcher == null)
                {
                    Debug.LogWarning("[Brain] Missing perception/dispatcher.");
                    return;
                }

                var snap = perception.CurrentSnapshot;

                // user input consume
                var userSpeech = UserInputReader.ConsumeUserInput(userInputFileName);

                // build prompts
                string systemPrompt = LLMPromptBuilder.BuildSystemPrompt();
                string userPrompt = LLMPromptBuilder.BuildUserPrompt(snap, userSpeech);

                Debug.Log($"[Brain] THINK ({reason}) userSpeech='{userSpeech}' visible={snap.visibleAnchors?.Count ?? 0} mem={snap.memoryAnchors?.Count ?? 0}");

                string llmRaw;
                if (enableLLM && llmBridgeBehaviour != null)
                {
                    llmRaw = await CallLLMAsync(llmBridgeBehaviour, systemPrompt, userPrompt);
                }
                else
                {
                    llmRaw = FallbackBehavior(userSpeech);
                }

                if (string.IsNullOrWhiteSpace(llmRaw))
                {
                    llmRaw = FallbackBehavior(userSpeech);
                }

                // parse
                if (!LLMResponseParser.TryParseCommands(llmRaw, out var cmds, out var err))
                {
                    Debug.LogWarning($"[Brain] LLM parse failed: {err}\nRAW:\n{llmRaw}");
                    cmds = new List<BrainCommand> { new BrainCommand { action = "idle", duration = 3f } };
                }

                // find command has priority
                BrainCommand findCmd = null;
                foreach (var c in cmds)
                {
                    if (string.Equals(c.action, "find", StringComparison.OrdinalIgnoreCase))
                    {
                        findCmd = c;
                        break;
                    }
                }

                _queue.Clear();

                if (findCmd != null)
                {
                    var label = string.IsNullOrWhiteSpace(findCmd.label) ? "unknown" : findCmd.label;
                    _find.maxAttempts = findMaxAttempts;
                    _find.BeginFind(label, snap);

                    // find 루프는 즉시 1스텝 실행
                    StepFindLoop();
                    return;
                }

                // enqueue normal commands
                int take = Mathf.Min(maxCommandsPerCycle, cmds.Count);
                for (int i = 0; i < take; i++)
                    _queue.Enqueue(cmds[i]);

                DispatchNextQueuedCommand();
            }
            finally
            {
                _thinking = false;
            }
        }

        void DispatchNextQueuedCommand()
        {
            if (_queue.Count == 0) return;
            var cmd = _queue.Dequeue();

            _waitingAction = true;
            dispatcher.ExecuteCommand(cmd);
        }

        // ---- LLM Bridge call via reflection (method-name agnostic) ----
        async Task<string> CallLLMAsync(MonoBehaviour bridge, string systemPrompt, string userPrompt)
        {
            if (bridge == null) return null;

            try
            {
                var t = bridge.GetType();

                // 1) Try any method with (string,string) -> Task<string> or string
                var methods = t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var m in methods)
                {
                    var ps = m.GetParameters();
                    if (ps.Length != 2) continue;
                    if (ps[0].ParameterType != typeof(string) || ps[1].ParameterType != typeof(string)) continue;

                    // Task<string>
                    if (m.ReturnType.IsGenericType && m.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                    {
                        var taskObj = m.Invoke(bridge, new object[] { systemPrompt, userPrompt });
                        if (taskObj is Task task)
                        {
                            await task;
                            var resultProp = taskObj.GetType().GetProperty("Result");
                            if (resultProp != null)
                                return resultProp.GetValue(taskObj) as string;
                        }
                    }

                    // string
                    if (m.ReturnType == typeof(string))
                    {
                        return m.Invoke(bridge, new object[] { systemPrompt, userPrompt }) as string;
                    }
                }

                // 2) If bridge has single prompt API (string)->Task<string>/string, concatenate
                string merged = $"SYSTEM:\n{systemPrompt}\n\nUSER:\n{userPrompt}";
                foreach (var m in methods)
                {
                    var ps = m.GetParameters();
                    if (ps.Length != 1) continue;
                    if (ps[0].ParameterType != typeof(string)) continue;

                    if (m.ReturnType.IsGenericType && m.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                    {
                        var taskObj = m.Invoke(bridge, new object[] { merged });
                        if (taskObj is Task task)
                        {
                            await task;
                            var resultProp = taskObj.GetType().GetProperty("Result");
                            if (resultProp != null)
                                return resultProp.GetValue(taskObj) as string;
                        }
                    }
                    if (m.ReturnType == typeof(string))
                    {
                        return m.Invoke(bridge, new object[] { merged }) as string;
                    }
                }

                Debug.LogError("[Brain] Could not find a compatible method on LLM bridge. Use fallback.");
                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Brain] LLM call failed: {e.Message}");
                return null;
            }
        }

        string FallbackBehavior(string userSpeech)
        {
            // LLM 없을 때도 시스템이 “계속” 돌게 하는 임시 행동 생성기
            userSpeech = (userSpeech ?? "").ToLowerInvariant();

            if (userSpeech.Contains("sit"))
                return @"[{""action"":""sit"",""duration"":5.0}]";
            if (userSpeech.Contains("sleep"))
                return @"[{""action"":""lying"",""duration"":6.0},{""action"":""sleep"",""duration"":12.0}]";
            if (userSpeech.Contains("find"))
                return @"[{""action"":""find"",""label"":""table""}]";

            // default
            return @"[
  {""action"":""walk"",""target"":""floor"",""speed"":""walk""},
  {""action"":""lookatuser""},
  {""action"":""sit"",""duration"":4.0},
  {""action"":""showemoji"",""emoji"":""😊"",""duration"":2.5}
]";
        }
    }
}
