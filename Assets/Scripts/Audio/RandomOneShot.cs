using UnityEngine;

/// <summary>
/// Aralıklarla rastgele noktalardan birinde çalan ortam sesi (sokakta uzaktan geçen araba).
/// </summary>
public class RandomOneShot : MonoBehaviour
{
    [SerializeField] private AudioClip clip;
    [Tooltip("Sesin çalabileceği noktalar. Boşsa bu obje.")]
    [SerializeField] private Transform[] points;
    [SerializeField] private float minInterval = 30f;
    [SerializeField] private float maxInterval = 70f;
    [SerializeField] private float volume = 0.6f;
    [SerializeField] private float minDistance = 8f;
    [SerializeField] private float maxDistance = 70f;

    private float nextTime;

    private void Start() => nextTime = Time.time + Random.Range(minInterval * 0.3f, maxInterval * 0.5f);

    private void Update()
    {
        if (clip == null || Time.time < nextTime)
            return;
        nextTime = Time.time + clip.length + Random.Range(minInterval, maxInterval);

        Transform point = points != null && points.Length > 0 ? points[Random.Range(0, points.Length)] : transform;
        AudioSource source = new GameObject("Ses_" + clip.name).AddComponent<AudioSource>();
        source.transform.SetParent(transform, false);
        source.transform.position = point.position;
        source.clip = clip;
        source.volume = volume * GameSettings.Volume(AudioCategory.Ambient);
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.Play();
        Destroy(source.gameObject, clip.length + 0.1f);
    }
}
