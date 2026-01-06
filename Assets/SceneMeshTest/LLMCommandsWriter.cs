using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public static class LLMCommandsWriter
{
    // 기본: persistentDataPath/LLMCommands.json
    public static string ResolveDefaultPath(string fileName = "LLMCommands.json")
        => Path.Combine(Application.persistentDataPath, fileName);

    // Editor에서 Assets/...로 쓰고 싶으면 fullPath로 변환해줌
    public static string ResolveAssetsPath(string assetsRelativePath)
    {
        // assetsRelativePath 예: "Assets/Code/Scripts/LLMCommands.json"
        var projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(projectRoot, assetsRelativePath);
    }

    public static void WriteCommands(string fullPath, List<LLMCommand> commands)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

        // 항상 Format2: {"commands":[...]} 로 저장
        var sb = new StringBuilder();
        sb.Append("{\"commands\":[");
        for (int i = 0; i < commands.Count; i++)
        {
            if (i > 0) sb.Append(",");
            AppendCommandObject(sb, commands[i]);
        }
        sb.Append("]}");

        AtomicWrite(fullPath, sb.ToString());
    }

    static void AppendCommandObject(StringBuilder sb, LLMCommand c)
    {
        sb.Append("{");

        // action (required)
        AppendString(sb, "action", c.action, first: true);

        // movement coords (optional)
        AppendFloat(sb, "x", c.x);
        AppendFloat(sb, "y", c.y);
        AppendFloat(sb, "z", c.z);

        // duration (optional)
        AppendFloat(sb, "duration", c.duration);

        // emoji (optional)
        AppendString(sb, "emoji", c.emoji);

        sb.Append("}");
    }

    static void AppendString(StringBuilder sb, string key, string value, bool first = false)
    {
        if (string.IsNullOrEmpty(value)) return;
        if (!first) sb.Append(",");
        sb.Append("\"").Append(key).Append("\":");
        sb.Append("\"").Append(Escape(value)).Append("\"");
    }

    static void AppendFloat(StringBuilder sb, string key, float? value)
    {
        if (!value.HasValue) return;
        sb.Append(",");
        sb.Append("\"").Append(key).Append("\":");
        sb.Append(value.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
    }

    static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    static void AtomicWrite(string path, string content)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content, Encoding.UTF8);
        File.Copy(tmp, path, true);
        File.Delete(tmp);
    }
}
