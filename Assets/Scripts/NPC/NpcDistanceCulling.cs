using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// NPC'lerin uzaklığa göre çizim maliyetini düşürür: kameradan <see cref="shadowDistance"/>'tan uzak parçaların
/// gölgesini, <see cref="hideDistance"/>'tan uzak parçaları tamamen kapatır (renderer kapanınca animator
/// <c>CullUpdateTransforms</c> ile kemikleri de güncellemez). Ölçüm (cadde başı): gölge ~0.6 ms, 80 m gizleme ~1.75 ms.
/// NPC'lerin kök objesine (sahnede <c>NPC_Alani</c>) eklenir; altındaki tüm skinned renderer'lara bakar,
/// kalabalığın sonradan doğurduğu NPC'ler ve günlük kıyafetle açılan parçalar da yenilemede yakalanır.
/// Gölgeye sadece başta gölge atan renderer'larda dokunulur.
/// </summary>
public class NpcDistanceCulling : MonoBehaviour
{
    [SerializeField] private float shadowDistance = 20f;
    [SerializeField] private float hideDistance = 80f;
    [Tooltip("Gizleme sınırında titremesin diye: gizli NPC bu kadar yaklaşınca görünür olur.")]
    [SerializeField] private float hideHysteresis = 5f;
    [Tooltip("Kontrol aralığı (sn).")]
    [SerializeField] private float interval = 0.25f;
    [Tooltip("Renderer listesinin yenilenme aralığı (sn).")]
    [SerializeField] private float refreshInterval = 1f;

    private readonly List<SkinnedMeshRenderer> scratch = new List<SkinnedMeshRenderer>();
    private readonly List<SkinnedMeshRenderer> renderers = new List<SkinnedMeshRenderer>();
    private readonly HashSet<SkinnedMeshRenderer> known = new HashSet<SkinnedMeshRenderer>();
    private readonly HashSet<SkinnedMeshRenderer> casters = new HashSet<SkinnedMeshRenderer>();
    private float nextCheck, nextRefresh;

    private void OnDisable()
    {
        foreach (SkinnedMeshRenderer r in renderers)
        {
            if (r == null)
                continue;
            r.enabled = true;
            if (casters.Contains(r))
                r.shadowCastingMode = ShadowCastingMode.On;
        }
        renderers.Clear();
        known.Clear();
        casters.Clear();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextCheck)
            return;
        nextCheck = Time.unscaledTime + interval;

        if (Time.unscaledTime >= nextRefresh)
        {
            nextRefresh = Time.unscaledTime + refreshInterval;
            Refresh();
        }

        Camera cam = Camera.main;
        if (cam == null)
            return;

        Vector3 eye = cam.transform.position;
        float shadowSqr = shadowDistance * shadowDistance;
        float hideSqr = hideDistance * hideDistance;
        float showDistance = Mathf.Max(0f, hideDistance - hideHysteresis);
        float showSqr = showDistance * showDistance;
        foreach (SkinnedMeshRenderer r in renderers)
        {
            if (r == null)
                continue;

            // Kapalı renderer'ın bounds'u güncellenmiyor; konumu kökten (NPC'yle birlikte yürüyen kemik) al.
            Transform anchor = r.rootBone != null ? r.rootBone : r.transform;
            float distSqr = (anchor.position - eye).sqrMagnitude;
            if (r.enabled ? distSqr > hideSqr : distSqr < showSqr)
                r.enabled = !r.enabled;

            if (casters.Contains(r))
                r.shadowCastingMode = distSqr <= shadowSqr ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }
    }

    private void Refresh()
    {
        renderers.RemoveAll(r => r == null);
        known.RemoveWhere(r => r == null);
        casters.RemoveWhere(r => r == null);
        GetComponentsInChildren(false, scratch);
        foreach (SkinnedMeshRenderer r in scratch)
        {
            if (!known.Add(r))
                continue;
            renderers.Add(r);
            if (r.shadowCastingMode == ShadowCastingMode.On)
                casters.Add(r);
        }
    }
}
