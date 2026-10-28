using UnityEngine;

/// <summary>
/// NPC'nin Animator Controller'ındaki bir state'i sahne başlayınca sürekli oynatır.
/// Geçici çözüm: NPC davranış sistemi gelene kadar her NPC tek animasyonu döngüde yapar.
/// <see cref="useRootMotion"/> açıksa klibin kendi yürüyüşü uygulanır (ör. telefonla volta atma);
/// klip döngü sonunda başladığı yere dönmüyorsa aradaki fark <see cref="loopDrift"/> ile döngü boyunca
/// geri alınır, NPC hep aynı alanda kalır.
/// </summary>
[RequireComponent(typeof(Animator))]
public class NpcAnimationPlayer : MonoBehaviour
{
    [Tooltip("Animator Controller'daki state adı (ör. Man_Idle, Standing_Talk).")]
    [SerializeField] private string stateName = "Man_Idle";
    [Tooltip("NPC'ler aynı karede başlayıp senkron görünmesin.")]
    [SerializeField] private bool randomizeStartTime = true;

    [Header("Kök Hareketi")]
    [Tooltip("Klibin yürüyüşünü uygula (yerinde değil, gerçekten yürüyerek oynar).")]
    [SerializeField] private bool useRootMotion;
    [Tooltip("Bir döngüde kökün başladığı yere göre kayması (NPC'nin başlangıç yönüne göre yerel, m). Döngü boyunca geri alınır.")]
    [SerializeField] private Vector3 loopDrift;
    [Tooltip("Klip uzunluğu (sn); kayma bu süreye yayılır.")]
    [SerializeField] private float loopLength = 1f;

    private Animator animator;
    private Quaternion startRotation;
    private float groundY;

    public string StateName
    {
        get => stateName;
        set => stateName = value;
    }

    private void Start()
    {
        animator = GetComponent<Animator>();
        animator.applyRootMotion = useRootMotion;
        startRotation = transform.rotation;
        groundY = transform.position.y;
        // Kök hareketinde klip baştan başlar: kayma yönü klibin ilk karesindeki yöne (= NPC'nin yönü) göredir.
        bool randomStart = randomizeStartTime && !useRootMotion;
        animator.Play(stateName, 0, randomStart ? Random.value : 0f);
    }

    private void OnAnimatorMove()
    {
        if (!useRootMotion || animator == null)
            return;

        Vector3 delta = animator.deltaPosition;
        if (loopLength > 0f)
            delta -= startRotation * loopDrift * (Time.deltaTime / loopLength);

        Vector3 pos = transform.position + new Vector3(delta.x, 0f, delta.z);
        pos.y = groundY;
        transform.SetPositionAndRotation(pos, animator.deltaRotation * transform.rotation);
    }
}
