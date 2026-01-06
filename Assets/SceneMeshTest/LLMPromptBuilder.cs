using System.Text;
using UnityEngine;

namespace PetBrain
{
    public static class LLMPromptBuilder
    {
        // “맨 처음”에 넣을 System Prompt (고정)
        public static string BuildSystemPrompt()
        {
            // 핵심만 유지 + FIND / user input 반영 규칙 추가
            return
@"You are an AI system that generates commands for a virtual dog in a VR/AR home scene.

You must output ONLY a JSON array of 1~5 command objects. No markdown, no explanation.

Allowed commands:
- State: {""action"":""idle|sit|lying|flat|sleep"", ""duration"": number? }
- Look:  {""action"":""lookatuser""} or {""action"":""stoplooking""}
- Emoji: {""action"":""showemoji"", ""emoji"":""😊"", ""duration"": number? }

Special command:
- FIND:  {""action"":""find"", ""label"":""<target_label>""}

Rules:
1) If the user input includes a direct instruction (e.g. ""sit down"", ""sleep"", ""come here"", ""find the table""), prioritize it.
2) If the user requests to find something, output a FIND command (usually as the first command).
3) Otherwise, generate natural dog behavior (3~5 commands) with realistic durations.
4) Avoid weird transitions (don’t sleep immediately from idle; prefer sit -> lying -> sleep).
5) Output valid JSON array only.";
        }

        // 매 Think 때 “실시간 입력”을 만들기
        public static string BuildUserPrompt(global::SceneSnapshot snapshot, string userSpeech)
        {
            var sb = new StringBuilder(2048);

            sb.AppendLine("RUNTIME INPUT");
            sb.AppendLine($"time: {snapshot.time:0.00}");
            sb.AppendLine($"petPosition: {snapshot.petPosition}");
            sb.AppendLine();

            sb.AppendLine("user_speech:");
            sb.AppendLine(string.IsNullOrWhiteSpace(userSpeech) ? "(empty)" : userSpeech);
            sb.AppendLine();

            // Snapshot을 LLM이 이해하기 쉬운 텍스트로 요약
            sb.AppendLine("visible_anchors (top 12):");
            AppendAnchorsSummary(sb, snapshot.visibleAnchors, snapshot.petPosition, maxCount: 12);
            sb.AppendLine();

            sb.AppendLine("memory_anchors (top 20):");
            AppendAnchorsSummary(sb, snapshot.memoryAnchors, snapshot.petPosition, maxCount: 20);
            sb.AppendLine();

            sb.AppendLine("TASK: Output ONLY a JSON array of 1~5 commands.");

            return sb.ToString();
        }

        static void AppendAnchorsSummary(StringBuilder sb, System.Collections.IEnumerable anchors, Vector3 petPos, int maxCount)
        {
            if (anchors == null)
            {
                sb.AppendLine("- (none)");
                return;
            }

            int n = 0;
            foreach (var a in anchors)
            {
                if (a == null) continue;
                if (n >= maxCount) break;

                // AnchorObservation/AnchorRecord 내부 필드명 변화에 안전하도록 reflection 사용
                var label = AnchorReflection.GetString(a, "label") ?? AnchorReflection.GetString(a, "classification") ?? "unknown";
                var id = AnchorReflection.GetString(a, "id") ?? AnchorReflection.GetString(a, "stableKey") ?? "no-id";
                var pos = AnchorReflection.GetVector3(a, "position", Vector3.zero);
                var vis = AnchorReflection.GetFloat(a, "visibilityScore", -1f);

                var dist = Vector3.Distance(petPos, pos);
                if (vis >= 0f)
                    sb.AppendLine($"- {label} (id={id}) pos={Fmt(pos)} dist={dist:0.00} vis={vis:0.00}");
                else
                    sb.AppendLine($"- {label} (id={id}) pos={Fmt(pos)} dist={dist:0.00}");

                n++;
            }

            if (n == 0)
                sb.AppendLine("- (none)");
        }

        static string Fmt(Vector3 v) => $"({v.x:0.00},{v.y:0.00},{v.z:0.00})";

        // 공유 reflection helper
        internal static class AnchorReflection
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

            public static float GetFloat(object obj, string member, float fallback)
            {
                if (obj == null) return fallback;
                var t = obj.GetType();
                var f = t.GetField(member);
                if (f != null)
                {
                    var v = f.GetValue(obj);
                    if (v is float ff) return ff;
                    if (v is double dd) return (float)dd;
                    if (v is int ii) return ii;
                }
                var p = t.GetProperty(member);
                if (p != null)
                {
                    var v = p.GetValue(obj);
                    if (v is float ff) return ff;
                    if (v is double dd) return (float)dd;
                    if (v is int ii) return ii;
                }
                return fallback;
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
