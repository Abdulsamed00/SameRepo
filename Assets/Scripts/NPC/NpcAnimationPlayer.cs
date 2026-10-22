using UnityEngine;

/// <summary>
/// NPC'nin Animator Controller'ındaki bir state'i sahne başlayınca sürekli oynatır.
/// Geçici çözüm: NPC davranış sistemi gelene kadar her NPC tek animasyonu döngüde yapar.
/// </summary>
[RequireComponent(typeof(Animator))]
public class NpcAnimationPlayer : MonoBehaviour
{
    [Tooltip("Animator Controller'daki state adı (ör. Man_Idle, Standing_Talk).")]
    [SerializeField] private string stateName = "Man_Idle";
    [Tooltip("NPC'ler aynı karede başlayıp senkron görünmesin.")]
    [SerializeField] private bool randomizeStartTime = true;

    public string StateName
    {
        get => stateName;
        set => stateName = value;
    }

    private void Start()
    {
        Animator animator = GetComponent<Animator>();
        animator.applyRootMotion = false;
        animator.Play(stateName, 0, randomizeStartTime ? Random.value : 0f);
    }
}
