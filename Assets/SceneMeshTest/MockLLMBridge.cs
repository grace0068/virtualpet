using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class MockLLMBridge : LLMBridgeBase
{
    [Range(0f, 1f)] public float useLookAtProb = 0.3f;
    [Range(0f, 1f)] public float useEmojiProb = 0.4f;
    [Range(0f, 1f)] public float runProb = 0.2f;

    public override Task<string> GenerateAsync(string prompt)
    {
        // prompt는 무시해도 되지만, 나중에 디버깅용으로 남겨둠.
        var cmds = new List<Dictionary<string, object>>();

        bool useLook = UnityEngine.Random.value < useLookAtProb;
        bool useEmoji = UnityEngine.Random.value < useEmojiProb;

        // 3~5개
        int n = UnityEngine.Random.Range(3, 6);

        // 시작: 이동을 넣을 확률
        bool startMove = UnityEngine.Random.value < 0.7f;
        if (startMove)
        {
            bool run = UnityEngine.Random.value < runProb;
            cmds.Add(new Dictionary<string, object> {
                {"action", run ? "run" : "walk"},
                {"target","floor"},
                {"speed", run ? "run" : "walk"}
            });
        }

        if (useLook) cmds.Add(new Dictionary<string, object> {{"action","lookatuser"}});

        if (useEmoji)
        {
            string[] emojis = new [] {"😊","🥰","👀","🤔","😴","🎾","❤️","🐾"};
            cmds.Add(new Dictionary<string, object> {
                {"action","showemoji"},
                {"emoji", emojis[UnityEngine.Random.Range(0, emojis.Length)]},
                {"duration", UnityEngine.Random.Range(2f, 4f)}
            });
        }

        // 나머지 상태 전이: sit/lying/flat/sleep 중 자연스럽게
        while (cmds.Count < n)
        {
            string next = PickState(cmds);
            var d = PickDuration(next);
            var obj = new Dictionary<string, object> {{"action", next}};
            if (d > 0f) obj["duration"] = d;
            cmds.Add(obj);

            // occasionally stoplooking
            if (useLook && UnityEngine.Random.value < 0.3f && cmds.Count < n)
                cmds.Add(new Dictionary<string, object> {{"action","stoplooking"}});
        }

        // JSON array string으로 직렬화(간단)
        string json = ToJsonArray(cmds);
        return Task.FromResult(json);
    }

    static string PickState(List<Dictionary<string, object>> sofar)
    {
        // 너무 급격히 sleep으로 가지 않게
        bool hasSit = ContainsAction(sofar, "sit");
        bool hasLying = ContainsAction(sofar, "lying");
        bool hasFlat = ContainsAction(sofar, "flat");

        if (!hasSit) return "sit";
        if (!hasLying) return UnityEngine.Random.value < 0.7f ? "lying" : "idle";
        if (!hasFlat) return UnityEngine.Random.value < 0.5f ? "flat" : "lying";
        return UnityEngine.Random.value < 0.5f ? "idle" : "sleep";
    }

    static float PickDuration(string action)
    {
        return action switch
        {
            "idle" => UnityEngine.Random.Range(2f, 5f),
            "sit" => UnityEngine.Random.Range(3f, 8f),
            "lying" => UnityEngine.Random.Range(5f, 15f),
            "flat" => UnityEngine.Random.Range(8f, 20f),
            "sleep" => UnityEngine.Random.Range(10f, 25f),
            _ => 0f
        };
    }

    static bool ContainsAction(List<Dictionary<string, object>> sofar, string a)
    {
        for (int i = 0; i < sofar.Count; i++)
            if (sofar[i].TryGetValue("action", out var v) && (string)v == a) return true;
        return false;
    }

    static string ToJsonArray(List<Dictionary<string, object>> cmds)
    {
        var sb = new StringBuilder();
        sb.Append("[");
        for (int i = 0; i < cmds.Count; i++)
        {
            if (i > 0) sb.Append(",");
            sb.Append("{");
            bool first = true;
            foreach (var kv in cmds[i])
            {
                if (!first) sb.Append(",");
                first = false;

                sb.Append("\"").Append(kv.Key).Append("\":");
                if (kv.Value is string s) sb.Append("\"").Append(s.Replace("\"","\\\"")).Append("\"");
                else if (kv.Value is float f) sb.Append(f.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
                else if (kv.Value is double d) sb.Append(d.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
                else sb.Append("\"").Append(kv.Value.ToString()).Append("\"");
            }
            sb.Append("}");
        }
        sb.Append("]");
        return sb.ToString();
    }
}
