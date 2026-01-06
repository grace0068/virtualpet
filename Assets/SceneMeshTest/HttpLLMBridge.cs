using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class HttpLLMBridge : LLMBridgeBase
{
    [Header("Example: http://127.0.0.1:8000/generate")]
    public string endpoint = "http://127.0.0.1:8000/generate";
    public int timeoutSeconds = 30;

    public override async Task<string> GenerateAsync(string prompt)
    {
        // 서버에 {"prompt":"..."} 형태로 보냄
        var payload = "{\"prompt\":" + JsonEscape(prompt) + "}";
        using var req = new UnityWebRequest(endpoint, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = timeoutSeconds;

        var op = req.SendWebRequest();
        while (!op.isDone) await Task.Yield();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning("[HttpLLMBridge] Request failed: " + req.error);
            return "[]";
        }

        // 서버는 그냥 JSON array 문자열을 body로 주거나,
        // {"output":"[...]"} 형태로 줄 수도 있어서 둘 다 허용
        var text = req.downloadHandler.text?.Trim();
        if (string.IsNullOrEmpty(text)) return "[]";
        return text;
    }

    static string JsonEscape(string s)
    {
        // "..." 형태로 감싼 JSON string을 반환
        if (s == null) return "\"\"";
        s = s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        return "\"" + s + "\"";
    }
}
