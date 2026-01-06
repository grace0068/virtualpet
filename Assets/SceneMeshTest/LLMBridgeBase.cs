using System.Threading.Tasks;
using UnityEngine;

public abstract class LLMBridgeBase : MonoBehaviour
{
    public abstract Task<string> GenerateAsync(string prompt);
}
