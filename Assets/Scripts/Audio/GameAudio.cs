using UnityEngine;

/// <summary>
/// Tek seferlik efekt sesleri (kapı, satın alma, esneme...). Ses objesi sahne geçişinde kesilmesin diye
/// DontDestroyOnLoad'dur ve klip bitince kendini siler.
/// </summary>
public static class GameAudio
{
    /// <summary>Dünyada bir noktadan gelen ses (kapı, dolap, kıyafet).</summary>
    public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f)
    {
        AudioSource source = Create(clip, volume);
        if (source == null)
            return;
        source.transform.position = position;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 2f;
        source.maxDistance = 20f;
        source.Play();
    }

    /// <summary>Konumsuz ses (arayüz, oyuncunun kendi sesi).</summary>
    public static void Play2D(AudioClip clip, float volume = 1f)
    {
        AudioSource source = Create(clip, volume);
        if (source == null)
            return;
        source.spatialBlend = 0f;
        source.Play();
    }

    private static AudioSource Create(AudioClip clip, float volume)
    {
        if (clip == null)
            return null;
        GameObject go = new GameObject("Ses_" + clip.name);
        Object.DontDestroyOnLoad(go);
        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volume;
        source.playOnAwake = false;
        Object.Destroy(go, clip.length + 0.1f);
        return source;
    }
}
