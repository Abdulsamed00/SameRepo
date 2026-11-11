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
            GroundFeet();

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

    /// <returns>Taban noktaları yeniden kurulduysa (ayakkabı değişti) true.</returns>
    private bool RefreshSole()
    {
        soleTimer -= Time.deltaTime;
        if (soleTimer > 0f)
            return false;
        soleTimer = 1f;
        var shoes = new System.Collections.Generic.List<SkinnedMeshRenderer>();
        foreach (SkinnedMeshRenderer smr in GetComponentsInChildren<SkinnedMeshRenderer>(false))
        {
            if (smr.enabled && smr.sharedMesh != null
                && (smr.name.IndexOf("Shoe", System.StringComparison.OrdinalIgnoreCase) >= 0 || smr.name.IndexOf("Boot", System.StringComparison.OrdinalIgnoreCase) >= 0))
                shoes.Add(smr);
        }
        if (shoeSet != null && shoeSet.Length == shoes.Count && shoes.TrueForAll(x => System.Array.IndexOf(shoeSet, x) >= 0))
            return false;
        shoeSet = shoes.ToArray();
        sole[0].Clear();
        sole[1].Clear();
        Transform[] bones =
        {
            animator.GetBoneTransform(HumanBodyBones.LeftFoot), animator.GetBoneTransform(HumanBodyBones.LeftToes),
            animator.GetBoneTransform(HumanBodyBones.RightFoot), animator.GetBoneTransform(HumanBodyBones.RightToes)
        };
        if (bones[0] == null || bones[2] == null)
            return true;
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
        return true;
    }

    // Ayakkabı tabanı (ve parmak eklemi - toeClearance) IK hedefinin (bilek hedefi + yönü) uzayında. Hedef her karede
    // animasyonun kendi pozundan gelir (GetIKPosition/Rotation, kaldırmasız), tabanın yeri de ondan hesaplanır: kaldırma
    // ölçüme hiç karışmaz. 11.11'e kadar taban kemiklerden okunup önceki kaldırma çıkarılıyordu; kemikler o anda
    // kaldırmayı tam yansıtmadığı için (IK ayağı hedefin ~2/3'ü kadar kaldırıyordu, ölçüldü) taban her karede biraz daha
    // aşağıda görünüyor, kaldırma kendi kendini büyütüp 0.15 m sınırına dayanıyordu: ayaklar havada, dizler 30-70° kırık
    // (kullanıcı: "ayakları dizlerinden kırık, yürüyenler ve sabit duranlar").
    // Kalibrasyon: o ayağa IK uygulanmamış bir karenin LateUpdate'inde (son poz = animasyon pozu) kemiklerdeki taban
    // noktaları aynı karenin hedefine göre kaydedilir; kaldırma yokken her karede tazelenir (parmak bükülmesi izlenir).
    private readonly System.Collections.Generic.List<Vector3>[] goalSole =
        { new System.Collections.Generic.List<Vector3>(), new System.Collections.Generic.List<Vector3>() };
    private readonly bool[] calibrated = new bool[2];
    private readonly Vector3[] goalPos = new Vector3[2];
    private readonly Quaternion[] goalRot = { Quaternion.identity, Quaternion.identity };
    private readonly int[] goalFrame = { -1, -1 };

    private void LateUpdate()
    {
        if (!groundFeet || animator == null || !animator.isHuman)
            return;
        if (RefreshSole())
            calibrated[0] = calibrated[1] = false;
        for (int i = 0; i < 2; i++)
        {
            if (goalFrame[i] == Time.frameCount && applied[i] == 0f && bodyApplied == 0f)
                Calibrate(i);
        }
    }

    private void Calibrate(int i)
    {
        bool left = i == 0;
        Transform toes = animator.GetBoneTransform(left ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes);
        Quaternion inv = Quaternion.Inverse(goalRot[i]);
        var list = goalSole[i];
        list.Clear();
        if (toes != null)
            list.Add(inv * (toes.position - Vector3.up * toeClearance - goalPos[i]));
        foreach (SolePoint sp in sole[i])
            list.Add(inv * (sp.bone.TransformPoint(sp.local) - goalPos[i]));
        calibrated[i] = true;
    }

    [Tooltip("Ayağın en fazla bu kadar (m) kaldırılması; zemin ayağın bundan daha üstündeyse (bankın oturağı, masa) zemin sayılmaz.")]
    [SerializeField] private float maxLift = 0.15f;

    [Tooltip("Kalça bileklerin bu kadar (m) üstündeyse ayakta sayılır (duran/yürüyen); altındaysa oturuyor.")]
    [SerializeField] private float standingHipHeight = 0.65f;
    [Tooltip("Ayaktayken (duran ve yürüyen) gövdenin sabit kaldırılması (m): klipte ayakkabı tabanı kökün bu kadar altında.")]
    [SerializeField] private float standingBodyLift = 0.02f;
    [Tooltip("Gövde kaldırmanın oturma/kalkma geçişindeki yumuşatma süresi (sn).")]
    [SerializeField] private float bodyLiftSmoothing = 0.4f;
    [Tooltip("Ayaktayken ayağın altındaki zemin kökün zemininden en az bu kadar (m) yüksekse ayak kaldırılır.")]
    [SerializeField] private float stepTolerance = 0.02f;
    private float bodyLift;
    private float bodyApplied;

    /// <summary>
    /// 11.11 (kullanıcı: "ayakları dizlerinden kırık, yürüyenler ve sabit duranlar"): yeni kliplerde (Duzeltilmis/*) basan
    /// ayağın tabanı kökün (zeminin) 2-3 cm altında. Bunu ayağı IK ile kaldırarak düzeltmek neredeyse düz bacakta dizi
    /// ~30° büküyor (3 cm kaldırma = 30°, ölçüldü). Ayaktayken gövde (bodyPosition) sabit standingBodyLift kadar kaldırılır,
    /// bacak klipteki gibi kalır; ayak IK'sı sadece ayağın altındaki zemin kökün zemininden yüksekse (kaldırım basamağı)
    /// o fark kadar kaldırır. Otururken gövde kaldırılmaz (oturaktan kalkardı), tabana göre ayak kaldırma kalır.
    /// </summary>
    private void GroundFeet()
    {
        var target = new float[2];
        var groundY = new float[2];
        bool ready = true;
        for (int i = 0; i < 2; i++)
            ready &= Measure(i, out target[i], out groundY[i]);
        if (!ready)
        {
            // Kalibrasyon bu karenin LateUpdate'inde, kaldırmasız pozda yapılır.
            bodyLift = bodyApplied = 0f;
            for (int i = 0; i < 2; i++)
            {
                smoothLift[i] = applied[i] = 0f;
                AvatarIKGoal g = i == 0 ? AvatarIKGoal.LeftFoot : AvatarIKGoal.RightFoot;
                animator.SetIKPositionWeight(g, 0f);
                animator.SetIKRotationWeight(g, 0f);
            }
            return;
        }

        bool standing = animator.bodyPosition.y - Mathf.Min(goalPos[0].y, goalPos[1].y) > standingHipHeight;
        // Ayaktayken sabit (11.11, kullanıcı: "kaldırımda yürüyenlerin dizlerinde sıkıntı"): gövde kaldırma önce tabanın
        // ölçülen gömülmesine göreydi; yürürken bu adım boyunca 0-6 cm gidip geliyor, gövde her adımda inip çıkıyor,
        // yavaş ortalamada da bazı NPC'ler 5-6 cm havada yürüyordu. Klibin (Duzeltilmis/*) tabanı kökün sabit ~2-3 cm
        // altında; o kadar sabit kaldırma yeterli. Otururken 0.
        float bodyTarget = standing ? standingBodyLift : 0f;
        float rate = standing ? 1f / bodyLiftSmoothing : 8f;
        bodyLift = Mathf.Lerp(bodyLift, bodyTarget, 1f - Mathf.Exp(-rate * Time.deltaTime));
        if (bodyLift < 0.0005f)
            bodyLift = 0f;
        if (bodyLift > 0f)
            animator.bodyPosition += Vector3.up * bodyLift;
        bodyApplied = bodyLift;

        if (standing)
        {
            // Ayaktayken ayak IK'sı tabanın gömülmesine bakmaz (gövde kapatıyor; basan ayağı 1-2 cm kaldırmak bile neredeyse
            // düz bacakta dizi her adımda 15-20° kırıyordu), sadece ayağın altındaki zemin kökün bastığı zeminden yüksekse
            // (kaldırım basamağı) o fark kadar kaldırır.
            float rootY = transform.position.y;
            for (int i = 0; i < 2; i++)
            {
                float step = groundY[i] > float.MinValue ? groundY[i] - rootY : 0f;
                target[i] = step > stepTolerance ? Mathf.Min(step, maxLift) : 0f;
            }
        }

        for (int i = 0; i < 2; i++)
            ApplyFoot(i, Mathf.Max(0f, target[i] - bodyLift));
    }

    /// <summary>Ayağın animasyon pozundaki tabanı; ayağın altındaki zemine göre gereken kaldırma ve kökün zeminine göre gömülme.</summary>
    private bool Measure(int i, out float targetLift, out float groundY)
    {
        groundY = float.MinValue;
        AvatarIKGoal goal = i == 0 ? AvatarIKGoal.LeftFoot : AvatarIKGoal.RightFoot;
        float bottom = i == 0 ? animator.leftFeetBottomHeight : animator.rightFeetBottomHeight;
        Vector3 ankle = animator.GetIKPosition(goal);
        Quaternion ankleRot = animator.GetIKRotation(goal);
        goalPos[i] = ankle;
        goalRot[i] = ankleRot;
        goalFrame[i] = Time.frameCount;
        targetLift = 0f;
        if (!calibrated[i])
            return false;
        float lowY = ankle.y - bottom;
        Vector3 lowPoint = ankle;
        foreach (Vector3 p in goalSole[i])
        {
            Vector3 w = ankle + ankleRot * p;
            if (w.y < lowY)
            {
                lowY = w.y;
                lowPoint = w;
            }
        }
        float ground = float.MinValue;
        foreach (Vector3 probe in new[] { ankle, lowPoint })
        {
            // Bankta oturanda ayak oturağın önünde/altında: aşağı ışın oturağa çarpıp ayağı 0.15 m kaldırıyordu (11.11).
            // Ayağın maxLift'ten daha üstündeki yüzey zemin değil.
            // Küre: levha aralarındaki ince boşluktan aşağı düşmesin (11.11, kaldırımda her 3 m'de diz kırılıyordu).
            if (Physics.SphereCast(probe + Vector3.up * 0.25f, 0.03f, Vector3.down, out RaycastHit hit, 0.6f, GroundMask, QueryTriggerInteraction.Ignore)
                && !hit.collider.transform.IsChildOf(transform) && hit.point.y < lowY + maxLift)
                ground = Mathf.Max(ground, hit.point.y);
        }
        groundY = ground;
        if (ground > float.MinValue)
            targetLift = Mathf.Clamp(ground + soleClearance - lowY, 0f, maxLift);
        return true;
    }

    private void ApplyFoot(int i, float targetLift)
    {
        AvatarIKGoal goal = i == 0 ? AvatarIKGoal.LeftFoot : AvatarIKGoal.RightFoot;
        Vector3 ankle = goalPos[i];
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
        // Hedef gövdeyle birlikte kalkmış ayağın üstüne eklenir.
        animator.SetIKPositionWeight(goal, 1f);
        animator.SetIKPosition(goal, ankle + Vector3.up * (bodyLift + smoothLift[i]));
        animator.SetIKRotationWeight(goal, 1f);
        animator.SetIKRotation(goal, goalRot[i]);
        applied[i] = smoothLift[i];
    }

    // Hata ayıklama/ölçüm için (Play'de dışarıdan okunur).
    public float BodyLift => bodyLift;
    public float FootLift(int i) => smoothLift[i];

    private readonly float[] smoothLift = new float[2];
    private readonly float[] applied = new float[2];
}
