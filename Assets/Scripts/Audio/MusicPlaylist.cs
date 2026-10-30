using UnityEngine;

/// <summary>
/// Klipleri karışık sırayla arka arkaya çalan müzik (mağaza müziği). Liste bitince yeniden karıştırılır.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class MusicPlaylist : MonoBehaviour
{
    [SerializeField] private AudioClip[] clips;

    private AudioSource source;
    private int[] order;
    private int next;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        source.loop = false;
        source.playOnAwake = false;
    }

    private void Update()
    {
        if (clips == null || clips.Length == 0 || source.isPlaying)
            return;

        if (order == null || next >= order.Length)
            Shuffle();
        source.clip = clips[order[next++]];
        source.Play();
    }

    private void Shuffle()
    {
        order = new int[clips.Length];
        for (int i = 0; i < order.Length; i++)
            order[i] = i;
        for (int i = order.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }
        next = 0;
    }
}
