using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Oyuncu yürürken döngüde çalan adım sesi. Klip sahneye göre seçilir (sokakta taş, evde/mağazada iç mekan).
/// Durunca ses kısılıp duraklar, yürümeye başlayınca kaldığı yerden devam eder. Player kökünde durur.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class PlayerFootsteps : MonoBehaviour
{
    [System.Serializable]
    public class SceneClip
    {
        public string scene;
        public AudioClip clip;
    }

    [Tooltip("Listede olmayan sahnelerde çalan adım sesi.")]
    [SerializeField] private AudioClip defaultClip;
    [SerializeField] private SceneClip[] sceneClips;
    [SerializeField] private float volume = 0.5f;
    [Tooltip("Bu hızın (m/sn) üstünde yürüyor sayılır.")]
    [SerializeField] private float minSpeed = 0.3f;
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float runSpeed = 3f;
    [Tooltip("Koşarken adım sesinin hızlanma oranı.")]
    [SerializeField] private float runPitch = 1.2f;
    [Tooltip("Sesin açılıp kapanma süresi (sn).")]
    [SerializeField] private float fadeTime = 0.15f;

    private AudioSource source;
    private FirstPersonController controller;
    private CharacterController characterController;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        controller = GetComponent<FirstPersonController>();
        characterController = GetComponent<CharacterController>();

        string scene = SceneManager.GetActiveScene().name;
        AudioClip clip = defaultClip;
        if (sceneClips != null)
            foreach (SceneClip entry in sceneClips)
                if (entry.scene == scene && entry.clip != null)
                    clip = entry.clip;

        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
    }

    private void Update()
    {
        if (source.clip == null)
            return;

        float speed = controller != null ? controller.HorizontalSpeed : 0f;
        bool walking = controller != null && !controller.Frozen && characterController.enabled
            && characterController.isGrounded && speed > minSpeed;

        float target = walking ? volume * GameSettings.Volume(AudioCategory.Effects) : 0f;
        source.volume = Mathf.MoveTowards(source.volume, target, volume * Time.deltaTime / fadeTime);
        if (walking)
            source.pitch = Mathf.Lerp(1f, runPitch, Mathf.InverseLerp(walkSpeed, runSpeed, speed));

        if (source.volume > 0f && !source.isPlaying)
            source.Play();
        else if (source.volume <= 0f && source.isPlaying)
            source.Pause();
    }
}
