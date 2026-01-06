using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace PetBrain
{
    public static class LLMResponseParser
    {
        public static bool TryParseCommands(string llmText, out List<BrainCommand> commands, out string error)
        {
            commands = new List<BrainCommand>();
            error = null;

            if (string.IsNullOrWhiteSpace(llmText))
            {
                error = "empty response";
                return false;
            }

            // 흔한 케이스: ```json ... ```
            var trimmed = llmText.Trim();
            trimmed = StripCodeFences(trimmed);

            // 1) JSON array 찾기: [ ... ]
            var arrayJson = ExtractJsonArray(trimmed);
            if (arrayJson == null)
            {
                // 2) 혹시 { "commands": [ ... ] } 형태면 commands array 뽑기
                arrayJson = ExtractCommandsArrayFromObject(trimmed);
            }

            if (arrayJson == null)
            {
                error = "No JSON array found.";
                return false;
            }

            try
            {
                var objs = JsonLite.ParseArrayOfObjects(arrayJson);
                foreach (var obj in objs)
                {
                    var cmd = new BrainCommand();
                    if (obj.TryGetValue("action", out var actionObj))
                        cmd.action = (actionObj as string)?.Trim();
                    if (obj.TryGetValue("emoji", out var emojiObj))
                        cmd.emoji = emojiObj as string;
                    if (obj.TryGetValue("label", out var labelObj))
                        cmd.label = labelObj as string;
                    if (obj.TryGetValue("target", out var targetObj))
                        cmd.target = targetObj as string;
                    if (obj.TryGetValue("speed", out var speedObj))
                        cmd.speed = speedObj as string;

                    if (obj.TryGetValue("duration", out var durObj))
                        cmd.duration = JsonLite.AsFloat(durObj, 0f);

                    if (string.IsNullOrWhiteSpace(cmd.action))
                        continue;

                    commands.Add(cmd);
                }

                if (commands.Count == 0)
                {
                    error = "Parsed but no valid commands.";
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                error = $"Parse exception: {e.Message}";
                return false;
            }
        }

        static string StripCodeFences(string s)
        {
            if (s.StartsWith("```"))
            {
                int firstNl = s.IndexOf('\n');
                if (firstNl >= 0) s = s.Substring(firstNl + 1);
                int last = s.LastIndexOf("```", StringComparison.Ordinal);
                if (last >= 0) s = s.Substring(0, last);
            }
            return s.Trim();
        }

        static string ExtractJsonArray(string s)
        {
            int l = s.IndexOf('[');
            int r = s.LastIndexOf(']');
            if (l < 0 || r < 0 || r <= l) return null;
            return s.Substring(l, r - l + 1);
        }

        static string ExtractCommandsArrayFromObject(string s)
        {
            // 아주 단순 탐색: "commands" 키 이후 첫 '['부터 마지막 ']'까지
            int idx = s.IndexOf("\"commands\"", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) idx = s.IndexOf("'commands'", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;

            int l = s.IndexOf('[', idx);
            int r = s.LastIndexOf(']');
            if (l < 0 || r < 0 || r <= l) return null;
            return s.Substring(l, r - l + 1);
        }

        // ---- Minimal JSON parser (array of flat objects) ----
        static class JsonLite
        {
            public static float AsFloat(object v, float fallback)
            {
                if (v == null) return fallback;
                if (v is float ff) return ff;
                if (v is double dd) return (float)dd;
                if (v is int ii) return ii;
                if (v is long ll) return ll;
                if (v is string s && float.TryParse(s, out var f)) return f;
                return fallback;
            }

            public static List<Dictionary<string, object>> ParseArrayOfObjects(string json)
            {
                int i = 0;
                SkipWs(json, ref i);
                Expect(json, ref i, '[');
                var list = new List<Dictionary<string, object>>();

                while (true)
                {
                    SkipWs(json, ref i);
                    if (Peek(json, i) == ']')
                    {
                        i++;
                        break;
                    }

                    var obj = ParseObject(json, ref i);
                    list.Add(obj);

                    SkipWs(json, ref i);
                    var c = Peek(json, i);
                    if (c == ',')
                    {
                        i++;
                        continue;
                    }
                    if (c == ']')
                    {
                        i++;
                        break;
                    }

                    throw new Exception("Expected ',' or ']'");
                }

                return list;
            }

            static Dictionary<string, object> ParseObject(string json, ref int i)
            {
                SkipWs(json, ref i);
                Expect(json, ref i, '{');

                var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                while (true)
                {
                    SkipWs(json, ref i);
                    if (Peek(json, i) == '}')
                    {
                        i++;
                        break;
                    }

                    var key = ParseString(json, ref i);
                    SkipWs(json, ref i);
                    Expect(json, ref i, ':');
                    SkipWs(json, ref i);

                    var val = ParseValue(json, ref i);
                    dict[key] = val;

                    SkipWs(json, ref i);
                    var c = Peek(json, i);
                    if (c == ',')
                    {
                        i++;
                        continue;
                    }
                    if (c == '}')
                    {
                        i++;
                        break;
                    }

                    throw new Exception("Expected ',' or '}'");
                }

                return dict;
            }

            static object ParseValue(string json, ref int i)
            {
                SkipWs(json, ref i);
                char c = Peek(json, i);

                if (c == '"') return ParseString(json, ref i);
                if (c == '-' || char.IsDigit(c)) return ParseNumber(json, ref i);

                // true/false/null
                if (Match(json, ref i, "true")) return true;
                if (Match(json, ref i, "false")) return false;
                if (Match(json, ref i, "null")) return null;

                // fallback: bareword until delimiter
                return ParseBareword(json, ref i);
            }

            static string ParseBareword(string json, ref int i)
            {
                var sb = new StringBuilder();
                while (i < json.Length)
                {
                    char c = json[i];
                    if (char.IsWhiteSpace(c) || c == ',' || c == '}' || c == ']') break;
                    sb.Append(c);
                    i++;
                }
                return sb.ToString();
            }

            static double ParseNumber(string json, ref int i)
            {
                int start = i;
                if (json[i] == '-') i++;
                while (i < json.Length && char.IsDigit(json[i])) i++;
                if (i < json.Length && json[i] == '.')
                {
                    i++;
                    while (i < json.Length && char.IsDigit(json[i])) i++;
                }
                if (i < json.Length && (json[i] == 'e' || json[i] == 'E'))
                {
                    i++;
                    if (i < json.Length && (json[i] == '+' || json[i] == '-')) i++;
                    while (i < json.Length && char.IsDigit(json[i])) i++;
                }

                var s = json.Substring(start, i - start);
                if (double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d))
                    return d;

                throw new Exception($"Bad number: {s}");
            }

            static string ParseString(string json, ref int i)
            {
                Expect(json, ref i, '"');
                var sb = new StringBuilder();
                while (i < json.Length)
                {
                    char c = json[i++];
                    if (c == '"') break;
                    if (c == '\\')
                    {
                        if (i >= json.Length) break;
                        char e = json[i++];
                        switch (e)
                        {
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case '/': sb.Append('/'); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case 'u':
                                if (i + 4 <= json.Length)
                                {
                                    var hex = json.Substring(i, 4);
                                    if (ushort.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var code))
                                        sb.Append((char)code);
                                    i += 4;
                                }
                                break;
                            default:
                                sb.Append(e);
                                break;
                        }
                    }
                    else sb.Append(c);
                }
                return sb.ToString();
            }

            static void SkipWs(string json, ref int i)
            {
                while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            }

            static char Peek(string json, int i) => (i < json.Length) ? json[i] : '\0';

            static void Expect(string json, ref int i, char c)
            {
                if (Peek(json, i) != c) throw new Exception($"Expected '{c}'");
                i++;
            }

            static bool Match(string json, ref int i, string token)
            {
                SkipWs(json, ref i);
                if (i + token.Length > json.Length) return false;
                for (int k = 0; k < token.Length; k++)
                    if (char.ToLowerInvariant(json[i + k]) != char.ToLowerInvariant(token[k]))
                        return false;
                i += token.Length;
                return true;
            }
        }
    }
}
