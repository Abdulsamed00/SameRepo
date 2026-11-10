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

    private void Ground(AvatarIKGoal goal, float bottom)
    {
        Vector3 ankle = animator.GetIKPosition(goal);
        float sole = ankle.y - bottom;
        if (Physics.Raycast(ankle + Vector3.up * 0.25f, Vector3.down, out RaycastHit hit, 0.6f, GroundMask, QueryTriggerInteraction.Ignore)
            && !hit.collider.transform.IsChildOf(transform))
        {
            float lift = hit.point.y + soleClearance - sole;
            if (lift > 0f)
            {
                animator.SetIKPositionWeight(goal, 1f);
                animator.SetIKPosition(goal, ankle + Vector3.up * lift);
                return;
            }
        }
        animator.SetIKPositionWeight(goal, 0f);
    }
}
