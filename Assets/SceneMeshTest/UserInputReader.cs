using System.IO;
using UnityEngine;

namespace PetBrain
{
    public static class UserInputReader
    {
        // persistentDataPath/UserInput.txt
        public static string ConsumeUserInput(string fileName = "UserInput.txt")
        {
            var path = Path.Combine(Application.persistentDataPath, fileName);

            try
            {
                if (!File.Exists(path))
                    return "";

                var text = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(text))
                    return "";

                // consume once -> clear file
                File.WriteAllText(path, "");
                return text.Trim();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[UserInputReader] Failed to read/clear {path}: {e.Message}");
                return "";
            }
        }
    }
}
