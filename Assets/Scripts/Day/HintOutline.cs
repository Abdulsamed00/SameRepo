using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Günün döngüsünde sıradaki etkileşim nesnesini, cüzdandaki gibi nabız atan beyaz kenarla (Shader Graph/OutlineNormal)
/// belirtir (10.11, kullanıcı: "döngüye göre etkileşime girmesi gereken objeleri outline ile belirt"). Arayüz yazı
/// yazmaz; yön sadece bu parıltıdır. Kenar, hedef renderer'ın kendi malzemelerine dokunmadan altına eklenen çocuk bir
/// mesh'tir (tüm alt mesh'lere ayrı kenar malzemesi), sadece parlarken açıktır.
/// Adımlar (hepsi gece, evde):
/// <list type="bullet">
/// <item><b>Door</b>: sabah, çıkış kilidi açılınca (cüzdan alındı) giriş kapısı.</item>
/// <item><b>WardrobeClosed</b>: günün ürünü alındı, giyilmedi, dolap kapalı; ayrıca yatmadan önce ev kıyafeti ve sabah giyme için (açıkken de): dolabın kendisi.</item>
/// <item><b>WardrobeClothing</b>: dolap açık, günün kıyafeti asılı: kıyafet (<see cref="WardrobeClothing"/> üzerinde).</item>
/// <item><b>BedroomSwitch</b>: yatak odası ışığı açıkken uyunacaksa ya da poz verilecekken ışık kapalıysa: yatak odası düğmesi.</item>
/// <item><b>Bed</b>: uyunabilir ve yatak odası ışığı kapalı: yatak.</item>
/// </list>
/// Ayna (poz) kendi kenarını <see cref="MirrorPose"/> üzerinden gösterir.
/// </summary>
public class HintOutline : MonoBehaviour
{
    public enum Step { Door, WardrobeClosed, WardrobeClothing, BedroomSwitch, Bed }

    [SerializeField] private Step step;
    [Tooltip("Kenarın çizileceği renderer'lar (MeshRenderer). Boşsa altındaki tüm MeshRenderer'lar.")]
    [SerializeField] private Renderer[] targets;
    [Tooltip("Same/OutlineNormal malzemesi (Assets/Shaders/Outline/Ayna_Outline.mat).")]
    [SerializeField] private Material outlineMaterial;
    [SerializeField] private float outlineWidth = 0.006f;
    [SerializeField] private float outlineIntensity = 3f;
    [SerializeField] private float pulseSpeed = 2.2f;
    [Tooltip("Yatak odasının ışık kimliği (RoomLights.RoomId).")]
    [SerializeField] private string bedroomId = "Yatak";

    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

    private readonly List<GameObject> shells = new List<GameObject>();
    private Material outline;
    private float glow;
    private SceneDoor door;
    private Openable wardrobe;
    private WardrobeClothing clothing;
    private Bed bed;
    private RoomLights bedroom;

    private void Awake()
    {
        door = GetComponent<SceneDoor>();
        clothing = GetComponent<WardrobeClothing>();
        wardrobe = clothing != null ? GetComponentInParent<Openable>() : GetComponent<Openable>();
        bed = GetComponent<Bed>();
        if (outlineMaterial == null)
            return;

        outline = new Material(outlineMaterial);
        outline.SetColor(OutlineColorId, Color.black);
        outline.SetFloat(OutlineWidthId, outlineWidth);

        IEnumerable<Renderer> list = targets != null && targets.Length > 0 ? targets : GetComponentsInChildren<MeshRenderer>(true);
        foreach (Renderer r in list)
        {
            MeshFilter filter = r != null ? r.GetComponent<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null)
                continue;
            GameObject shell = new GameObject("Ipucu_Kenari");
            shell.layer = r.gameObject.layer;
            shell.transform.SetParent(r.transform, false);
            shell.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            MeshRenderer mr = shell.AddComponent<MeshRenderer>();
            Material[] mats = new Material[Mathf.Max(1, filter.sharedMesh.subMeshCount)];
            for (int i = 0; i < mats.Length; i++)
                mats[i] = outline;
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            shell.SetActive(false);
            shells.Add(shell);
        }
    }

    private RoomLights Bedroom
    {
        get
        {
            if (bedroom == null)
            {
                foreach (RoomLights room in FindObjectsByType<RoomLights>(FindObjectsSortMode.None))
                    if (room.RoomId == bedroomId)
                        bedroom = room;
            }
            return bedroom;
        }
    }

    // Uyku vakti: gece, günün işleri bitti (ilk gecede reklam izlendi).
    private static bool SleepPhase => DayRoutine.CanSleep && (!DayRoutine.Prologue || DayRoutine.NightAdSeen);

    // Giyildi, poz verilmedi (ayna kenarını MirrorPose çizer; ışık kapalıysa önce düğme).
    private static bool PosePhase => DayRoutine.NightFell && !DayRoutine.Prologue && !DayRoutine.FinalMorning
                                     && DayRoutine.BoughtToday && DayRoutine.WornToday && !DayRoutine.PosedToday;

    private static bool WearPhase => DayRoutine.NightFell && !DayRoutine.Prologue && !DayRoutine.FinalMorning
                                     && DayRoutine.BoughtToday && !DayRoutine.WornToday;

    private bool Wanted()
    {
        if (PauseMenu.IsPaused)
            return false;
        switch (step)
        {
            case Step.Door:
                return door != null && !door.Locked && !DayRoutine.NightFell;
            case Step.WardrobeClosed:
                if (wardrobe == null)
                    return false;
                // Kıyafet değiştirme (ev kıyafeti / sabah giyme) dolap açıkken de parlar: değiştirme açık dolaptan yapılır.
                return (WearPhase && !wardrobe.IsOpen) || DayRoutine.HomeChangePending || DayRoutine.DressPending;
            case Step.WardrobeClothing:
                return WearPhase && clothing != null && clothing.Product == GameDay.Product && clothing.Hanging
                       && wardrobe != null && wardrobe.IsOpen && !clothing.Inspecting;
            case Step.BedroomSwitch:
            {
                RoomLights room = Bedroom;
                if (room == null)
                    return false;
                return (SleepPhase && room.IsOn) || (PosePhase && !room.IsOn);
            }
            case Step.Bed:
            {
                RoomLights room = Bedroom;
                return bed != null && SleepPhase && room != null && !room.IsOn && !string.IsNullOrEmpty(bed.Prompt);
            }
        }
        return false;
    }

    private void Update()
    {
        if (outline == null)
            return;
        bool wanted = Wanted();
        glow = Mathf.MoveTowards(glow, wanted ? 1f : 0f, 2f * Time.unscaledDeltaTime);
        bool visible = glow > 0.001f;
        foreach (GameObject shell in shells)
            if (shell != null && shell.activeSelf != visible)
                shell.SetActive(visible);
        if (!visible)
            return;

        float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * pulseSpeed);
        float v = outlineIntensity * (0.45f + 0.55f * pulse) * glow;
        outline.SetColor(OutlineColorId, new Color(v, v, v, 1f));
    }
}
