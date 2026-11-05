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
    [Tooltip("Pencerelerden giren ay ışığı süzmeleri (görsel ışın); sadece gece açık.")]
    [SerializeField] private GameObject moonBeams;
    [Tooltip("Pencerelerden giren güneş ışığı süzmeleri; sadece gündüz açık.")]
    [SerializeField] private GameObject sunBeams;

    [Header("Oda ışığı açıkken ay (06.11)")]
    [Tooltip("Lambası açık odanın pencerelerinden ay girmez: pencerenin gölge perdesi (moonBeams altında Engel_N, ShadowsOnly) açılır, süzmesi (Suzme_N) söner.")]
    [SerializeField] private float moonDimSpeed = 3f;
    [Tooltip("Oda ışıkları ve dünya uzayında oda sınırları (x/z min-max). Pencere hangi odadaysa o odanın ışığına bakar.")]
    [SerializeField] private RoomArea[] rooms;

    [System.Serializable]
    private struct RoomArea
    {
        public RoomLights lights;
        public Vector2 min;
        public Vector2 max;
    }
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

    private void Update()
    {
        UpdateScenario();
        UpdateMoonDimming();
    }

    private class MoonWindow
    {
        public Renderer beam;
        public Renderer blocker;
        public RoomLights room;
        public float factor = 1f;
    }

    private MoonWindow[] moonWindows;
    private Color moonBeamColor;
    private float nightSunIntensity;
    private MaterialPropertyBlock beamBlock;

    // Lambası açık odanın pencerelerinden ay girmez (06.11: lamba yanarken zeminde mavi ay lekesi saçma duruyordu,
    // ilk sürüm kameranın odasına bakıyordu, koridordan bakınca leke görünüyordu). Ay tek yönlü ışık olduğu için oda
    // başına söndürülemez; onun yerine o odanın pencerelerine görünmez gölge perdesi (Engel_N) açılır, ışık içeri
    // giremez. Süzme (Suzme_N) yumuşakça söner. Bake'li yansıyan ay ışığı kalır (lamba bastırır).
    private void UpdateMoonDimming()
    {
        if (!DayRoutine.NightFell || moonBeams == null)
            return;
        if (moonWindows == null)
            CollectMoonWindows();

        foreach (MoonWindow w in moonWindows)
        {
            bool lit = w.room != null && w.room.IsOn;
            w.factor = Mathf.MoveTowards(w.factor, lit ? 0f : 1f, moonDimSpeed * Time.deltaTime);
            if (w.blocker != null)
                w.blocker.enabled = lit;
            if (w.beam != null)
            {
                beamBlock.SetColor(BaseColorId, moonBeamColor * w.factor);
                w.beam.SetPropertyBlock(beamBlock);
            }
        }
    }

    private void CollectMoonWindows()
    {
        beamBlock = new MaterialPropertyBlock();
        var list = new System.Collections.Generic.List<MoonWindow>();
        foreach (Transform child in moonBeams.transform)
        {
            if (!child.name.StartsWith("Engel_"))
                continue;
            string id = child.name.Substring("Engel_".Length);
            Transform beam = moonBeams.transform.Find("Suzme_" + id);
            var w = new MoonWindow
            {
                blocker = child.GetComponent<Renderer>(),
                beam = beam != null ? beam.GetComponent<Renderer>() : null,
                room = RoomAt(child.position)
            };
            if (w.blocker != null)
                w.blocker.enabled = false;
            list.Add(w);
        }
        moonWindows = list.ToArray();
        Renderer first = moonWindows.Select(w => w.beam).FirstOrDefault(r => r != null);
        moonBeamColor = first != null ? first.sharedMaterial.GetColor(BaseColorId) : Color.white;
    }

    private RoomLights RoomAt(Vector3 p)
    {
        if (rooms == null)
            return null;
        foreach (RoomArea room in rooms)
            if (p.x >= room.min.x && p.x <= room.max.x && p.z >= room.min.y && p.z <= room.max.y)
                return room.lights;
        return null;
    }

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
        nightSunIntensity = Mathf.Max(dayIntensity * sunMultiplier, moonIntensity);
        sun.intensity = night ? nightSunIntensity : dayIntensity;
        sun.color = night ? moonColor : dayColor;
        sun.transform.rotation = night ? Quaternion.Euler(moonEuler) : dayRotation;        // Gündüz ev sadece güneşle aydınlanıyor, iç kısımlar karanlık kalıyordu: gündüz pozlama artar (gece realtime
        // lambalar zaten yeterli, gece 0).
        GameSettings.SetSceneExposure(night ? 0f : dayExposure);
        if (moonBeams != null)
            moonBeams.SetActive(night);
        if (sunBeams != null)
            sunBeams.SetActive(!night);
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
