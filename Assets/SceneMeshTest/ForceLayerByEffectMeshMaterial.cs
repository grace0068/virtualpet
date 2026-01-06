using System;
using UnityEngine;
using Meta.XR.MRUtilityKit;

public class ForceLayerByEffectMeshMaterial : MonoBehaviour
{
    public string targetLayerName = "PetVision";
    public float enforceForSeconds = 5f;

    int targetLayer;
    Material targetMat;
    float startTime;

    void Awake()
    {
        targetLayer = LayerMask.NameToLayer(targetLayerName);
        if (targetLayer < 0)
            Debug.LogError($"Layer '{targetLayerName}' not found.");
    }

    void OnEnable()
    {
        startTime = Time.time;

        // 이 스크립트는 EffectMesh 오브젝트에 붙인다는 가정
        var em = GetComponent<EffectMesh>();
        if (em != null)
            targetMat = em.MeshMaterial;   // inspector의 Mesh Material
        else
            Debug.LogError("EffectMesh component not found on this object.");
    }

    void LateUpdate()
    {
        if (targetMat == null || targetLayer < 0) return;

        // EffectMesh가 런타임에 메쉬 오브젝트를 생성/갱신할 수 있어서
        // 시작 후 몇 초간 반복해서 적용
        if (Time.time - startTime > enforceForSeconds) return;

        Apply();
    }

    void Apply()
    {
        var renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        foreach (var r in renderers)
        {
            if (r == null) continue;

            // sharedMaterials 중 targetMat이 포함되면 레이어 강제
            var mats = r.sharedMaterials;
            if (mats == null) continue;

            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == targetMat)
                {
                    r.gameObject.layer = targetLayer;
                    break;
                }
            }
        }
    }
}
