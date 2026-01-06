using System;
using UnityEngine;

namespace PetBrain
{
    public static class PetBrainEvents
    {
        public static event Action<PetActionResult> ActionCompleted;

        public static void RaiseActionCompleted(string action, bool success = true, Vector3 destination = default, string note = null)
        {
            ActionCompleted?.Invoke(new PetActionResult
            {
                action = action,
                success = success,
                destination = destination,
                note = note
            });
        }
    }
}
