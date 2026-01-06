using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class OllamaGenerateRequest
{
    public string model;
    public string prompt;
    public string system;
    public bool stream = false;
}

[Serializable]
public class OllamaGenerateResponse
{
    public string response;
}

public abstract class LLMBridgeBase : MonoBehaviour
{
    public abstract Task<string> GenerateAsync(string prompt, string system = null);
}

public class OllamaLLMBridge : LLMBridgeBase
{
    [Header("Ollama")]
    public string baseUrl = "http://localhost:11434";
    public string model = "llama3.2";

    public override async Task<string> GenerateAsync(string prompt, string system = null)
    {
        var url = $"{baseUrl.TrimEnd('/')}/api/generate";

        var reqObj = new OllamaGenerateRequest
        {
            model = model,
            prompt = prompt,
            system = system,
            stream = false
        };

        var json = JsonUtility.ToJson(reqObj);
        using var req = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        var op = req.SendWebRequest();
        while (!op.isDone) await Task.Yield();

        if (req.result != UnityWebRequest.Result.Success)
            throw new Exception($"Ollama request failed: {req.error}");

        // Ollama 응답 JSON에서 response 필드만 꺼냄
        var respJson = req.downloadHandler.text;
        var resp = JsonUtility.FromJson<OllamaGenerateResponse>(respJson);
        return resp?.response ?? "";
    }
}
