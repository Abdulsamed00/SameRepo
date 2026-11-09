using UnityEngine;

/// <summary>
/// Gitar çalan NPC: gitar göğüs kemiğine bağlı durur, eller IK ile gitara oturur.
/// Sol el (sap): yönü animasyondan değil gitarın eksenlerinden kurulur (09.11, kullanıcı: "eller bozuk"): avuç sapın
/// altında yukarı bakar, eklem çizgisi sap boyunca klavyenin ön-alt kenarında, parmaklar klavyenin önünden yukarı kıvrılır,
/// başparmak sapın arkasına gider. Modelde parmak kemiği yoktu; elle iskeletlenen rig'e her ele iki kemik eklendi
/// (`*Fingers`: dört parmak birlikte, `*Thumb`), kıvrımları LateUpdate'te verilir. Sol el sap boyunca animasyonun
/// hareketiyle hafifçe kayar. Sağ el (tel) animasyonun yönünü korur, gövdenin üstünde sabit, parmakları hafif kıvrık.
/// Animator katmanında IK Pass açık olmalı (NPC_Muzisyen.controller); Animator her zaman çalışmalı (AlwaysAnimate):
/// görünmezken durunca ilk karelerdeki ölçüm bozuluyordu.
/// </summary>
[RequireComponent(typeof(Animator))]
public class GuitarPlayer : MonoBehaviour
{
    [SerializeField] private Transform guitar;
    [Tooltip("Klipteki ortalama bilek konumları (göğüs kemiğine göre). Animasyonun hareketi bu ortalamadan farkı olarak alınır.")]
    [SerializeField] private Vector3 leftAverage;
    [SerializeField] private Vector3 rightAverage;
    [Tooltip("Sağ bileğin gitara göre konumu (m; x gitarın eni (yukarı), y sap yönü, z gitarın ön yüzü).")]
    [SerializeField] private Vector3 rightGrip;
    [Tooltip("Sol elin sap boyunca animasyonla en fazla kayabileceği mesafe (m).")]
    [SerializeField] private float leftSlide = 0.03f;
    [Tooltip("Sağ elin gitarın yüzünde animasyonla en fazla oynayacağı mesafe (m).")]
    [SerializeField] private float rightRange = 0.02f;
    [Tooltip("Sağ dirseğin gitara göre konumu: gövdenin üst kenarının önünde, yoksa önkol gitarın içinden çıkar.")]
    [SerializeField] private Vector3 rightElbow = new Vector3(0.10f, -0.55f, 0.12f);

    [Header("Sol el (sapı kavrama)")]
    [Tooltip("Parmak eklemlerinin (LeftFingers kemiği) gitara göre konumu. Sap: x ±0.02, arka z 0.012, klavye z 0.034.")]
    [SerializeField] private Vector3 leftKnuckle = new Vector3(-0.028f, 0.16f, 0.04f);
    [Tooltip("Düz parmakların yönü (gitar uzayında).")]
    [SerializeField] private Vector3 leftFingerDir = new Vector3(0f, 0f, 1f);
    [Tooltip("Avucun baktığı yön (gitar uzayında). Gövdeye doğru eğik (y < 0): parmaklar klavyede çapraz yatar, akor basar gibi.")]
    [SerializeField] private Vector3 leftPalmDir = new Vector3(1f, -0.7f, 0f);
    [Tooltip("Sol dirseğin gitara göre konumu.")]
    [SerializeField] private Vector3 leftElbow = new Vector3(-0.25f, 0.0f, -0.25f);
    [SerializeField, Range(0f, 1f)] private float leftElbowWeight = 0.6f;

    [Header("Parmak kıvrımı (derece)")]
    [SerializeField] private float leftFingerCurl = 100f;
    [SerializeField] private float leftThumbCurl = 70f;
    [SerializeField] private float rightFingerCurl = 50f;
    [SerializeField] private float rightThumbCurl = 30f;

    private Animator animator;
    private Transform chest, leftHand;
    private Transform leftFingers, leftThumb, rightFingers, rightThumb;
    private Quaternion leftFingersRest, leftThumbRest, rightFingersRest, rightThumbRest;
    private Quaternion handFrame = Quaternion.identity;  // el kemiği uzayında (parmak yönü, avuç) çerçevesi
    private Vector3 knuckleLocal;                        // eklem noktası, el kemiği uzayında
    private Quaternion goalOffset = Quaternion.identity; // IK hedef dönüşü → el kemiği dönüşü
    private Quaternion goalSample;
    private int calibrate = 3;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        leftFingers = leftHand != null ? leftHand.Find("LeftFingers") : null;
        leftThumb = leftHand != null ? leftHand.Find("LeftThumb") : null;
        rightFingers = rightHand != null ? rightHand.Find("RightFingers") : null;
        rightThumb = rightHand != null ? rightHand.Find("RightThumb") : null;
        if (leftFingers) leftFingersRest = leftFingers.localRotation;
        if (leftThumb) leftThumbRest = leftThumb.localRotation;
        if (rightFingers) rightFingersRest = rightFingers.localRotation;
        if (rightThumb) rightThumbRest = rightThumb.localRotation;
        if (leftFingers) knuckleLocal = leftFingers.localPosition;

        // El kemiği uzayında parmak yönü ve avuç: bağlanma pozundan (T-poz, avuç aşağı).
        SkinnedMeshRenderer skin = GetComponentInChildren<SkinnedMeshRenderer>();
        if (skin != null && skin.sharedMesh != null && leftHand != null)
        {
            int hand = System.Array.IndexOf(skin.bones, leftHand);
            int lower = System.Array.IndexOf(skin.bones, animator.GetBoneTransform(HumanBodyBones.LeftLowerArm));
            if (hand >= 0 && lower >= 0)
            {
                Matrix4x4 h = skin.sharedMesh.bindposes[hand].inverse, l = skin.sharedMesh.bindposes[lower].inverse;
                Vector3 finger = (h.MultiplyPoint3x4(Vector3.zero) - l.MultiplyPoint3x4(Vector3.zero)).normalized;
                Quaternion inv = Quaternion.Inverse(h.rotation);
                handFrame = Quaternion.LookRotation(inv * finger, inv * Vector3.down);
            }
        }
    }

    private void LateUpdate()
    {
        if (calibrate > 0 && leftHand != null)
        {
            goalOffset = Quaternion.Inverse(goalSample) * leftHand.rotation;
            calibrate--;
        }
        Curl(leftFingers, leftFingersRest, leftFingerCurl);
        Curl(leftThumb, leftThumbRest, leftThumbCurl);
        Curl(rightFingers, rightFingersRest, rightFingerCurl);
        Curl(rightThumb, rightThumbRest, rightThumbCurl);
    }

    private static void Curl(Transform bone, Quaternion rest, float degrees)
    {
        if (bone != null)
            bone.localRotation = rest * Quaternion.AngleAxis(degrees, Vector3.forward);
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (guitar == null || chest == null)
            return;

        // Sol el: istenen el kemiği dönüşü gitarın eksenlerinden; konum eklem noktasından geri hesaplanır.
        Vector3 slide = Delta(AvatarIKGoal.LeftHand, leftAverage);
        Vector3 knuckle = guitar.TransformPoint(leftKnuckle + new Vector3(0f, Mathf.Clamp(slide.y, -leftSlide, leftSlide), 0f));
        Vector3 fingerW = guitar.TransformDirection(leftFingerDir).normalized;
        Vector3 palmW = Vector3.ProjectOnPlane(guitar.TransformDirection(leftPalmDir), fingerW).normalized;
        Quaternion bone = Quaternion.LookRotation(fingerW, palmW) * Quaternion.Inverse(handFrame);
        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1f);
        animator.SetIKPosition(AvatarIKGoal.LeftHand, knuckle - bone * knuckleLocal);
        if (calibrate > 0)
        {
            // Ölçüm karesi: IK tamamen kapalı, el animasyondaki dönüşünde kalır; hedef ile kemik farkı sabittir.
            goalSample = animator.GetIKRotation(AvatarIKGoal.LeftHand);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0f);
            return;
        }
        else
        {
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 1f);
            animator.SetIKRotation(AvatarIKGoal.LeftHand, bone * Quaternion.Inverse(goalOffset));
        }
        animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, leftElbowWeight);
        animator.SetIKHintPosition(AvatarIKHint.LeftElbow, guitar.TransformPoint(leftElbow));

        // Sağ el: animasyonun yönü, gövdenin üstünde küçük hareket.
        Vector3 right = Delta(AvatarIKGoal.RightHand, rightAverage);
        right.x = Mathf.Clamp(right.x, -rightRange, rightRange);
        right.y = Mathf.Clamp(right.y, -rightRange, rightRange);
        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 1f);
        animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 1f);
        animator.SetIKPosition(AvatarIKGoal.RightHand, guitar.position + guitar.rotation * (rightGrip + new Vector3(right.x, right.y, 0f)));
        animator.SetIKRotation(AvatarIKGoal.RightHand, animator.GetIKRotation(AvatarIKGoal.RightHand));
        animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 1f);
        animator.SetIKHintPosition(AvatarIKHint.RightElbow, guitar.position + guitar.rotation * rightElbow);
    }

    // Animasyondaki bileğin klip ortalamasından sapması, gitarın ekseninde.
    private Vector3 Delta(AvatarIKGoal goal, Vector3 average)
    {
        Vector3 animated = chest.InverseTransformPoint(animator.GetIKPosition(goal));
        Vector3 world = chest.TransformVector(animated - average);
        return Quaternion.Inverse(guitar.rotation) * world;
    }
}
