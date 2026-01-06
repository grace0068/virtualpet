using System;
using System.Collections.Generic;
using MiniJSON;

[Serializable]
public class LLMCommand
{
    public string action;

    public float? x;
    public float? y;
    public float? z;

    public float? duration;
    public string emoji;

    // LLM이 {"target":"floor","speed":"walk"} 형태로 주는 경우용
    public string target;
    public string speed;
}

public static class LLMCommandParser
{
    // LLM output: JSON Array 또는 {"commands":[...]} 둘 다 허용
    public static List<LLMCommand> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<LLMCommand>();

        json = json.Trim();

        // 혹시 LLM이 앞뒤에 설명을 붙이면 배열 부분만 잘라보기
        if (!json.StartsWith("[") && !json.StartsWith("{"))
        {
            int s = json.IndexOf('[');
            int e = json.LastIndexOf(']');
            if (s >= 0 && e > s) json = json.Substring(s, e - s + 1);
        }

        object rootObj = null;
        try
        {
            rootObj = Json.Deserialize(json);
        }
        catch
        {
            // 마지막 수단
            int s = json.IndexOf('[');
            int e = json.LastIndexOf(']');
            if (s >= 0 && e > s)
            {
                var sliced = json.Substring(s, e - s + 1);
                rootObj = Json.Deserialize(sliced);
            }
        }

        if (rootObj is List<object> arr)
            return ParseArray(arr);

        if (rootObj is Dictionary<string, object> obj)
        {
            if (obj.TryGetValue("commands", out var commandsObj) && commandsObj is List<object> arr2)
                return ParseArray(arr2);
        }

        return new List<LLMCommand>();
    }

    static List<LLMCommand> ParseArray(List<object> arr)
    {
        var list = new List<LLMCommand>();

        foreach (var el in arr)
        {
            if (el is not Dictionary<string, object> dict) continue;

            var c = new LLMCommand();
            c.action = GetString(dict, "action");

            c.x = GetFloat(dict, "x");
            c.y = GetFloat(dict, "y");
            c.z = GetFloat(dict, "z");

            c.duration = GetFloat(dict, "duration");
            c.emoji = GetString(dict, "emoji");

            c.target = GetString(dict, "target");
            c.speed  = GetString(dict, "speed");

            if (!string.IsNullOrWhiteSpace(c.action))
                list.Add(c);
        }

        return list;
    }

    static string GetString(Dictionary<string, object> dict, string key)
    {
        if (!dict.TryGetValue(key, out var v) || v == null) return null;
        return v as string;
    }

    static float? GetFloat(Dictionary<string, object> dict, string key)
    {
        if (!dict.TryGetValue(key, out var v) || v == null) return null;

        if (v is double d) return (float)d;
        if (v is long l) return (float)l;
        if (v is int i) return i;

        if (v is string s && float.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var f))
            return f;

        return null;
    }
}
