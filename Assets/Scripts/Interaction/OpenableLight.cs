using UnityEngine;

/// <summary>
/// Dolap içi ışığı (05.11): bağlı <see cref="Openable"/> açılınca yavaşça yanar, kapanınca söner. Dolabın içi kapalı bir
/// kutu olduğu için oda ışığı ve güneş içeri girmiyordu, kıyafetler görünmüyordu. Realtime nokta ışık, gölgesiz.
/// </summary>
[RequireComponent(typeof(Light))]
public class OpenableLight : MonoBehaviour
{
    [SerializeField] private Openable target;
    [Tooltip("Boş değilse bu odanın ışığı açıkken yanar (oda ışığı kapanınca dolap ışığı da söner).")]
    [SerializeField] private RoomLights room;
    [SerializeField] private float intensity = 1.2f;
    [SerializeField] private float fadeSpeed = 3f;

    private Light lamp;
    private float level;

    private void Awake()
    {
        lamp = GetComponent<Light>();
        lamp.intensity = 0f;
        lamp.enabled = false;
    }

    private void Update()
    {
        // Sadece gece ve oda ışığı açıkken (10.11, kullanıcı: "sabah dolapta ışık olmasın").
        bool open = target != null && target.IsOpen && DayRoutine.NightFell && (room == null || room.IsOn);
        level = Mathf.MoveTowards(level, open ? 1f : 0f, fadeSpeed * Time.deltaTime);
        lamp.enabled = level > 0f;
        lamp.intensity = intensity * level;
    }
}
