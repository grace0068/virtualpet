using System;
using UnityEngine;

namespace PetBrain
{
    [Serializable]
    public class BrainCommand
    {
        public string action;     // required
        public float duration;    // optional
        public string emoji;      // optional
        public string label;      // for find
        public string target;     // optional (e.g., "floor")
        public string speed;      // optional (e.g., "walk"/"run")
    }

    public enum FindDecisionType
    {
        None = 0,
        Search = 1,
        ObjectFound = 2,
        ObjectNotFound = 3
    }

    public struct FindDecision
    {
        public FindDecisionType type;
        public Vector3 destination;
        public string note;

        public static FindDecision None() => new FindDecision { type = FindDecisionType.None };
        public static FindDecision Search(Vector3 d, string note = null) => new FindDecision { type = FindDecisionType.Search, destination = d, note = note };
        public static FindDecision Found(Vector3 d, string note = null) => new FindDecision { type = FindDecisionType.ObjectFound, destination = d, note = note };
        public static FindDecision NotFound(string note = null) => new FindDecision { type = FindDecisionType.ObjectNotFound, destination = Vector3.zero, note = note };
    }

    public struct PetActionResult
    {
        public string action;
        public bool success;
        public Vector3 destination;
        public string note;
    }
}
