using UnityEngine;

namespace PetBrain
{
    public class PetActionDispatcher : MonoBehaviour
    {
        [Header("Temp action executor")]
        public PetMoveTemp tempPetMove; // assign or auto-find

        [Header("Force temp even if real PetMove exists")]
        public bool forceTemp = true;

        void Awake()
        {
            if (tempPetMove == null)
                tempPetMove = FindFirstObjectByType<PetMoveTemp>();
        }

        public void ExecuteFindDecision(FindDecision d)
        {
            EnsureTemp();

            switch (d.type)
            {
                case FindDecisionType.Search:
                    Debug.Log($"[Dispatcher] FIND/Search: {d.destination} ({d.note})");
                    tempPetMove.QueueSearchCommand(d.destination);
                    break;

                case FindDecisionType.ObjectFound:
                    Debug.Log($"[Dispatcher] FIND/Found: {d.destination} ({d.note})");
                    tempPetMove.QueueObjectFoundCommand(d.destination);
                    break;

                case FindDecisionType.ObjectNotFound:
                    Debug.Log($"[Dispatcher] FIND/NotFound ({d.note})");
                    tempPetMove.QueueObjectNotFoundCommand();
                    break;

                default:
                    // nothing
                    break;
            }
        }

        public void ExecuteCommand(BrainCommand cmd)
        {
            EnsureTemp();

            var a = (cmd.action ?? "").Trim().ToLowerInvariant();

            // FIND는 orchestrator가 처리 (여기선 무시)
            if (a == "find") return;

            // 지금 단계: 다른 명령들은 “로그 + 대기”로 처리
            float dur = cmd.duration;
            if (dur <= 0.01f)
            {
                // state 커맨드는 적당히 기본 duration
                if (a == "idle") dur = 3f;
                else if (a == "sit") dur = 5f;
                else if (a == "lying") dur = 8f;
                else if (a == "flat") dur = 10f;
                else if (a == "sleep") dur = 15f;
                else dur = 2.5f;
            }

            string extra = "";
            if (!string.IsNullOrWhiteSpace(cmd.emoji)) extra = $" emoji={cmd.emoji}";
            if (!string.IsNullOrWhiteSpace(cmd.label)) extra += $" label={cmd.label}";
            Debug.Log($"[Dispatcher] Command: {cmd.action}{extra} (dur {dur:0.0}s)");

            tempPetMove.QueueDebugAction(cmd.action, dur);
        }

        void EnsureTemp()
        {
            if (tempPetMove == null)
                tempPetMove = FindFirstObjectByType<PetMoveTemp>();

            if (tempPetMove == null)
            {
                // 마지막 수단: dispatcher가 붙은 오브젝트에 임시로 붙임
                tempPetMove = gameObject.AddComponent<PetMoveTemp>();
                Debug.LogWarning("[Dispatcher] PetMoveTemp was missing; added to Dispatcher object.");
            }
        }
    }
}
