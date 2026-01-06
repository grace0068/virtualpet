using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Android;
using Meta.XR.MRUtilityKit;

public class MRUKBootstrap : MonoBehaviour
{
    [Header("Editor/Simulator fallback (pick one at random)")]
    public GameObject[] fallbackRoomPrefabs;

    [Header("Device load settings")]
    public bool removeMissingRooms = true;

    async void Start()
    {
        // 1) MRUK singleton 준비 대기
        while (MRUK.Instance == null)
            await Task.Yield();

#if UNITY_EDITOR
        // Editor + XR Simulator:
        // Simulator가 제공하는 "device scene"을 먼저 로드 시도해서,
        // 배경(synthetic env)과 MRUK/EffectMesh가 최대한 같은 데이터 기반이 되게 함.
        var editorResult = await MRUK.Instance.LoadSceneFromDevice(
            requestSceneCaptureIfNoDataFound: false,   // Editor에서 Space Setup 같은 걸 띄우지 않도록
            removeMissingRooms: removeMissingRooms
        );

        if (editorResult == MRUK.LoadDeviceResult.Success)
        {
            Debug.Log("[MRUKBootstrap] Editor/XR Simulator device scene loaded!");
            return;
        }

        Debug.LogWarning($"[MRUKBootstrap] Editor LoadSceneFromDevice failed: {editorResult}. Falling back to prefab.");
        LoadRandomFallbackPrefab();
        return;

#else
        // 2) 런타임 Scene permission 요청 (Spatial Data Permission)
        if (!Permission.HasUserAuthorizedPermission(OVRPermissionsRequester.ScenePermission))
        {
            var callbacks = new PermissionCallbacks();
            bool done = false;

            callbacks.PermissionGranted += _ => done = true;
            callbacks.PermissionDenied += _ => done = true;
            callbacks.PermissionDeniedAndDontAskAgain += _ => done = true;

            Permission.RequestUserPermission(OVRPermissionsRequester.ScenePermission, callbacks);
            while (!done) await Task.Yield();
        }

        // 3) Device scene 로드 시도 (없으면 Space Setup 자동 시작)
        var result = await MRUK.Instance.LoadSceneFromDevice(
            requestSceneCaptureIfNoDataFound: true,
            removeMissingRooms: removeMissingRooms
        );

        if (result == MRUK.LoadDeviceResult.Success)
        {
            Debug.Log("[MRUKBootstrap] Device scene loaded!");
            return;
        }

        Debug.LogWarning($"[MRUKBootstrap] LoadSceneFromDevice failed: {result}");

        // (선택) 기기에서도 폴백 룸 로드
        LoadRandomFallbackPrefab();
#endif
    }

    void LoadRandomFallbackPrefab()
    {
        if (fallbackRoomPrefabs == null || fallbackRoomPrefabs.Length == 0)
        {
            Debug.LogError("[MRUKBootstrap] No fallbackRoomPrefabs assigned.");
            return;
        }

        // null 아닌 것만 골라서 랜덤 선택
        int tries = fallbackRoomPrefabs.Length;
        GameObject chosen = null;

        for (int i = 0; i < tries; i++)
        {
            int idx = Random.Range(0, fallbackRoomPrefabs.Length);
            if (fallbackRoomPrefabs[idx] != null)
            {
                chosen = fallbackRoomPrefabs[idx];
                break;
            }
        }

        if (chosen == null)
        {
            Debug.LogError("[MRUKBootstrap] All fallbackRoomPrefabs are null.");
            return;
        }

        Debug.Log($"[MRUKBootstrap] Loading fallback prefab: {chosen.name}");
        MRUK.Instance.LoadSceneFromPrefab(chosen, clearSceneFirst: true);
    }
}
