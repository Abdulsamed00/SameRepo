using UnityEngine;

/// <summary>
/// Döngüdeki bir AudioSource'u klibin rastgele bir yerinden başlatır (sokak müzisyenleri aynı parçayı
/// aynı anda baştan çalmasın, oyuncu her geldiğinde şarkının başına denk gelmesin).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class RandomStartLoop : MonoBehaviour
{
    private void Start()
    {
        AudioSource source = GetComponent<AudioSource>();
        if (source.clip == null)
            return;
        source.loop = true;
        source.time = Random.Range(0f, source.clip.length * 0.9f);
        source.Play();
    }
}
