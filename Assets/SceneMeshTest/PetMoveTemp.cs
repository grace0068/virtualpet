using System.Collections;
using UnityEngine;

namespace PetBrain
{
    public class PetMoveTemp : MonoBehaviour
    {
        [Header("Temp action timings")]
        public float defaultActionSeconds = 3f;
        public float searchSpeed = 1.0f;
        public float foundSpeed = 2.5f;

        bool _busy;

        public bool IsBusy => _busy;

        public void QueueSearchCommand(Vector3 destination)
        {
            if (_busy) return;
            StartCoroutine(MoveRoutine("search", destination, searchSpeed));
        }

        public void QueueObjectFoundCommand(Vector3 destination)
        {
            if (_busy) return;
            StartCoroutine(MoveRoutine("objectfound", destination, foundSpeed));
        }

        public void QueueObjectNotFoundCommand()
        {
            if (_busy) return;
            StartCoroutine(WaitRoutine("objectnotfound", defaultActionSeconds));
        }

        public void QueueShowEmojiCommand(string emoji, float duration = 2.5f)
        {
            Debug.Log($"[PetMoveTemp] showemoji {emoji} for {duration:0.0}s");
            // 네 temp 구현이 “큐 기반 + 일정 시간 후 ActionFinished 이벤트”라면
            // 여기서도 같은 큐에 넣어 duration만큼 실행되도록 처리하면 됨.
        }

        public void QueueDebugAction(string action, float durationSeconds)
        {
            if (_busy) return;
            StartCoroutine(WaitRoutine(action, Mathf.Max(0.5f, durationSeconds)));
        }

        IEnumerator WaitRoutine(string action, float seconds)
        {
            _busy = true;
            Debug.Log($"[PetMoveTemp] {action} (wait {seconds:0.0}s)");
            yield return new WaitForSeconds(seconds);
            _busy = false;
            PetBrainEvents.RaiseActionCompleted(action, success: true);
        }

        IEnumerator MoveRoutine(string action, Vector3 destination, float speed)
        {
            _busy = true;
            Debug.Log($"[PetMoveTemp] {action} -> {destination} (speed {speed:0.0})");

            float maxSeconds = Mathf.Max(1f, defaultActionSeconds);
            float t0 = Time.time;

            // 아주 단순 이동 (NavMesh 없이)
            while (Time.time - t0 < maxSeconds)
            {
                var pos = transform.position;
                var to = destination - pos;
                to.y = 0f;

                if (to.magnitude < 0.05f)
                    break;

                var step = speed * Time.deltaTime;
                var delta = Vector3.ClampMagnitude(to, step);
                transform.position = pos + delta;

                if (delta.sqrMagnitude > 0.0001f)
                {
                    var forward = delta.normalized;
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(forward, Vector3.up), 8f * Time.deltaTime);
                }

                yield return null;
            }

            _busy = false;
            PetBrainEvents.RaiseActionCompleted(action, success: true, destination: destination);
        }
    }
}
