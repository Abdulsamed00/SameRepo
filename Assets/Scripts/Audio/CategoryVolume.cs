using UnityEngine;

/// <summary>
/// Sabit sesli bir AudioSource'u (müzik, ortam döngüsü) ayarlardaki kategori sesine bağlar: kaynağın kendi
/// sesi Awake'teki değerdir, ayar değişince onunla çarpılır. Sesi kodla sürekli değişen kaynaklar
/// (adım sesi, TV) bu bileşen yerine <see cref="GameSettings.Volume"/>'u kendileri çarpar.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class CategoryVolume : MonoBehaviour
{
    [SerializeField] private AudioCategory category = AudioCategory.Effects;

    private AudioSource source;
    private float baseVolume;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        baseVolume = source.volume;
    }

    private void OnEnable()
    {
        GameSettings.Changed += Apply;
        Apply();
    }

    private void OnDisable() => GameSettings.Changed -= Apply;

    private void Apply() => source.volume = baseVolume * GameSettings.Volume(category);
}
