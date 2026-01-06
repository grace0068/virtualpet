using System.Linq;
using System.Text;
using UnityEngine;
using System.Collections.Generic;

public static class LLMPromptBuilder
{
    // 너무 길면 필요 부분만 줄여도 됨. 지금은 네 템플릿을 최대한 보존.
    private const string BasePrompt = 
@"# Virtual Pet Behavior Generation Prompt
You are an AI system that generates natural, realistic dog behavior sequences for a virtual pet in a VR/AR environment. Your task is to create JSON command sequences that make the virtual dog behave like a real pet would in a home setting.

## Command System Overview
You will generate sequences of 3-5 commands in JSON format. Each command represents an action the pet will perform.

### Available Commands
- State: idle, sit, lying, flat, sleep (use duration guidelines)
- Movement: walk/run with {""target"":""floor"",""speed"":""walk|run""}
- Look: lookatuser, stoplooking
- Emoji: showemoji with emoji and optional duration

### Output Format
Generate a JSON array of 3-5 commands. Output ONLY the JSON array, no explanation.

## Realistic Dog Behavior Guidelines
- Prefer walk over run (80/20)
- Include lookatuser occasionally (~30%)
- Include emoji sometimes (~40%), usually one per sequence
- Natural transitions (sit before lying, not sleep directly)
- Durations: idle 2-5, sit 3-8, lying 5-15, flat 8-20, sleep 10-30
";

    public static string Build(SceneSnapshot snap, int maxVisible = 12, int maxMemory = 20)
    {
        var sb = new StringBuilder();
        sb.AppendLine(BasePrompt);

        sb.AppendLine("## Scene Context");
        sb.AppendLine($"Time: {snap.time:0.00}s");
        sb.AppendLine($"PetPose: pos=({R(snap.petPosition.x)},{R(snap.petPosition.y)},{R(snap.petPosition.z)}), rotY~{ApproxYaw(snap.petRotation):0.0}deg");

        // Visible anchors: visibilityScore desc
        var vis = (snap.visibleAnchors ?? new List<AnchorObservation>())
            .OrderByDescending(a => a.visibilityScore)
            .Take(maxVisible)
            .ToList();

        sb.AppendLine();
        sb.AppendLine("### Visible Anchors (what the pet currently sees)");
        if (vis.Count == 0) sb.AppendLine("- (none)");
        for (int i = 0; i < vis.Count; i++)
        {
            var a = vis[i];
            sb.AppendLine($"- {a.label} id={Short(a.id)} pos=({R(a.position.x)},{R(a.position.y)},{R(a.position.z)}) vis={a.visibilityScore:0.00} samples={a.visibleSamples}/{a.totalSamples}");
        }

        // Memory anchors: lastSeen desc
        var mem = (snap.memoryAnchors ?? new List<AnchorRecord>())
            .OrderByDescending(a => a.lastSeenTime)
            .Take(maxMemory)
            .ToList();

        sb.AppendLine();
        sb.AppendLine("### Memory Anchors (what the pet has discovered so far)");
        if (mem.Count == 0) sb.AppendLine("- (none)");
        for (int i = 0; i < mem.Count; i++)
        {
            var a = mem[i];
            float age = Mathf.Max(0f, snap.time - a.lastSeenTime);
            sb.AppendLine($"- {a.label} id={Short(a.id)} lastSeen={age:0.0}s ago pos=({R(a.position.x)},{R(a.position.y)},{R(a.position.z)}) seenCount={a.seenCount}");
        }

        sb.AppendLine();
        sb.AppendLine("## Task");
        sb.AppendLine("Generate a natural 3-5 command behavior sequence for the dog, considering the scene context above.");
        sb.AppendLine("Output only the JSON array.");

        return sb.ToString();
    }

    static string R(float f) => f.ToString("0.00");
    static string Short(string id) => string.IsNullOrEmpty(id) ? "null" : (id.Length <= 8 ? id : id.Substring(0, 8));
    static float ApproxYaw(Quaternion q)
    {
        // yaw approximation
        var e = q.eulerAngles;
        return e.y;
    }
}
