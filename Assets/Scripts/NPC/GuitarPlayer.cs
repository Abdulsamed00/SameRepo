using UnityEngine;

/// <summary>
/// Gitar çalan NPC: gitar göğüs kemiğine bağlı durur, eller IK ile gitara oturur (animasyonun eli gitarın içine girmesin).
/// Sol bilek sap boyunca, sağ bilek gövdenin üstünde animasyonun hareketini korur, gitara dik eksende sabit kalır.
/// Animator katmanında IK Pass açık olmalı (NPC_Muzisyen.controller).
/// </summary>
[RequireComponent(typeof(Animator))]
public class GuitarPlayer : MonoBehaviour
{
    [SerializeField] private Transform guitar;
    [Tooltip("Klipteki ortalama bilek konumları (göğüs kemiğine göre). Animasyonun hareketi bu ortalamadan farkı olarak alınır.")]
    [SerializeField] private Vector3 leftAverage;
    [SerializeField] private Vector3 rightAverage;
    [Tooltip("Bileklerin gitara göre konumu (m; x gitarın eni, y sap yönü, z gitarın ön yüzü).")]
    [SerializeField] private Vector3 leftGrip;
    [SerializeField] private Vector3 rightGrip;
    [Tooltip("Sol elin sap boyunca en fazla kayabileceği mesafe (m).")]
    [SerializeField] private float leftSlide = 0.05f;
    [Tooltip("Sağ elin gitarın yüzünde animasyonla en fazla oynayacağı mesafe (m).")]
    [SerializeField] private float rightRange = 0.02f;
    [Tooltip("Sağ dirseğin gitara göre konumu: gövdenin üst kenarının önünde, yoksa önkol gitarın içinden çıkar.")]
    [SerializeField] private Vector3 rightElbow = new Vector3(0.10f, -0.55f, 0.12f);

    private Animator animator;
    private Transform chest;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        chest = animator.GetBoneTransform(HumanBodyBones.Chest);
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (guitar == null || chest == null)
            return;

        Vector3 left = Delta(AvatarIKGoal.LeftHand, leftAverage);
        left.y = Mathf.Clamp(left.y, -leftSlide, leftSlide);
        Place(AvatarIKGoal.LeftHand, leftGrip + new Vector3(0f, left.y, 0f));

        Vector3 right = Delta(AvatarIKGoal.RightHand, rightAverage);
        right.x = Mathf.Clamp(right.x, -rightRange, rightRange);
        right.y = Mathf.Clamp(right.y, -rightRange, rightRange);
        Place(AvatarIKGoal.RightHand, rightGrip + new Vector3(right.x, right.y, 0f));

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

    private void Place(AvatarIKGoal goal, Vector3 grip)
    {
        animator.SetIKPositionWeight(goal, 1f);
        animator.SetIKRotationWeight(goal, 1f);
        animator.SetIKPosition(goal, guitar.position + guitar.rotation * grip);
        animator.SetIKRotation(goal, animator.GetIKRotation(goal));
    }
}
