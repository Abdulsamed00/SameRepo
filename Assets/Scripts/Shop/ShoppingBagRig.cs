using UnityEngine;

/// <summary>
/// Poşetin ip rigi (12.11): `Poset_Rig.fbx` içinde `Sap_On` / `Sap_Arka` kemikleri iki ipi ayrı oynatır, `Poset_Kapanis`
/// klibi iplerin açıktan (0) tutulan toplu hâle (1) geçişidir. Klip Animator'sız, doğrudan örneklenir.
/// </summary>
public class ShoppingBagRig : MonoBehaviour
{
    public AnimationClip closeClip;
    [Range(0f, 1f)] public float gather = 1f;
    private float applied = -1f;

    public void Apply(float amount)
    {
        gather = Mathf.Clamp01(amount);
        if (closeClip == null || Mathf.Approximately(applied, gather))
            return;
        applied = gather;
        closeClip.SampleAnimation(gameObject, closeClip.length * gather);
    }

    private void Awake()
    {
        Apply(1f);
    }
}
