using UnityEngine;
using System.Linq;
using UnityEngine.Rendering;

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
    [Tooltip("Gece güneşin direkt ışığının çarpanı (0 = kapalı). Gece aynı ışık ay ışığı olur (aşağıdaki alanlar).")]
    [SerializeField] private float sunMultiplier = 0f;
    [Header("Ay ışığı (05.11)")]
    [Tooltip("Gece güneş ışığı yerine soğuk, zayıf ay ışığı: dışarıyı ve pencerelerden içeriyi aydınlatır (gölgeli).")]
    [SerializeField] private float moonIntensity = 0.9f;
    [SerializeField] private Color moonColor = new Color(0.55f, 0.68f, 1f);
    [SerializeField] private Vector3 moonEuler = new Vector3(38f, 150f, 0f);

    [Header("Pencereden görünen dış mekan")]
    [Tooltip("Zemin gibi bake edilmiş aydınlatmayla gündüz kalan dış yüzeyler: gece rengi karartılır (güneşi kapatmak bunları karartmıyor).")]
    [SerializeField] private Renderer[] darkenAtNight;
    [SerializeField] private float darkenMultiplier = 0.1f;

    [Tooltip("Gece yansıma probe'larının şiddeti (gündüz 1).")]
    [SerializeField] private float reflectionMultiplier = 0.1f;

    [Header("Gece iç ışıklar (05.11)")]
    [Tooltip("APV ışık senaryoları: gece iç lambalar kapalı bake edilmiş senaryo, gündüz akşamüstü bake'i.")]
    [SerializeField] private string dayScenario = "Gunduz";
    [SerializeField] private string nightScenario = "Gece";
    [Tooltip("Gündüz evin ek pozlaması (EV), parlaklık ayarına eklenir.")]
    [SerializeField] private float dayExposure = 0.9f;
    [Tooltip("(Kullanılmıyor, 05.11) Lamba parlaması artık RoomLights'ta; ışıklar Realtime ve oda düğmeleriyle açılıp kapanır.")]
    [SerializeField] private Renderer[] lampsOffAtNight;
    [Tooltip("Duvar/zemin/tavan lightmap'li: gece lambalar kapalı bake edilmiş lightmap'ler (sıra gündüzdekiyle aynı).")]
    [SerializeField] private Texture2D[] nightLightmapColor;
    [SerializeField] private Texture2D[] nightLightmapDir;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    private ReflectionProbe[] probes;
    private Light sun;
    private float dayIntensity;
    private Color dayColor;
    private Quaternion dayRotation;
    private Material daySky;
    private Material nightSky;
    private string wantedScenario;
    private LightmapData[] dayLightmaps;
    private LightmapData[] nightLightmaps;

    private void Awake()
    {
        sun = GetComponent<Light>();
        dayIntensity = sun.intensity;
        dayColor = sun.color;
        dayRotation = sun.transform.rotation;
        daySky = RenderSettings.skybox;
        probes = FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None);
        dayLightmaps = LightmapSettings.lightmaps;
        if (nightLightmapColor != null && nightLightmapColor.Length == dayLightmaps.Length && dayLightmaps.Length > 0)
        {
            nightLightmaps = new LightmapData[dayLightmaps.Length];
            for (int i = 0; i < dayLightmaps.Length; i++)
            {
                nightLightmaps[i] = new LightmapData
                {
                    lightmapColor = nightLightmapColor[i],
                    lightmapDir = nightLightmapDir != null && i < nightLightmapDir.Length ? nightLightmapDir[i] : null,
                    shadowMask = dayLightmaps[i].shadowMask
                };
            }
        }
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

    private void Update() => UpdateScenario();

    private void UpdateScenario()
    {
        ProbeReferenceVolume volume = ProbeReferenceVolume.instance;
        ProbeVolumeBakingSet set = volume.currentBakingSet;
        if (string.IsNullOrEmpty(wantedScenario) || set == null || volume.lightingScenario == wantedScenario
            || !set.lightingScenarios.Contains(wantedScenario))
            return;
        volume.lightingScenario = wantedScenario;
    }

    private void OnDestroy()
    {
        if (nightSky != null)
            Destroy(nightSky);
        GameSettings.SetSceneExposure(0f);
    }

    private void Apply()
    {
        bool night = DayRoutine.NightFell;
        sun.intensity = night ? Mathf.Max(dayIntensity * sunMultiplier, moonIntensity) : dayIntensity;
        sun.color = night ? moonColor : dayColor;
        sun.transform.rotation = night ? Quaternion.Euler(moonEuler) : dayRotation;        // Gündüz ev sadece güneşle aydınlanıyor, iç kısımlar karanlık kalıyordu: gündüz pozlama artar (gece realtime
        // lambalar zaten yeterli, gece 0).
        GameSettings.SetSceneExposure(night ? 0f : dayExposure);
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

        // Gece evin tek ışığı TV (Television.tvLight): iç lambaların bake'i olmayan senaryoya geçilir.
        // APV sahne açılırken geç yüklenebiliyor: senaryo Update'te uygulanır.
        wantedScenario = night ? nightScenario : dayScenario;
        UpdateScenario();
        if (nightLightmaps != null)
            LightmapSettings.lightmaps = night ? nightLightmaps : dayLightmaps;


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
