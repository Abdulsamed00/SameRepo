using UnityEngine;

/// <summary>
/// Evden çıkıp dönünce (<see cref="DayRoutine.NightFell"/>) hava kararmıştır: pencerelerden görünen gökyüzü gece olur,
/// güneşin direkt ışığı kapanır (içerideki ışıklar ve bake edilmiş aydınlatma yerinde kalır). Ertesi sabah
/// (<see cref="DayRoutine.Morning"/>, ekran kararmışken; son günün sabahı da) akşamüstü geri gelir. Güneş (Directional Light) objesine eklenir.
/// </summary>
[RequireComponent(typeof(Light))]
public class HouseNightfall : MonoBehaviour
{
    [Header("Gece gökyüzü")]
    [SerializeField] private float skyExposure = 0.03f;
    [SerializeField] private Color skyTint = new Color(0.25f, 0.3f, 0.5f, 1f);
    [SerializeField] private Color groundColor = new Color(0.04f, 0.045f, 0.07f, 1f);
    [Tooltip("Gece güneşin direkt ışığının çarpanı (0 = kapalı).")]
    [SerializeField] private float sunMultiplier = 0f;

    [Header("Pencereden görünen dış mekan")]
    [Tooltip("Zemin gibi bake edilmiş aydınlatmayla gündüz kalan dış yüzeyler: gece rengi karartılır (güneşi kapatmak bunları karartmıyor).")]
    [SerializeField] private Renderer[] darkenAtNight;
    [SerializeField] private float darkenMultiplier = 0.1f;

    [Tooltip("Gece yansıma probe'larının şiddeti (gündüz 1).")]
    [SerializeField] private float reflectionMultiplier = 0.1f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private ReflectionProbe[] probes;
    private Light sun;
    private float dayIntensity;
    private Material daySky;
    private Material nightSky;

    private void Awake()
    {
        sun = GetComponent<Light>();
        dayIntensity = sun.intensity;
        daySky = RenderSettings.skybox;
        probes = FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None);
    }

    private void OnEnable()
    {
        GameDay.Changed += Apply;
        DayRoutine.Morning += Apply;
    }

    private void OnDisable()
    {
        GameDay.Changed -= Apply;
        DayRoutine.Morning -= Apply;
    }

    private void Start() => Apply();

    private void OnDestroy()
    {
        if (nightSky != null)
            Destroy(nightSky);
    }

    private void Apply()
    {
        bool night = DayRoutine.NightFell;
        sun.intensity = night ? dayIntensity * sunMultiplier : dayIntensity;
        if (night && daySky != null)
        {
            if (nightSky == null)
            {
                nightSky = new Material(daySky);
                nightSky.SetFloat("_Exposure", skyExposure);
                nightSky.SetFloat("_SunDisk", 0f);
                nightSky.SetColor("_SkyTint", skyTint);
                nightSky.SetColor("_GroundColor", groundColor);
            }
            RenderSettings.skybox = nightSky;
        }
        else
        {
            RenderSettings.skybox = daySky;
        }
        DynamicGI.UpdateEnvironment();

        // Cam ve parlak yüzeyler akşam bake'li yansıma probe'larını gösteriyordu (pencerede hâlâ gün batımı).
        RenderSettings.reflectionIntensity = night ? reflectionMultiplier : 1f;
        foreach (ReflectionProbe probe in probes)
            probe.intensity = night ? reflectionMultiplier : 1f;

        foreach (Renderer r in darkenAtNight)
        {
            if (r == null)
                continue;
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            Color color = r.sharedMaterial.GetColor(BaseColorId);
            if (night)
            {
                color.r *= darkenMultiplier;
                color.g *= darkenMultiplier;
                color.b *= darkenMultiplier;
            }
            block.SetColor(BaseColorId, color);
            r.SetPropertyBlock(block);
        }
    }
}
