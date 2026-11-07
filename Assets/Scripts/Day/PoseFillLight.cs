using UnityEngine;

/// <summary>
/// Gündüz aynada poz verirken karakteri aydınlatan yumuşak dolgu ışığı (05.11): gündüz iç ışıklar kapalı, ayna köşesi
/// sadece pencere ışığıyla loş kalıyor ve karakter "solmuş" görünüyordu. Gece oda ışığı (<see cref="RoomLights"/>) yeter,
/// bu ışık sadece gündüz ve poz sürerken yanar, yavaşça açılıp söner. Gölgesiz.
/// </summary>
[RequireComponent(typeof(Light))]
public class PoseFillLight : MonoBehaviour
{
    [SerializeField] private float intensity = 1.8f;
    [SerializeField] private float fadeSpeed = 2f;

    private Light lamp;
    private PlayerBody body;
    private float level;

    private void Awake()
    {
        lamp = GetComponent<Light>();
        lamp.intensity = 0f;
        lamp.enabled = false;
    }

    private void Update()
    {
        if (body == null)
            body = FindFirstObjectByType<PlayerBody>();
        bool on = body != null && body.IsPosing && !DayRoutine.NightFell;
        level = Mathf.MoveTowards(level, on ? 1f : 0f, fadeSpeed * Time.deltaTime);
        lamp.enabled = level > 0f;
        lamp.intensity = intensity * level;
    }
}
