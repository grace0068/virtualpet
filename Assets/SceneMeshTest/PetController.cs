using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PetController : MonoBehaviour
{
    public Transform petHead;         // (선택) 이동 기준을 머리(yaw)로 쓰고 싶으면 넣기
    public float moveSpeed = 1.5f;
    public float turnSpeedDeg = 120f;

    [Header("Tuning")]
    public bool moveRelativeToHeadYaw = false; // true면 petHead yaw 기준 이동
    public bool runWhenStickPressed = false;   // true면 스틱 클릭(키 I)로 달리기
    public float runMultiplier = 2f;

    CharacterController cc;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
    }

    void Update()
    {
        // --- Controller 입력 ---
        // Meta XR Simulator 바인딩:
        // Y/H/G/J -> Thumbstick
        // I -> Thumbstick Press
        // B -> A/X
        // N -> B/Y

        var l = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
        var r = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);
        Vector2 move = (l.sqrMagnitude >= r.sqrMagnitude) ? l : r;

        // 회전: B(A/X)=왼쪽, N(B/Y)=오른쪽
        float yaw = 0f;
        if (OVRInput.Get(OVRInput.Button.One)) yaw -= 1f; // A/X (B 키)
        if (OVRInput.Get(OVRInput.Button.Two)) yaw += 1f; // B/Y (N 키)

        // 달리기(선택): 스틱 클릭(I 키)
        float speedMul = 1f;
        if (runWhenStickPressed && OVRInput.Get(OVRInput.Button.PrimaryThumbstick))
            speedMul = runMultiplier;

        // 회전
        transform.Rotate(Vector3.up, yaw * turnSpeedDeg * Time.deltaTime);

        // 이동 방향 구성
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        if (moveRelativeToHeadYaw && petHead != null)
        {
            // 머리(yaw) 기준으로 평면 이동 (y 제거)
            forward = Vector3.ProjectOnPlane(petHead.forward, Vector3.up).normalized;
            right = Vector3.ProjectOnPlane(petHead.right, Vector3.up).normalized;
        }

        Vector3 dir = (forward * move.y + right * move.x);
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        // 중력(간단 처리)
        Vector3 velocity = dir * (moveSpeed * speedMul);
        velocity.y = -2f; // 바닥에 붙도록 약하게 아래로

        cc.Move(velocity * Time.deltaTime);
    }
}
