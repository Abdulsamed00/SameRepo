using UnityEngine;

/// <summary>
/// NPC'lerin iki küçük IK düzeltmesi (10.11, kullanıcı listesi). Animator'ın temel katmanında IK Pass açık olmalı
/// (<c>NPC_Man</c>/<c>NPC_Women</c> controller'ları).
/// <list type="bullet">
/// <item><b>Ayak zemine gömülmesin:</b> kök zemine (NavMesh/raycast) basıyor ama ayaklar klipten gelir: kaldırım kenarında
/// yürürken bir ayak 0.12 m'lik kaldırımın içine, bankta (Bench_Sit, ayaklar önde) ayaklar 3-10 cm kaldırıma/yola
/// giriyordu. Ayak tabanı (bilek hedefi - <see cref="Animator.leftFeetBottomHeight"/>) altındaki zeminden aşağıdaysa ayak
/// hedefi o kadar yukarı alınır; zeminin üstündeyse IK kapalı, animasyon aynen oynar.</item>
/// <item><b>Çanta elin içinden geçmesin:</b> çanta (<c>Bag</c>, tek kemiğe, sağ köprücüğe bağlı) sağ kalçada asılı, yürürken
/// sallanan sağ el içinden geçiyordu. Çanta oyuncudaki gibi gövdeye (Spine1) bağlanır, sağ el çantanın üstüne konur
/// (çantasını tutan kadın): el çantayla birlikte gider, kol sallanmaz.</item>
/// </list>
/// </summary>
[RequireComponent(typeof(Animator))]
public class NpcIk : MonoBehaviour
{
    [SerializeField] private bool groundFeet = true;
    [Tooltip("Ayak tabanının zeminden en az bu kadar (m) üstünde kalması.")]
    [SerializeField] private float soleClearance = 0.005f;
    [SerializeField] private bool holdBag = true;
    [Range(0f, 1f)] [SerializeField] private float bagHandWeight = 0.9f;

    // NPC gövdeleri (Ignore Raycast) zemin sayılmaz.
    private const int GroundMask = ~(1 << 2);

    private Animator animator;
    private SkinnedMeshRenderer bag;
    private Transform bagBone;
    [Tooltip("Sağ elin hedefi, çanta kemiğinin (bağlanma pozundaki sağ köprücük) uzayında.")]
    [SerializeField] private Vector3 bagHandLocal = new Vector3(0.0223f, 0.3123f, 0.2723f);
    [Tooltip("Vücuttan dışa yön, aynı uzayda.")]
    [SerializeField] private Vector3 bagOutLocal = new Vector3(0.085f, 0.931f, -0.355f);
    private bool bagReady;
    private float bagWeight;

    private void Awake() => animator = GetComponent<Animator>();

    private void Start()
    {
        if (holdBag)
            SetupBag();
    }

    private void SetupBag()
    {
        foreach (SkinnedMeshRenderer smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.name == "Bag" && smr.sharedMesh != null)
            {
                bag = smr;
                break;
            }
        }
        if (bag == null || !animator.isHuman)
            return;

        Transform torso = animator.GetBoneTransform(HumanBodyBones.Chest) ?? animator.GetBoneTransform(HumanBodyBones.Spine);
        Transform[] bones = bag.bones;
        Matrix4x4[] bindPoses = bag.sharedMesh.bindposes;
        int index = -1;
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] != null && bones[i].name.EndsWith("RightShoulder"))
                index = i;
        }
        if (torso == null || index < 0)
            return;

        // Gövdeye bağlı vekil kemik (bağlanma pozunda köprücükle aynı yerde): omuz oynayınca çanta sallanmaz.
        Matrix4x4 bindWorld = bag.transform.localToWorldMatrix * bindPoses[index].inverse;
        Matrix4x4 local = torso.worldToLocalMatrix * bindWorld;
        bagBone = new GameObject("Canta_Vekil_Kemik").transform;
        bagBone.SetParent(torso, false);
        bagBone.localPosition = local.GetColumn(3);
        bagBone.localRotation = local.rotation;
        bagBone.localScale = local.lossyScale;
        bones[index] = bagBone;
        bag.bones = bones;

        // Elin konacağı yer: çanta gövdesinin (askının altındaki ~15 cm'lik kutu) üst kenarının ortasının 2 cm üstü,
        // köprücük kemiğinin bağlanma uzayında. Mesh çalışırken okunamadığı (Read/Write kapalı) için editörde
        // NPC_Women'ın Bag mesh'inden hesaplandı (10.11); çanta modeli değişirse yeniden hesapla.
        bagReady = true;
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex != 0 || animator == null)
            return;

        if (groundFeet)
        {
            Ground(AvatarIKGoal.LeftFoot, animator.leftFeetBottomHeight);
            Ground(AvatarIKGoal.RightFoot, animator.rightFeetBottomHeight);
        }

        bool bagOn = bagReady && bag != null && bag.gameObject.activeInHierarchy && bag.enabled;
        bagWeight = Mathf.MoveTowards(bagWeight, bagOn ? bagHandWeight : 0f, Time.deltaTime * 3f);
        if (bagWeight > 0f)
        {
            Vector3 outward = bagBone.TransformDirection(bagOutLocal);
            // Avuç çantanın kapağının dışında dursun (tam ortada el çantanın içine giriyordu).
            Vector3 target = bagBone.TransformPoint(bagHandLocal) + outward * 0.03f;
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, bagWeight);
            animator.SetIKPosition(AvatarIKGoal.RightHand, target);
            // Avuç çantanın üstüne, parmaklar öne: elin yukarısı dışa bakar.
            Quaternion hand = Quaternion.LookRotation(transform.forward, outward);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, bagWeight * 0.6f);
            animator.SetIKRotation(AvatarIKGoal.RightHand, hand);
            animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, bagWeight);
            animator.SetIKHintPosition(AvatarIKHint.RightElbow, target + outward * 0.08f - transform.forward * 0.15f + Vector3.up * 0.3f);
        }
        else
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0f);
        }
    }

    [Tooltip("Ayak parmağı ekleminin tabandan yüksekliği (m). Oturunca ayak ucu aşağı baktığı için bilek hesabı yetmiyor, " +
             "parmak ucu kaldırıma 3-4 cm giriyordu (10.11).")]
    [SerializeField] private float toeClearance = 0.025f;

    // Ayakkabının taban noktaları (11.11, kullanıcı: "bankta oturanların ayakları yerin içine giriyor"): bilek/parmak kemiği
    // hesabı ayakkabı mesh'inin kalınlığını ve burnunu bilmiyordu, Bench_Sit'te ayakkabı 7-8 cm, kafede 2-4 cm gömülüyordu.
    // Açılışta ayakkabı mesh'i bir kez bake edilir, her köşe en yakın ayak/parmak kemiğinin uzayına alınır (seyreltilmiş);
    // IK'da bu noktaların en alçağı zeminin üstünde tutulur.
    private struct SolePoint { public Transform bone; public Vector3 local; }
    private readonly System.Collections.Generic.List<SolePoint>[] sole =
        { new System.Collections.Generic.List<SolePoint>(), new System.Collections.Generic.List<SolePoint>() };
    private SkinnedMeshRenderer[] shoeSet;
    private float soleTimer;

    private void RefreshSole()
    {
        soleTimer -= Time.deltaTime;
        if (soleTimer > 0f)
            return;
        soleTimer = 1f;
        var shoes = new System.Collections.Generic.List<SkinnedMeshRenderer>();
        foreach (SkinnedMeshRenderer smr in GetComponentsInChildren<SkinnedMeshRenderer>(false))
        {
            if (smr.enabled && smr.sharedMesh != null
                && (smr.name.IndexOf("Shoe", System.StringComparison.OrdinalIgnoreCase) >= 0 || smr.name.IndexOf("Boot", System.StringComparison.OrdinalIgnoreCase) >= 0))
                shoes.Add(smr);
        }
        if (shoeSet != null && shoeSet.Length == shoes.Count && shoes.TrueForAll(x => System.Array.IndexOf(shoeSet, x) >= 0))
            return;
        shoeSet = shoes.ToArray();
        sole[0].Clear();
        sole[1].Clear();
        Transform[] bones =
        {
            animator.GetBoneTransform(HumanBodyBones.LeftFoot), animator.GetBoneTransform(HumanBodyBones.LeftToes),
            animator.GetBoneTransform(HumanBodyBones.RightFoot), animator.GetBoneTransform(HumanBodyBones.RightToes)
        };
        if (bones[0] == null || bones[2] == null)
            return;
        var baked = new Mesh();
        foreach (SkinnedMeshRenderer smr in shoeSet)
        {
            smr.BakeMesh(baked, true);
            Vector3[] v = baked.vertices;
            int step = Mathf.Max(1, v.Length / 150);
            for (int i = 0; i < v.Length; i += step)
            {
                Vector3 w = smr.transform.TransformPoint(v[i]);
                int best = -1;
                float bestD = 0.35f * 0.35f;
                for (int b = 0; b < 4; b++)
                {
                    if (bones[b] == null)
                        continue;
                    float d = (bones[b].position - w).sqrMagnitude;
                    if (d < bestD)
                    {
                        bestD = d;
                        best = b;
                    }
                }
                if (best < 0)
                    continue;
                sole[best < 2 ? 0 : 1].Add(new SolePoint { bone = bones[best], local = bones[best].InverseTransformPoint(w) });
            }
        }
        Destroy(baked);
    }

    private void Ground(AvatarIKGoal goal, float bottom)
    {
        bool left = goal == AvatarIKGoal.LeftFoot;
        int i = left ? 0 : 1;
        if (i == 0)
            RefreshSole();
        Vector3 ankle = animator.GetIKPosition(goal);
        Transform foot = animator.GetBoneTransform(left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
        Transform toes = animator.GetBoneTransform(left ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes);
        // Kemikler bir önceki karenin (IK uygulanmış) pozunda. Kaldırma yönü sabit bir öteleme olduğu için (ayağın yönü de
        // sabitlenir) önceki kaldırma çıkarılınca animasyonun kendi pozu bulunur. IK hedefi bilek kemiğinin ~5 cm üstünde
        // olduğundan noktalar hedefe göre kaydırılmaz (11.11: kaydırılınca gömülme hiç görülmüyordu).
        Vector3 back = Vector3.up * applied[i];
        float lowY = ankle.y - bottom;
        Vector3 lowPoint = ankle;
        Vector3 toePoint = ankle;
        if (foot != null && toes != null)
        {
            toePoint = toes.position - back;
            if (toePoint.y - toeClearance < lowY)
            {
                lowY = toePoint.y - toeClearance;
                lowPoint = toePoint;
            }
        }
        foreach (SolePoint sp in sole[i])
        {
            Vector3 w = sp.bone.TransformPoint(sp.local) - back;
            if (w.y < lowY)
            {
                lowY = w.y;
                lowPoint = w;
            }
        }
        float targetLift = 0f;
        float ground = float.MinValue;
        foreach (Vector3 probe in new[] { ankle, toePoint, lowPoint })
        {
            if (Physics.Raycast(probe + Vector3.up * 0.25f, Vector3.down, out RaycastHit hit, 0.6f, GroundMask, QueryTriggerInteraction.Ignore)
                && !hit.collider.transform.IsChildOf(transform))
                ground = Mathf.Max(ground, hit.point.y);
        }
        if (ground > float.MinValue)
            targetLift = Mathf.Clamp(ground + soleClearance - lowY, 0f, 0.15f);
        // Yumuşatma (10.11, "kadın NPC'lerin dizleri kaldırımda titriyor"): kaldırma ayak başına yumuşatılır.
        float k = 1f - Mathf.Exp(-(targetLift > smoothLift[i] ? 18f : 8f) * Time.deltaTime);
        smoothLift[i] = Mathf.Lerp(smoothLift[i], targetLift, k);
        if (smoothLift[i] < 0.0005f)
        {
            smoothLift[i] = 0f;
            applied[i] = 0f;
            animator.SetIKPositionWeight(goal, 0f);
            animator.SetIKRotationWeight(goal, 0f);
            return;
        }
        // Ayağın yönü de sabitlenir: sadece bilek kaldırılınca diz açısı değişip ayak aşağı dönüyor, ayakkabının burnu
        // daha da gömülüyordu (ölçüldü: bilek 4.7 cm kalkarken en alçak nokta 1.2 cm indi).
        Quaternion rot = animator.GetIKRotation(goal);
        animator.SetIKPositionWeight(goal, 1f);
        animator.SetIKPosition(goal, ankle + Vector3.up * smoothLift[i]);
        animator.SetIKRotationWeight(goal, 1f);
        animator.SetIKRotation(goal, rot);
        applied[i] = smoothLift[i];
    }

    private readonly float[] smoothLift = new float[2];
    private readonly float[] applied = new float[2];
}
