using UnityEngine;

/// <summary>
/// Oturunca paltonun arka eteği (11.11, kullanıcı: "paltonun arka kısmı boşluktan oluşuyor"): etek uyluklara bağlı,
/// oturunca uyluklarla öne dönüp oturağın içinden geçiyor ve bacakların arasında oturağın ön kenarının altından
/// çıkıyordu. Palto mesh'inde "Oturma" blendshape'i var (arka etek kalçanın altına toplanır); bu bileşen karakter bir
/// oturma state'indeyken onu açar. Oyuncuda (AnaKarakter) ve NPC kadınlarda (NPC_Women) durur.
/// </summary>
public class SeatedCoat : MonoBehaviour
{
    [SerializeField] private string coatName = "Coat";
    [SerializeField] private string shapeName = "Oturma";
    [Tooltip("Adı bunları içeren klipler oturma sayılır.")]
    [SerializeField] private string[] sitStates = { "Sit", "LyingDown" };
    [SerializeField] private float blendSpeed = 4f;

    private Animator animator;
    private SkinnedMeshRenderer coat;
    private int shape = -1;
    private float weight;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        foreach (SkinnedMeshRenderer r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (r.name == coatName && r.sharedMesh != null)
            {
                coat = r;
                shape = r.sharedMesh.GetBlendShapeIndex(shapeName);
                break;
            }
        }
    }

    private void Update()
    {
        if (coat == null || shape < 0 || animator == null || !animator.isActiveAndEnabled)
            return;
        float target = IsSitting() ? 100f : 0f;
        weight = Mathf.MoveTowards(weight, target, blendSpeed * 100f * Time.deltaTime);
        coat.SetBlendShapeWeight(shape, weight);
    }

    private bool IsSitting()
    {
        AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(0);
        if (Matches(clips))
            return true;
        if (animator.IsInTransition(0) && Matches(animator.GetNextAnimatorClipInfo(0)))
            return true;
        return false;
    }

    private bool Matches(AnimatorClipInfo[] clips)
    {
        foreach (AnimatorClipInfo info in clips)
        {
            if (info.clip == null || info.weight < 0.5f)
                continue;
            string n = info.clip.name;
            foreach (string s in sitStates)
            {
                if (n.IndexOf(s, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
        }
        return false;
    }
}
