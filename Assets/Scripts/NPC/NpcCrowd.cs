using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

/// <summary>Kıyafet yuvası: seçeneklerden biri açılır, diğerleri kapanır. Bir seçenek '+' ile birden çok parça olabilir.</summary>
[Serializable]
public class NpcOutfitSlot
{
    public string name;
    public string[] options;
    [Range(0f, 1f)] public float emptyChance;

    public NpcOutfitSlot(string name, float emptyChance, params string[] options)
    {
        this.name = name;
        this.emptyChance = emptyChance;
        this.options = options;
    }
}

/// <summary>Bir NPC türü (erkek/kadın): prefab, Animator state adları, yürüme klibinin doğal hızı ve kıyafet yuvaları.</summary>
[Serializable]
public class NpcProfile
{
    public GameObject prefab;
    [Min(0f)] public float weight = 1f;
    public string idleState;
    public string walkState;
    public string sitState;
    public string talkState = "Standing_Talk";
    [Tooltip("Yürüme klibinin animasyon hızı 1'deyken ayakların kaymadığı yürüme hızı (m/sn).")]
    public float walkClipSpeed = 1.2f;
    [Tooltip("Bu profilin yürüme hızı aralığı (m/sn); boşsa (0) kalabalığın speedRange'i. Klibin doğal temposuna yakın tutulur, " +
             "yoksa animasyon ya çok hızlı oynar (koşturur gibi) ya da ayaklar yerde kayar (11.11).")]
    public Vector2 speedRange;
    public NpcOutfitSlot[] outfit;

    public void ApplyRandomOutfit(Transform root)
    {
        if (outfit == null)
            return;

        foreach (NpcOutfitSlot slot in outfit)
        {
            foreach (string option in slot.options)
                SetParts(root, option, false);
        }
        foreach (NpcOutfitSlot slot in outfit)
        {
            if (slot.options.Length == 0 || Random.value < slot.emptyChance)
                continue;
            SetParts(root, slot.options[Random.Range(0, slot.options.Length)], true);
        }
    }

    private static void SetParts(Transform root, string option, bool on)
    {
        foreach (string part in option.Split('+'))
        {
            Transform t = root.Find(part);
            if (t != null)
                t.gameObject.SetActive(on);
        }
    }
}

/// <summary>
/// Bir sahnenin NPC kalabalığı (sokak, mağaza). Sahne açılınca <see cref="population"/> kadar NPC'yi gezinme noktalarına
/// ve boş <see cref="NpcSpot"/>'lara dağıtır, her birine rastgele kıyafet giydirir. NPC'ler sırayla gezinir, noktalarda
/// vakit geçirir; <see cref="exits"/> doluysa birkaç hedeften sonra bir çıkışa yürüyüp kaybolur, yerine oyuncunun
/// görmediği bir çıkıştan yenisi gelir. Oyuncuya NavMeshObstacle eklenir ki NPC'ler onun etrafından dolaşsın.
/// </summary>
public class NpcCrowd : MonoBehaviour
{
    public enum GoalKind
    {
        Wander,
        Spot,
        Leave,
        /// <summary>Finalde oyuncunun çevresindeki halkada yerini alıp ona dönük durur.</summary>
        Ring
    }

    public struct Goal
    {
        public GoalKind kind;
        public Vector3 point;
        public Quaternion rotation;
        public NpcSpot spot;
        /// <summary>Ring: yerinde konuşma klibini oynar.</summary>
        public bool talk;
    }

    [SerializeField] private NpcProfile[] profiles =
    {
        new NpcProfile
        {
            idleState = "Man_Idle",
            walkState = "Man_Walk",
            sitState = "Bench_Sit",
            // 11.11: Erkek_Walk_Duz'da basan ayağın (parmak) geri kayma hızı sol 1.59 / sağ 1.49 m/s, ortalama 1.53
            // (klip 240 örnekte, ayak zemindeyken ölçüldü).
            walkClipSpeed = 1.53f,
            speedRange = new Vector2(1.1f, 1.45f),
            outfit = new[]
            {
                new NpcOutfitSlot("Saç", 0f, "shortCutHair", "CurlyHairBlack", "CurlyHairBrown", "Hat", "OrangeHat"),
                new NpcOutfitSlot("Sakal", 0.4f, "beard", "yellowbeard"),
                new NpcOutfitSlot("Üst", 0f, "BlackJacket+Tshirt(just combined with jacket)", "Meshy_Navy Crewneck Sweater_Mesh_0.021"),
                new NpcOutfitSlot("Alt", 0f, "Pants", "BlueJean"),
                new NpcOutfitSlot("Ayakkabı", 0f, "ShoeLeft+ShoeRight", "BlackShoeLeft+BlackShoeRight", "BrownShoeLeft+BrownShoeRight"),
            }
        },
        new NpcProfile
        {
            idleState = "Women_Idle",
            walkState = "Women_Walking",
            sitState = "Bench_Sit",
            // 11.11: Walk_Duz (Kız_npc Walk) basan ayağın geri kayma hızı ~1.18 m/s (sol 1.20 / sağ 1.17). 0.76'da
            // animasyon 1.5 kat hızlı oynuyor, basan ayak yürüme hızının %62'si kadar kayıyordu.
            walkClipSpeed = 1.18f,
            speedRange = new Vector2(0.85f, 1.1f),
            outfit = new[]
            {
                new NpcOutfitSlot("Saç", 0f, "BlondeHair", "CurlyHairBlack", "CurlyHairBrown", "TopuzSac"),
                new NpcOutfitSlot("Üst", 0f, "Coat", "Tshırt", "Tshırt_Blue", "Tshırt_Pink"),
                new NpcOutfitSlot("Alt", 0f, "BlueJean", "EsofmanAltı", "EsofmanAltı_Yesil", "Siyah_Tayt"),
                new NpcOutfitSlot("Ayakkabı", 0f, "ShoeLeft+ShoeRight", "BlackShoeLeft+BlackShoeRight", "BrownShoeLeft+BrownShoeRight"),
                new NpcOutfitSlot("Çanta", 0.7f, "Bag"),
                new NpcOutfitSlot("Gözlük", 0.8f, "Glasses"),
            }
        },
    };

    [SerializeField] private int population = 30;
    [Tooltip("Gezinme hedefleri: bu objelerin altındaki tüm alt objeler (ör. Rota_* noktaları).")]
    [SerializeField] private Transform[] wanderRoots;
    [Tooltip("NpcSpot'ların arandığı kök.")]
    [SerializeField] private Transform spotsRoot;
    [Tooltip("Ayrılma/geliş noktaları. Boşsa NPC'ler sahneden hiç ayrılmaz (mağaza).")]
    [SerializeField] private Transform[] exits;

    [Header("Davranış")]
    [Range(0f, 1f)] [SerializeField] private float spotChance = 0.45f;
    [Tooltip("Gezinme hedefi NPC'nin en fazla bu kadar uzağından seçilir (m). Kalabalık caddeye yayılmış kalsın.")]
    [SerializeField] private float wanderRange = 40f;
    [SerializeField] private float spotRange = 40f;
    [SerializeField] private float wanderJitter = 1.5f;
    [Tooltip("Başlangıçta noktalarda (bank, vitrin) başlayan NPC oranı.")]
    [Range(0f, 1f)] [SerializeField] private float startAtSpotChance = 0.35f;
    [Tooltip("NPC kaç hedeften sonra çıkışa gider (min-max).")]
    [SerializeField] private Vector2Int goalsBeforeLeave = new Vector2Int(2, 6);
    [SerializeField] private Vector2 speedRange = new Vector2(1.05f, 1.45f);

    [Header("Cadde Akışı")]
    [Tooltip("Açıksa her gezinme kökü (Rota_*) bir yürüme şerididir: NPC şeridinde tek yöne (z boyunca) yürür, rastgele " +
             "noktalar arasında çapraz gidip gelmez; sadece önündeki, kendi tarafındaki noktalarda durur, şeridin sonunda " +
             "çıkışa gider. Orta şeritler sağdan akar (+x şeridi +z'ye), kaldırımlarda iki yön de var.")]
    [SerializeField] private bool streetFlow;
    [Tooltip("Akışta önündeki noktaya (bank, vitrin) uğrama olasılığı.")]
    [Range(0f, 1f)] [SerializeField] private float flowSpotChance = 0.25f;
    [Tooltip("Akışta uğranan noktanın en fazla bu kadar önünde olabileceği (m, z boyunca).")]
    [SerializeField] private float flowSpotAhead = 18f;

    [Header("Yeniden Doğma")]
    [SerializeField] private Vector2 respawnDelay = new Vector2(2f, 6f);
    [Tooltip("Oyuncuya bundan yakın ve görüş alanındaki çıkışlarda NPC doğmaz/kaybolmaz.")]
    [SerializeField] private float hiddenDistance = 35f;

    [Header("Final")]
    [Tooltip("Final başlayınca görüş dışında ayrıca doğan NPC sayısı (oyuncunun çevresini kalabalık doldursun).")]
    [SerializeField] private int finaleExtra = 40;
    [Tooltip("Finalde NPC'ler oyuncunun çevresinde bu yarıçap içinde dolaşır (m).")]
    [SerializeField] private float finaleRadius = 9f;
    [Tooltip("Oyuncunun dibinde bu kadar boşluk kalır (m).")]
    [SerializeField] private float finaleClearance = 1.6f;
    [Tooltip("Ek NPC'lerin doğduğu bant: oyuncunun cadde boyunca bu kadar önü/arkası (m, min-max).")]
    [SerializeField] private Vector2 finaleSpawnBand = new Vector2(9f, 24f);
    [Tooltip("Caddenin NPC şeridi (dünya x, min-max): ek NPC'ler ve hedefler bunun içinde.")]
    [SerializeField] private Vector2 finaleLaneX = new Vector2(-4.5f, 4.5f);
    [Tooltip("Caddenin boyu (dünya z, min-max): dışında doğan ek NPC'ler caddenin başındaki binanın kapısına yığılıyordu.")]
    [SerializeField] private Vector2 finaleLaneZ = new Vector2(3f, 118f);

    [Header("Final Kalabalığı")]
    [Tooltip("Oyuncuya benzeyen (aynı ürünler, aynı sarı saç) kadın sayısı. Kalabalığın en iç sırasını onlar alır.")]
    [SerializeField] private int ringCount = 26;
    [Tooltip("Oyuncunun çevresinde boş kalan alanın yarıçapı (m); açıya göre +innerJitter kadar düzensiz.")]
    [SerializeField] private float ringRadius = 0.95f;
    [SerializeField] private float innerJitter = 0.7f;
    [Tooltip("Kalabalıkta iki kişi arası mesafe (m, min-max): her yer kendi aralığını rastgele alır.")]
    [SerializeField] private Vector2 crowdSpacing = new Vector2(0.58f, 0.86f);
    [Tooltip("Kalabalığın yayılabildiği şerit (dünya x): yol + kaldırımın sokak eşyası olmayan kısmı.")]
    [SerializeField] private Vector2 crowdLaneX = new Vector2(-7.4f, 7.4f);
    [Tooltip("Kalabalığın dış kenarı bu kadar düzensiz (m): yerler mesafe + bu kadar rastgele ile sıralanır.")]
    [SerializeField] private float edgeRaggedness = 1.6f;
    [Tooltip("Oyuncuya bakışta sapma (derece).")]
    [SerializeField] private float facingJitter = 18f;
    [Tooltip("Oyuncuya değil yanındakine dönük duranların oranı (dış sıralarda).")]
    [Range(0f, 1f)] [SerializeField] private float lookAwayChance = 0.12f;
    [Tooltip("Dış sıralarda yanındakiyle konuşanların oranı.")]
    [Range(0f, 1f)] [SerializeField] private float talkChance = 0.1f;
    [Tooltip("Benzerlerin görüş dışında doğduğu bant: oyuncunun cadde boyunca önü/arkası (m, min-max).")]
    [SerializeField] private Vector2 ringSpawnBand = new Vector2(7f, 16f);
    [SerializeField] private float ringWalkSpeed = 1.5f;
    [Tooltip("Benzerler için kullanılan profil (kadın).")]
    [SerializeField] private int ringProfile = 1;
    [Tooltip("Benzerlerin hepsine giydirilen parça (oyuncunun saçı); aynı yuvadaki diğer seçenekler kapanır.")]
    [SerializeField] private string ringLook = "BlondeHair";
    [Tooltip("Finale başlarken bundan uzaktaki NPC'ler kalabalığa katılmaz, uzakta gezinir (m).")]
    [SerializeField] private float joinDistance = 28f;
    [Tooltip("Açıksa kalabalık SADECE oyuncuya benzeyen kadınlardan oluşur (07.11, kullanıcı isteği): sokaktaki diğer " +
             "NPC'ler uzaklaşıp kamera görmeyince kaybolur, sabit NPC'ler (kafe, bank, telefon) görülmediği an gizlenir; " +
             "ek doğanlar (finaleExtra) da benzer olur.")]
    [SerializeField] private bool onlyLookalikes = true;
    [Tooltip("onlyLookalikes: oyuncuya bundan yakın sabit NPC'ler (kalabalık dışı) görülmediği an gizlenir (m).")]
    [SerializeField] private float bystanderRadius = 40f;

    [Header("Final: Kendini Kaybetme")]
    [Tooltip("Kalabalığın en iç sırası oyuncunun kendi modelinin birebir kopyası (aynı yüz, saç, ürünler, aynı animasyon): " +
             "kamera hareket edince oyuncu kendini ayırt edemesin. Dış sıralar benzer kadın NPC'ler. 0 = kapalı.")]
    [SerializeField] private int playerCopyCount = 40;
    [Tooltip("Herkes mağazaya döndüğünde bakış sapması (derece).")]
    [SerializeField] private float conformJitter = 5f;
    [Tooltip("Mağazaya dönüş kalabalığa yayılır: her biri bu kadar gecikmeyle (sn, min-max) döner.")]
    [SerializeField] private Vector2 conformDelay = new Vector2(0f, 2.2f);

    private GameObject copyTemplate;
    private NpcProfile copyProfile;
    private readonly HashSet<NpcAgent> copies = new HashSet<NpcAgent>();
    // En içteki bu kadar yer oyuncu kopyalarına ayrılır (oyuncunun dibi onlarla dolsun).
    private int reservedInner;
    private bool conforming;
    private Quaternion conformRotation;

    private bool finale;
    private readonly List<NpcAgent> outsiders = new List<NpcAgent>();
    private readonly List<GameObject> bystanders = new List<GameObject>();
    private readonly HashSet<NpcAgent> arrived = new HashSet<NpcAgent>();
    private bool frozen;
    private Vector3 finaleCenter;
    private NavMeshPath finalePath;

    private struct CrowdSlot
    {
        public Vector3 point;
        public float key;
        public NpcAgent owner;
        /// <summary>Yerine varamayıp yakınında duran NPC: bu yere başkası gelirse içine girerdi.</summary>
        public NpcAgent blocker;
        /// <summary>Oyuncu tam merkeze varamayıp bu yerin dibinde durdu: NPC gelirse karakterin içine girerdi.</summary>
        public bool nearPlayer;

        public bool Free => owner == null && blocker == null && !nearPlayer;
    }

    // Oyuncunun ortaya yürüdüğü yol (ara sokaktan çıktığı yer → merkez) bu kadar geniş boş tutulur (m, yarı genişlik).
    private const float PathClearance = 0.8f;
    // Duran NPC'nin bu kadar yakınındaki boş yerler kapanır (m).
    private const float BlockRadius = 0.55f;
    // Doğan NPC'ler arası en az mesafe (m): üst üste doğanlar birbirini itip titriyordu.
    private const float SpawnSpacing = 0.8f;
    private readonly List<Vector3> finaleSpawns = new List<Vector3>();
    private Vector3 finalePathStart;
    private bool playerArrived;

    private readonly List<CrowdSlot> crowdSlots = new List<CrowdSlot>();
    private readonly HashSet<NpcAgent> lookalikes = new HashSet<NpcAgent>();
    private readonly HashSet<NpcAgent> joiners = new HashSet<NpcAgent>();

    /// <summary>Kalabalığın çoğu yerini aldı (final kamerası bunu bekler).</summary>
    public bool RingFormed => joiners.Count == 0 || arrived.Count >= Mathf.CeilToInt(Mathf.Min(joiners.Count, crowdSlots.Count) * 0.88f);

    /// <summary>Finalin son anı: herkes olduğu pozda donar.</summary>
    public bool Frozen => frozen;

    private readonly List<Vector3> wanderPoints = new List<Vector3>();
    // Akış şeritleri: her gezinme kökünün noktaları, z'ye göre sıralı.
    private readonly List<List<Vector3>> routes = new List<List<Vector3>>();
    private readonly List<float> routeX = new List<float>();
    private readonly List<NpcSpot> spots = new List<NpcSpot>();
    private readonly List<NpcAgent> agents = new List<NpcAgent>();
    private Transform player;
    private Camera playerCamera;
    private float totalWeight;

    private void Start()
    {
        foreach (Transform root in wanderRoots)
        {
            if (root == null)
                continue;
            var route = new List<Vector3>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>())
            {
                if (t.childCount == 0 && NavMesh.SamplePosition(t.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                    wanderPoints.Add(hit.position);
                    route.Add(hit.position);
                }
            }
            if (route.Count >= 2)
            {
                route.Sort((a, b) => a.z.CompareTo(b.z));
                float x = 0f;
                foreach (Vector3 p in route)
                    x += p.x;
                routes.Add(route);
                routeX.Add(x / route.Count);
            }
        }
        if (routes.Count == 0)
            streetFlow = false;
        if (spotsRoot != null)
            spots.AddRange(spotsRoot.GetComponentsInChildren<NpcSpot>());
        foreach (NpcProfile p in profiles)
        {
            if (p.prefab != null)
                totalWeight += p.weight;
        }

        var fpc = FindAnyObjectByType<FirstPersonController>();
        if (fpc != null)
        {
            player = fpc.transform;
            playerCamera = fpc.GetComponentInChildren<Camera>();
            if (player.GetComponent<NavMeshObstacle>() == null)
            {
                var o = player.gameObject.AddComponent<NavMeshObstacle>();
                o.shape = NavMeshObstacleShape.Capsule;
                o.radius = 0.35f;
                o.height = 1.8f;
                o.center = new Vector3(0f, 0.9f, 0f);
                o.carving = false;
            }
        }

        AddBodiesToStaticNpcs();

        if (wanderPoints.Count == 0 || totalWeight <= 0f)
        {
            Debug.LogWarning($"{name}: gezinme noktası ya da NPC prefab'ı yok, kalabalık kapalı.");
            return;
        }

        // Grafik kalitesi düştükçe kalabalık azalır (Düşük %50, Orta %75).
        int count = Mathf.Max(1, Mathf.RoundToInt(population * GameSettings.NpcFactor));
        for (int i = 0; i < count; i++)
            SpawnInitial();
    }

    // Sahnedeki sabit NPC'lerin (kafe, sohbet, telefon, kasiyer...) çoğunda collider yoktu, oyuncu içlerinden geçiyordu
    // (10.11). Kökte collider'ı olmayan her animatörlü NPC'ye Ignore Raycast katmanında (etkileşim ışınını kesmez) kapsül eklenir.
    private void AddBodiesToStaticNpcs()
    {
        var seen = new HashSet<Transform>();
        foreach (NpcAnimationPlayer a in FindObjectsByType<NpcAnimationPlayer>(FindObjectsSortMode.None))
            AddBody(a.transform, seen);
        foreach (NpcDailyOutfit o in FindObjectsByType<NpcDailyOutfit>(FindObjectsSortMode.None))
            AddBody(o.transform, seen);
    }

    private static void AddBody(Transform root, HashSet<Transform> seen)
    {
        if (!seen.Add(root) || root.GetComponent<NpcAgent>() != null || root.GetComponent<Collider>() != null
            || root.GetComponentInChildren<Animator>() == null)
            return;
        var c = root.gameObject.AddComponent<CapsuleCollider>();
        c.center = new Vector3(0f, 0.9f, 0f);
        c.height = 1.75f;
        c.radius = 0.28f;
        root.gameObject.layer = 2;
    }

    private void SpawnInitial()
    {
        NpcSpot startSpot = null;
        if (Random.value < startAtSpotChance)
        {
            var free = spots.FindAll(s => s.FreeSlots > 0);
            if (free.Count > 0)
                startSpot = free[Random.Range(0, free.Count)];
        }

        NavMeshHit hit = default;
        bool found = false;
        for (int attempt = 0; attempt < 8 && !found; attempt++)
        {
            Vector3 pos = startSpot != null ? startSpot.transform.position : Jitter(wanderPoints[Random.Range(0, wanderPoints.Count)]);
            found = NavMesh.SamplePosition(pos, out hit, 2f, NavMesh.AllAreas) && (startSpot != null || !Occupied(hit.position, 1.3f));
        }
        if (!found)
            return;
        Spawn(hit.position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), startSpot);
    }

    private NpcAgent Spawn(Vector3 position, Quaternion rotation, NpcSpot startSpot, NpcProfile forced = null)
    {
        NpcProfile profile = forced ?? PickProfile();
        GameObject go = Instantiate(profile.prefab, position, rotation, transform);
        go.name = profile.prefab.name;
        if (!go.activeSelf)
            go.SetActive(true);

        // Prefab'daki tek animasyonlu eski bileşenler kalabalıkta kullanılmaz.
        foreach (var old in go.GetComponents<NpcAnimationPlayer>())
            DestroyImmediate(old);
        foreach (var old in go.GetComponents<NpcPatrol>())
            DestroyImmediate(old);

        profile.ApplyRandomOutfit(go.transform);

        var npc = go.AddComponent<NpcAgent>();
        Vector2 range = profile.speedRange.y > 0f ? profile.speedRange : speedRange;
        npc.Init(this, profile, Random.Range(range.x, range.y));
        npc.GoalLimit = Random.Range(goalsBeforeLeave.x, goalsBeforeLeave.y + 1);
        if (streetFlow)
            AssignRoute(npc, position);
        npc.StartAt(startSpot);
        agents.Add(npc);
        return npc;
    }

    // Yön caddenin içine doğru (doğduğu uç ya da ara sokaktan), şerit o yönde akan en yakın şerit.
    private void AssignRoute(NpcAgent npc, Vector3 position)
    {
        float minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (List<Vector3> r in routes)
        {
            minZ = Mathf.Min(minZ, r[0].z);
            maxZ = Mathf.Max(maxZ, r[r.Count - 1].z);
        }
        float rel = Mathf.InverseLerp(minZ, maxZ, position.z);
        // Uçlarda içeri doğru; ortada rastgele.
        int heading = rel < 0.15f ? 1 : rel > 0.85f ? -1 : (Random.value < 0.5f ? 1 : -1);
        npc.Heading = heading;
        npc.Route = NearestRoute(position, heading);
    }

    // Tüm şeritler sağdan akar: +x şeritleri +z'ye, -x şeritleri -z'ye (kaldırımlar 11.11'den beri dahil).
    private bool RouteAllows(int route, int heading)
    {
        // Kaldırımlar da tek yönlü, sağdan akış (11.11, kullanıcı: "NPC'ler birbirini itiyor"): kaldırımdaki bank, lamba,
        // saksı ve kafe teraslarının arasındaki dar geçitlerde karşı yönden gelen iki NPC sığmıyor, ajanlar iç içe girip
        // ayrılırken birbirini itiyordu. Artık +x tarafı (orta şerit + kaldırım) +z'ye, -x tarafı -z'ye akar.
        return routeX[route] * heading >= 0f;
    }

    private int NearestRoute(Vector3 position, int heading)
    {
        int best = 0;
        float bestD = float.MaxValue;
        for (int i = 0; i < routes.Count; i++)
        {
            if (!RouteAllows(i, heading))
                continue;
            float d = Mathf.Abs(routeX[i] - position.x);
            if (d < bestD)
            {
                bestD = d;
                best = i;
            }
        }
        return best;
    }

    private NpcProfile PickProfile()
    {
        float r = Random.value * totalWeight;
        foreach (NpcProfile p in profiles)
        {
            if (p.prefab == null)
                continue;
            r -= p.weight;
            if (r <= 0f)
                return p;
        }
        foreach (NpcProfile p in profiles)
        {
            if (p.prefab != null)
                return p;
        }
        return null;
    }

    /// <summary>
    /// Oyun sonu: NPC'ler artık bank/vitrin noktalarına gitmez, sahneden ayrılmaz; hepsi <paramref name="center"/>
    /// (caddenin ortasındaki oyuncu) çevresinde dolaşır. Ayrıca kameranın görmediği yerlerden <see cref="finaleExtra"/>
    /// kadar NPC doğar. Son ürün alındığı için hepsi oyuncuyla aynı giyinir (<see cref="NpcFashion"/>): oyuncu kendine
    /// benzeyenlerin kalabalığında kalır.
    /// </summary>
    public void BeginFinale(Vector3 center) => BeginFinale(center, center);

    /// <param name="playerPosition">Oyuncunun final başladığı yer: buradan merkeze uzanan yol, oyuncu varana kadar
    /// (<see cref="PlayerArrived"/>) boş tutulur. Yoksa erken gelen NPC'ler yolunu kesiyor, karakter kalabalığa takılıp
    /// ortaya varamıyordu (kalabalık da yanlış yerin çevresinde toplanmış oluyordu).</param>
    public void BeginFinale(Vector3 center, Vector3 playerPosition)
    {
        if (finale || wanderPoints.Count == 0)
            return;
        finale = true;
        finaleCenter = center;
        finalePathStart = playerPosition;
        finalePath = new NavMeshPath();
        finaleSpawns.Clear();
        // Uzaktakiler (görünmezler) kalabalığa yürüyüp gelmeye çalışmaz, yerler yakındakilere ve yeni doğanlara kalır.
        // Sadece benzerler modunda sokaktaki eski NPC'ler hiç katılmaz: oyuncudan uzaklaşır, görülmeyince kaybolur.
        foreach (NpcAgent agent in agents)
        {
            Vector3 d = agent.transform.position - center;
            d.y = 0f;
            if (onlyLookalikes)
                outsiders.Add(agent);
            else if (d.sqrMagnitude < joinDistance * joinDistance)
                joiners.Add(agent);
            agent.Interrupt();
        }

        Camera cam = playerCamera != null ? playerCamera : Camera.main;
        if (onlyLookalikes)
        {
            CollectBystanders(center);
            int total = ringCount + finaleExtra;
            // Önce oyuncunun kopyaları, en yakın bantta (iç sıraları onlar alır), sonra benzer kadınlar.
            int copyCount = Mathf.Min(playerCopyCount, total);
            if (copyCount > 0 && BuildCopyProfile())
                SpawnRing(center, cam, copyCount, copyProfile, new Vector2(ringSpawnBand.x, ringSpawnBand.x + 7f), true);
            reservedInner = copies.Count;
            SpawnRing(center, cam, total - copies.Count);
            BuildCrowdSlots(center, lookalikes.Count + 6);
            StartCoroutine(ClearOutsiders());
            return;
        }

        BuildCrowdSlots(center, ringCount + finaleExtra + joiners.Count + 8);
        SpawnRing(center, cam, ringCount);

        int spawned = 0;
        for (int attempt = 0; attempt < finaleExtra * 8 && spawned < finaleExtra; attempt++)
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            Vector3 p = new Vector3(Random.Range(finaleLaneX.x, finaleLaneX.y), center.y,
                center.z + side * Random.Range(finaleSpawnBand.x, finaleSpawnBand.y));
            if (p.z < finaleLaneZ.x || p.z > finaleLaneZ.y || (cam != null && InView(cam, p)))
                continue;
            if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 1.5f, NavMesh.AllAreas) || TooCloseToSpawn(hit.position))
                continue;
            // Caddeye bağlı olmayan adacıklar (binanın kapı basamakları) elenir: orada doğan NPC yerinden kıpırdamıyordu.
            if (!NavMesh.CalculatePath(hit.position, center, NavMesh.AllAreas, finalePath)
                || finalePath.status != NavMeshPathStatus.PathComplete)
                continue;
            Vector3 toCenter = center - hit.position;
            toCenter.y = 0f;
            NpcAgent npc = Spawn(hit.position, Quaternion.LookRotation(toCenter), null);
            finaleSpawns.Add(hit.position);
            npc.GoalLimit = int.MaxValue;
            npc.SetAvoidanceRadius(0.3f);
            npc.SetSpeed(Random.Range(1.2f, 1.6f));
            joiners.Add(npc);
            spawned++;
        }
    }

    // Oyuncuya benzeyen kadınlar görüş dışında, oyuncunun cadde boyunca önünde/arkasında doğar, her biri halkada bir
    // yere yürür ve ona dönük durur. Halkanın içi boş kalır: final kamerası karakteri tepeden, halkanın üstünden görür.
    private void SpawnRing(Vector3 center, Camera cam, int count)
    {
        if (count <= 0 || ringProfile < 0 || ringProfile >= profiles.Length || profiles[ringProfile].prefab == null)
            return;
        SpawnRing(center, cam, count, profiles[ringProfile], ringSpawnBand, false);
    }

    private void SpawnRing(Vector3 center, Camera cam, int count, NpcProfile profile, Vector2 band, bool playerCopy)
    {
        if (count <= 0 || profile == null || profile.prefab == null)
            return;
        Vector2 ringSpawnBand = band;
        int placed = 0;
        for (int attempt = 0; attempt < count * 20 && placed < count; attempt++)
        {
            // Yer bulunamazsa bant giderek genişler.
            float widen = attempt / (float)(count * 10);
            float side = Random.value < 0.5f ? -1f : 1f;
            Vector3 p = new Vector3(Random.Range(finaleLaneX.x, finaleLaneX.y), center.y,
                center.z + side * Random.Range(ringSpawnBand.x, ringSpawnBand.y * (1f + 2f * widen)));
            // Görüş içinde hiç doğmaz (eskiden bant genişleyince gözün önünde beliriyordu); bant giderek uzar.
            if (p.z < finaleLaneZ.x || p.z > finaleLaneZ.y || (cam != null && InView(cam, p)))
                continue;
            if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 1.5f, NavMesh.AllAreas) || TooCloseToSpawn(hit.position)
                || !NavMesh.CalculatePath(hit.position, center, NavMesh.AllAreas, finalePath)
                || finalePath.status != NavMeshPathStatus.PathComplete)
                continue;

            Vector3 toCenter = center - hit.position;
            toCenter.y = 0f;
            NpcAgent npc = Spawn(hit.position, Quaternion.LookRotation(toCenter), null, profile);
            finaleSpawns.Add(hit.position);
            npc.GoalLimit = int.MaxValue;
            npc.SetAvoidanceRadius(0.3f);
            npc.SetSpeed(ringWalkSpeed * Random.Range(0.9f, 1.15f));
            if (playerCopy)
            {
                copies.Add(npc);
            }
            else
            {
                SetLook(npc.transform, profile, ringLook);
                // Oyuncu solmaz (Reklam katmanı); benzerleri solarsa kalabalıkta renkli tek kişi olarak seçiliyordu.
                SceneFade.MarkExempt(npc.gameObject);
                foreach (SkinnedMeshRenderer r in npc.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (r.name.Contains("Shoe"))
                        SceneFade.MarkExempt(r.gameObject, true);
                }
            }
            lookalikes.Add(npc);
            joiners.Add(npc);
            placed++;
        }
    }

    // Oyuncunun çevresinde düzensiz, sık bir kalabalık için duruş yerleri: iç kenar açıya göre dalgalı, kişiler arası
    // mesafe yer başına rastgele, dış kenar yırtık (mesafe + rastgele ile sıralanıp sırayla doldurulur). Sokak eşyasının
    // (lamba, bank, saksı) içine düşen yerler elenir.
    private void BuildCrowdSlots(Vector3 center, int count)
    {
        crowdSlots.Clear();
        float seedA = Random.Range(0f, 100f), seedB = Random.Range(0f, 100f);
        const float maxRadius = 15f;
        var candidates = new List<CrowdSlot>();
        for (int i = 0; i < count * 30; i++)
        {
            Vector2 r = Random.insideUnitCircle * maxRadius;
            float d = r.magnitude;
            float angle = Mathf.Atan2(r.y, r.x);
            float inner = ringRadius + innerJitter * Mathf.PerlinNoise(seedA + Mathf.Cos(angle) * 1.4f, seedB + Mathf.Sin(angle) * 1.4f);
            if (d < inner)
                continue;
            Vector3 p = center + new Vector3(r.x, 0f, r.y);
            if (p.x < crowdLaneX.x || p.x > crowdLaneX.y || p.z < finaleLaneZ.x || p.z > finaleLaneZ.y)
                continue;
            // İç sıralar sık: mesafeye ağırlık verilir, dış kenar rastgeleyle yırtılır.
            candidates.Add(new CrowdSlot { point = p, key = d + Random.Range(0f, edgeRaggedness) });
        }
        candidates.Sort((a, b) => a.key.CompareTo(b.key));

        var spacing = new List<float>();
        const int blockMask = ~(1 << 2);
        foreach (CrowdSlot c in candidates)
        {
            if (crowdSlots.Count >= count)
                break;
            float own = Random.Range(crowdSpacing.x, crowdSpacing.y);
            bool clear = true;
            for (int i = 0; i < crowdSlots.Count && clear; i++)
            {
                float need = (own + spacing[i]) * 0.5f;
                Vector3 o = crowdSlots[i].point - c.point;
                o.y = 0f;
                clear = o.sqrMagnitude >= need * need;
            }
            if (!clear || !NavMesh.SamplePosition(c.point, out NavMeshHit hit, 0.35f, NavMesh.AllAreas))
                continue;
            Vector3 p = hit.position;
            if (Physics.CheckCapsule(p + Vector3.up * 0.45f, p + Vector3.up * 1.5f, 0.22f, blockMask, QueryTriggerInteraction.Ignore))
                continue;
            crowdSlots.Add(new CrowdSlot { point = p, key = c.key });
            spacing.Add(own);
        }
    }

    // Benzerler en içteki boş yerlerden kendilerine en yakınını, diğerleri sırası (iç kenara yakınlık) ve yürüme
    // mesafesine göre en iyisini alır: önce gelen içeri yerleşir, geç gelen dışta kalır, kimse kalabalığı yarıp geçmez.
    private int ClaimSlot(NpcAgent npc)
    {
        Vector3 from = npc.transform.position;
        bool lookalike = lookalikes.Contains(npc);
        int innerLimit = Mathf.Min(crowdSlots.Count, Mathf.CeilToInt(ringCount * 1.4f));
        int best = -1;
        float bestCost = float.MaxValue;
        for (int i = 0; i < crowdSlots.Count; i++)
        {
            if (!crowdSlots[i].Free || !MayTake(npc, i))
                continue;
            if (lookalike && i >= innerLimit && best >= 0)
                break;
            // Kimseye yer ayrılmaz: içeriden dışarı dolar. Ayrılan iç yerlere geç gelen benzerler dış sıraları
            // yaramadığı için oyuncunun dibi boş kalıyordu.
            float walk = Vector3.Distance(from, crowdSlots[i].point);
            float cost = lookalike ? crowdSlots[i].key * 2f + walk * 0.3f : crowdSlots[i].key * 2f + walk * 0.35f;
            if (cost < bestCost)
            {
                bestCost = cost;
                best = i;
            }
        }
        if (best >= 0)
        {
            CrowdSlot s = crowdSlots[best];
            s.owner = npc;
            crowdSlots[best] = s;
        }
        return best;
    }

    // İç yerler (oyuncunun dibi) oyuncu kopyalarına ayrılır: oyuncunun yanında Kız NPC modeli durunca karakter
    // farklı yüzü/bedeniyle seçiliyordu.
    private bool MayTake(NpcAgent npc, int slot) => slot >= reservedInner || copies.Count == 0 || copies.Contains(npc);

    private Goal SlotGoal(int slot)
    {
        Vector3 p = crowdSlots[slot].point;
        if (conforming)
            return new Goal { kind = GoalKind.Ring, point = p, rotation = ConformRotation() };
        Vector3 look = finaleCenter - p;
        look.y = 0f;
        bool inner = slot < ringCount;
        float yaw = Random.Range(-facingJitter, facingJitter);
        bool talk = false;
        if (!inner && Random.value < lookAwayChance + talkChance)
        {
            // Dış sıralarda bazıları yanındakine döner (bir kısmı konuşur): kalabalık asker gibi dizilmesin.
            talk = Random.value < talkChance / (lookAwayChance + talkChance);
            yaw = Random.Range(55f, 110f) * (Random.value < 0.5f ? -1f : 1f);
        }
        return new Goal
        {
            kind = GoalKind.Ring,
            point = p,
            rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.LookRotation(look.sqrMagnitude > 0.001f ? look : Vector3.forward),
            talk = talk
        };
    }

    private int OwnedSlot(NpcAgent npc)
    {
        for (int i = 0; i < crowdSlots.Count; i++)
        {
            if (crowdSlots[i].owner == npc)
                return i;
        }
        return -1;
    }

    private void SetOwner(int slot, NpcAgent owner)
    {
        CrowdSlot s = crowdSlots[slot];
        s.owner = owner;
        crowdSlots[slot] = s;
    }

    /// <summary>
    /// Yürürken: önündeki daha içteki bir yer boşsa ona geçer. Uzaktan iç yer kapan NPC'ler final donana kadar
    /// yetişemiyordu, oyuncunun dibi boş kalıyordu; artık o yerleri oraya ilk varan doldurur.
    /// </summary>
    public bool TryUpgradeSlot(NpcAgent npc, out Goal goal, bool settled = false)
    {
        goal = default;
        if (frozen || crowdSlots.Count == 0)
            return false;
        int current = OwnedSlot(npc);
        Vector3 from = npc.transform.position;
        // Yerini bırakmış (varamayıp olduğu yerde duran) NPC'nin "sırası" merkeze uzaklığıdır.
        Vector3 toCenter = from - finaleCenter;
        toCenter.y = 0f;
        float currentKey = current >= 0 ? crowdSlots[current].key : toCenter.magnitude + 0.3f;
        float currentDistance = current >= 0 ? Vector3.Distance(from, crowdSlots[current].point) : 0f;
        // Yerleşmiş olan sadece dibindeki (adım mesafesindeki) boş iç yere geçer; yürüyen yolunun üstündekine.
        // Yerleşmişin adımı kısa: uzun adımda iki kişinin arasından sıyrılıp onları itiyordu.
        float reach = settled ? 1.25f : currentDistance + 0.6f;
        int best = -1;
        float bestKey = currentKey - 0.3f;
        int limit = current >= 0 ? current : crowdSlots.Count;
        for (int i = 0; i < limit; i++)
        {
            if (!crowdSlots[i].Free || crowdSlots[i].key >= bestKey || !MayTake(npc, i))
                continue;
            if (Vector3.Distance(from, crowdSlots[i].point) > reach)
                continue;
            best = i;
            bestKey = crowdSlots[i].key;
        }
        if (best < 0)
            return false;
        if (current >= 0)
            SetOwner(current, null);
        ClearBlocks(npc);
        SetOwner(best, npc);
        goal = SlotGoal(best);
        return true;
    }

    /// <summary>
    /// Yerine varamayan NPC olduğu yerde durur: yeri bırakılır, ama durduğu noktanın dibindeki boş yerler (bıraktığı
    /// dahil) kapanır; yoksa o yeri sonradan alan NPC duranın içine yürüyordu. Dibinde boş bir yer varsa onu sahiplenir
    /// (kalabalığın parçası sayılır, sonra içeri adım atabilir).
    /// </summary>
    public void SettleWhereStanding(NpcAgent npc, Vector3 position)
    {
        ReleaseSlot(npc);
        int nearest = -1;
        float nearestD = float.MaxValue;
        for (int i = 0; i < crowdSlots.Count; i++)
        {
            CrowdSlot s = crowdSlots[i];
            if (s.owner != null || s.blocker != null)
                continue;
            Vector3 d = s.point - position;
            d.y = 0f;
            if (d.sqrMagnitude > BlockRadius * BlockRadius)
                continue;
            s.blocker = npc;
            crowdSlots[i] = s;
            if (d.sqrMagnitude < nearestD && !s.nearPlayer)
            {
                nearestD = d.sqrMagnitude;
                nearest = i;
            }
        }
        if (nearest >= 0)
        {
            CrowdSlot s = crowdSlots[nearest];
            s.blocker = null;
            s.owner = npc;
            crowdSlots[nearest] = s;
        }
    }

    /// <summary>Yerini bırakır (kapattığı yerler de açılır).</summary>
    public void ReleaseSlot(NpcAgent npc)
    {
        int slot = OwnedSlot(npc);
        if (slot >= 0)
            SetOwner(slot, null);
        ClearBlocks(npc);
    }

    private void ClearBlocks(NpcAgent npc)
    {
        for (int i = 0; i < crowdSlots.Count; i++)
        {
            if (crowdSlots[i].blocker != npc)
                continue;
            CrowdSlot s = crowdSlots[i];
            s.blocker = null;
            crowdSlots[i] = s;
        }
    }

    /// <summary>Oyuncu caddenin ortasına vardı: yolunu bekleyen NPC'ler yerlerine geçer, kalabalık arkasını da kapatır.
    /// Oyuncu takılıp tam merkeze varamadıysa dibindeki yerler kapalı kalır (NPC'ler karakterin içine girmesin).</summary>
    public void PlayerArrived(Vector3 playerPosition)
    {
        if (!finale || playerArrived)
            return;
        playerArrived = true;
        // Karakterin kapsülü (0.3) + NPC gövdesi (0.25) + pay.
        const float keep = 0.6f;
        for (int i = 0; i < crowdSlots.Count; i++)
        {
            CrowdSlot s = crowdSlots[i];
            Vector3 d = s.point - playerPosition;
            d.y = 0f;
            s.nearPlayer = d.sqrMagnitude < keep * keep;
            crowdSlots[i] = s;
        }
    }

    /// <summary>
    /// Oyuncu henüz ortaya yürürken bu yer onun önündeki yolun üstünde mi: öyleyse NPC yerine geçmez, yakınında bekler
    /// (yol verir). Eskiden erken gelen NPC'ler yolu kesiyor, karakter kalabalığa takılıp ortaya varamıyordu.
    /// </summary>
    public bool WaitForPlayer(Vector3 slotPoint) => finale && !frozen && OnPlayerPath(slotPoint);

    private bool OnPlayerPath(Vector3 p)
    {
        if (playerArrived)
            return false;
        // Yolun oyuncunun geçtiği kısmı serbest: sadece önünde kalan kısmı.
        Vector3 from = player != null ? player.position : finalePathStart;
        Vector2 a = new Vector2(from.x, from.z);
        Vector2 b = new Vector2(finaleCenter.x, finaleCenter.z);
        Vector2 q = new Vector2(p.x, p.z);
        Vector2 ab = b - a;
        float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude) : 0f;
        return (q - (a + ab * t)).sqrMagnitude < PathClearance * PathClearance;
    }

    private bool TooCloseToSpawn(Vector3 p)
    {
        foreach (Vector3 s in finaleSpawns)
        {
            Vector3 d = s - p;
            d.y = 0f;
            if (d.sqrMagnitude < SpawnSpacing * SpawnSpacing)
                return true;
        }
        return false;
    }

    /// <summary>Finalin son anı: kalabalık (ve yürüyenler) olduğu pozda donar.</summary>
    public void FreezeFinale()
    {
        frozen = true;
        foreach (NpcAgent agent in agents)
        {
            if (agent != null)
                agent.Freeze();
        }
    }

    // Parçayı açar, aynı yuvadaki diğer seçenekleri kapatır.
    private static void SetLook(Transform root, NpcProfile profile, string look)
    {
        if (string.IsNullOrEmpty(look) || profile.outfit == null)
            return;
        foreach (NpcOutfitSlot slot in profile.outfit)
        {
            if (Array.IndexOf(slot.options, look) < 0)
                continue;
            foreach (string option in slot.options)
            {
                foreach (string part in option.Split('+'))
                {
                    Transform t = root.Find(part);
                    if (t != null)
                        t.gameObject.SetActive(option == look);
                }
            }
        }
    }

    // Oyuncunun görünen modelinin (Animator'lı gövde kökü) o anki hâlinin kopyası: giydiği ürünler, saçı, yüz
    // blendshape'leri, ayakkabı ton bloğu, solmayan katman. Script'leri sökülür, NPC kadın controller'ını alır (oyuncuyla
    // aynı idle ve yürüme klipleri). Kopya pasif bir kabın altında yapılır ki oyuncu script'lerinin Awake'i çalışmasın.
    private bool BuildCopyProfile()
    {
        if (copyProfile != null)
            return true;
        if (player == null || ringProfile < 0 || ringProfile >= profiles.Length || profiles[ringProfile].prefab == null)
            return false;
        Animator source = null;
        foreach (Animator a in player.GetComponentsInChildren<Animator>())
        {
            if (a.isHuman)
            {
                source = a;
                break;
            }
        }
        Animator womenAnimator = profiles[ringProfile].prefab.GetComponent<Animator>();
        if (source == null || womenAnimator == null)
            return false;

        var holder = new GameObject("Oyuncu_Kopyasi_Sablon");
        holder.transform.SetParent(transform, false);
        holder.SetActive(false);
        copyTemplate = Instantiate(source.gameObject, holder.transform);
        copyTemplate.name = "Oyuncu_Kopyasi";
        copyTemplate.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

        // Renderer'lar aynı sırada: ayakkabının soğuk ton bloğu gibi property block'lar taşınır.
        SkinnedMeshRenderer[] from = source.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        SkinnedMeshRenderer[] to = copyTemplate.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var block = new MaterialPropertyBlock();
        for (int i = 0; i < from.Length && i < to.Length; i++)
        {
            if (!from[i].HasPropertyBlock())
                continue;
            from[i].GetPropertyBlock(block);
            to[i].SetPropertyBlock(block);
        }

        foreach (MonoBehaviour b in copyTemplate.GetComponentsInChildren<MonoBehaviour>(true))
            DestroyImmediate(b);
        int exempt = LayerMask.NameToLayer("Reklam");
        foreach (SkinnedMeshRenderer r in copyTemplate.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            // Birinci şahıs için üretilen kafasız gövde kopyası (asıl gövde kafalı).
            if (r.name.EndsWith("_Kafasiz"))
            {
                DestroyImmediate(r.gameObject);
                continue;
            }
            r.forceRenderingOff = false;
            r.enabled = true;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            r.updateWhenOffscreen = false;
            if (exempt >= 0)
                r.gameObject.layer = exempt;
        }

        Animator copyAnimator = copyTemplate.GetComponent<Animator>();
        copyAnimator.runtimeAnimatorController = womenAnimator.runtimeAnimatorController;
        copyAnimator.applyRootMotion = false;

        NpcProfile women = profiles[ringProfile];
        copyProfile = new NpcProfile
        {
            prefab = copyTemplate,
            weight = 0f,
            idleState = women.idleState,
            walkState = women.walkState,
            sitState = women.sitState,
            talkState = women.talkState,
            walkClipSpeed = women.walkClipSpeed,
            outfit = null
        };
        return true;
    }

    /// <summary>
    /// Kalabalık (ve yürüyüp gelenler) oyuncuyla aynı yöne, mağazaya döner; dönüş kalabalığa dalga gibi yayılır.
    /// Herkes oyuncuya bakarken bakışlar ortadakini işaret ediyordu; aynı yöne bakan aynı insanların arasında oyuncu
    /// kendini ayırt edemez.
    /// </summary>
    public void Conform(Vector3 direction)
    {
        direction.y = 0f;
        if (!finale || direction.sqrMagnitude < 0.001f)
            return;
        conforming = true;
        conformRotation = Quaternion.LookRotation(direction.normalized);
        foreach (NpcAgent npc in joiners)
        {
            if (npc == null)
                continue;
            // Dönüş merkezden dışa doğru yayılır (yakındakiler önce), üstüne biraz rastgele.
            Vector3 d = npc.transform.position - finaleCenter;
            d.y = 0f;
            float delay = Mathf.Lerp(conformDelay.x, conformDelay.y, Mathf.Clamp01(d.magnitude / 8f) * 0.7f + Random.value * 0.3f);
            npc.Conform(ConformRotation(), delay);
        }
    }

    private Quaternion ConformRotation() => Quaternion.Euler(0f, Random.Range(-conformJitter, conformJitter), 0f) * conformRotation;

    /// <summary>
    /// Final kamerasının oyuncudan kayacağı kişi: oyuncunun yanındaki (1.4-3 m) yerleşmiş bir kopya, tercihen yanda
    /// (önünde/arkasında değil). Kamera yükselirken odağı fark ettirmeden ona geçer; son karede ortadaki oyuncu değildir.
    /// </summary>
    public Transform PickDecoy(Vector3 playerPosition, Vector3 forward)
    {
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
        Transform best = null;
        float bestScore = float.MaxValue;
        foreach (NpcAgent npc in copies.Count > 0 ? copies : lookalikes)
        {
            if (npc == null || !npc.AtSpot)
                continue;
            Vector3 d = npc.transform.position - playerPosition;
            d.y = 0f;
            float m = d.magnitude;
            if (m < 1.4f || m > 3f)
                continue;
            float side = Mathf.Abs(Vector3.Dot(d / m, forward));
            float score = side + Mathf.Abs(m - 2f) * 0.3f + Random.value * 0.4f;
            if (score < bestScore)
            {
                bestScore = score;
                best = npc.transform;
            }
        }
        return best;
    }

    /// <summary>Halkadaki NPC yerine vardı.</summary>
    public void NotifyRingArrived(NpcAgent npc)
    {
        if (joiners.Contains(npc))
            arrived.Add(npc);
    }

    // Sadece benzerler: oyuncunun çevresindeki sabit NPC'ler (kafe, bank, telefon, çalgıcı...) ve sokaktan kalan
    // kalabalık NPC'leri kamera görmediği an kaldırılır; görünürken kaybolmazlar (birden yok olmasınlar).
    private void CollectBystanders(Vector3 center)
    {
        bystanders.Clear();
        foreach (Animator a in FindObjectsByType<Animator>(FindObjectsSortMode.None))
        {
            if (a.transform.IsChildOf(transform) || (player != null && a.transform.IsChildOf(player)))
                continue;
            if (a.GetComponentInParent<NpcAgent>() != null)
                continue;
            Vector3 d = a.transform.position - center;
            d.y = 0f;
            // Final kalabalığında erkek görünmez (08.11): erkekler uzakta da olsa toplanır.
            if ((d.sqrMagnitude < bystanderRadius * bystanderRadius || IsMale(a.gameObject))
                && a.GetComponentInChildren<SkinnedMeshRenderer>() != null)
                bystanders.Add(a.gameObject);
        }
    }

    // Erkek NPC (prefab/obje adından): erkek, çalgıcı, kasiyer erkek.
    private static bool IsMale(GameObject go)
    {
        string n = go.name;
        return n.Contains("Man") || n.Contains("Erkek") || n.Contains("Calgici") || n.Contains("Muzisyen");
    }

    private IEnumerator ClearOutsiders()
    {
        var wait = new WaitForSeconds(0.25f);
        while (!frozen && (outsiders.Count > 0 || bystanders.Count > 0))
        {
            Camera cam = playerCamera != null ? playerCamera : Camera.main;
            for (int i = outsiders.Count - 1; i >= 0; i--)
            {
                NpcAgent npc = outsiders[i];
                if (npc == null) { outsiders.RemoveAt(i); continue; }
                bool farMale = IsMale(npc.gameObject) && (npc.transform.position - finaleCenter).sqrMagnitude > 20f * 20f;
                if (!farMale && cam != null && InView(cam, npc.transform.position))
                    continue;
                agents.Remove(npc);
                outsiders.RemoveAt(i);
                Destroy(npc.gameObject);
            }
            for (int i = bystanders.Count - 1; i >= 0; i--)
            {
                GameObject go = bystanders[i];
                if (go == null) { bystanders.RemoveAt(i); continue; }
                bool farMale = IsMale(go) && (go.transform.position - finaleCenter).sqrMagnitude > 20f * 20f;
                if (!farMale && cam != null && InView(cam, go.transform.position))
                    continue;
                go.SetActive(false);
                bystanders.RemoveAt(i);
            }
            yield return wait;
        }
    }

    // Kalabalığa katılmayan eski NPC: oyuncudan uzaklaşır (görüş dışına çıkınca kaldırılır).
    private Vector3 AwayPoint(Vector3 from)
    {
        Vector3 dir = from - finaleCenter;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f)
            dir = Vector3.forward;
        dir.x = 0f;
        dir = dir.sqrMagnitude < 0.01f ? Vector3.forward : dir.normalized;
        Vector3 p = new Vector3(from.x, from.y, Mathf.Clamp(finaleCenter.z + dir.z * 35f, finaleLaneZ.x, finaleLaneZ.y));
        if (NavMesh.SamplePosition(p, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            return hit.position;
        return p;
    }

    private static bool InView(Camera cam, Vector3 position)
    {
        Vector3 v = cam.WorldToViewportPoint(position + Vector3.up);
        return v.z > 0f && v.x > -0.15f && v.x < 1.15f && v.y > -0.15f && v.y < 1.15f;
    }

    // Oyuncunun çevresinde, dibinde boşluk bırakan bir halka içinde rastgele bir nokta (caddenin NPC şeridinde).
    private Vector3 FinalePoint()
    {
        // Halka varsa geri kalan kalabalık onun dışında kalır.
        float clearance = finaleClearance;
        if (crowdSlots.Count > 0)
            clearance = Mathf.Max(clearance, crowdSlots[crowdSlots.Count - 1].key + 0.5f);
        for (int i = 0; i < 8; i++)
        {
            Vector2 r = Random.insideUnitCircle.normalized * Random.Range(clearance, Mathf.Max(finaleRadius, clearance + 2f));
            Vector3 p = finaleCenter + new Vector3(r.x, 0f, r.y);
            p.x = Mathf.Clamp(p.x, finaleLaneX.x, finaleLaneX.y);
            p.z = Mathf.Clamp(p.z, finaleLaneZ.x, finaleLaneZ.y);
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 1f, NavMesh.AllAreas))
                return hit.position;
        }
        return finaleCenter + Vector3.forward * finaleRadius;
    }

    public Goal NextGoal(NpcAgent npc)
    {
        Vector3 from = npc.transform.position;

        if (finale)
        {
            int slot = joiners.Contains(npc) ? ClaimSlot(npc) : -1;
            if (slot >= 0)
                return SlotGoal(slot);
            if (outsiders.Contains(npc))
                return new Goal { kind = GoalKind.Wander, point = AwayPoint(from) };
            return new Goal { kind = GoalKind.Wander, point = FinalePoint() };
        }

        if (streetFlow)
            return NextFlowGoal(npc, from);

        if (exits != null && exits.Length > 0 && npc.GoalsDone >= npc.GoalLimit)
        {
            Transform exit = PickExit(from);
            if (exit != null)
                return new Goal { kind = GoalKind.Leave, point = exit.position };
        }

        if (Random.value < spotChance)
        {
            NpcSpot s = PickSpot(from);
            if (s != null)
                return new Goal { kind = GoalKind.Spot, spot = s };
        }

        return new Goal { kind = GoalKind.Wander, point = PickWanderPoint(from) };
    }

    /// <summary>Akışta gezinme hedefleri ara durak: NPC durmadan bir sonrakine geçer.</summary>
    public bool FlowMode => streetFlow && !finale;

    // Cadde akışı: şeridinde, yürüdüğü yönde bir iki nokta ileri; ara sıra önündeki, kendi tarafındaki bir noktaya
    // uğrar; yeterince yürüdüyse önündeki bir çıkıştan, şeridin sonundaysa en yakın çıkıştan ayrılır.
    private Goal NextFlowGoal(NpcAgent npc, Vector3 from)
    {
        int heading = npc.Heading;
        bool hasExits = exits != null && exits.Length > 0;

        if (hasExits && npc.GoalsDone >= npc.GoalLimit)
        {
            Transform exit = PickExitAhead(from, heading, 25f);
            if (exit != null)
                return new Goal { kind = GoalKind.Leave, point = exit.position };
        }

        if (npc.GoalsDone > 0 && Random.value < flowSpotChance)
        {
            NpcSpot s = PickSpotAhead(from, heading, Mathf.Sign(routeX[npc.Route]));
            if (s != null)
                return new Goal { kind = GoalKind.Spot, spot = s };
        }

        if (TryRoutePoint(npc.Route, from, heading, out Vector3 point))
            return new Goal { kind = GoalKind.Wander, point = point };

        // Şeridin sonu.
        if (hasExits)
        {
            Transform exit = PickExitAhead(from, heading, 40f) ?? PickExit(from);
            if (exit != null)
                return new Goal { kind = GoalKind.Leave, point = exit.position };
        }
        npc.Heading = -heading;
        npc.Route = NearestRoute(from, -heading);
        if (TryRoutePoint(npc.Route, from, -heading, out point))
            return new Goal { kind = GoalKind.Wander, point = point };
        return new Goal { kind = GoalKind.Wander, point = PickWanderPoint(from) };
    }

    // Şeritte yürüme yönünde en az 3 m ilerideki ilk (bazen ikinci) nokta; şeridin çizgisinden az sapar.
    private bool TryRoutePoint(int routeIndex, Vector3 from, int heading, out Vector3 point)
    {
        List<Vector3> route = routes[routeIndex];
        int next = -1;
        for (int i = 0; i < route.Count; i++)
        {
            int idx = heading > 0 ? i : route.Count - 1 - i;
            if ((route[idx].z - from.z) * heading > 3f)
            {
                next = idx;
                break;
            }
        }
        if (next < 0)
        {
            point = from;
            return false;
        }

        if (Random.value < 0.4f)
            next = Mathf.Clamp(next + heading, 0, route.Count - 1);
        Vector3 p = route[next] + new Vector3(Random.Range(-0.6f, 0.6f), 0f, Random.Range(-1.5f, 1.5f));
        point = NavMesh.SamplePosition(p, out NavMeshHit hit, 1.5f, NavMesh.AllAreas) ? hit.position : route[next];
        return true;
    }

    private NpcSpot PickSpotAhead(Vector3 from, int heading, float side)
    {
        NpcSpot best = null;
        float bestScore = 0f;
        foreach (NpcSpot s in spots)
        {
            Vector3 p = s.transform.position;
            float ahead = (p.z - from.z) * heading;
            if (s.FreeSlots == 0 || ahead < 2f || ahead > flowSpotAhead || p.x * side <= 0f)
                continue;
            float score = Random.value * (s.Activity == NpcActivity.Talk && s.OccupantCount > 0 ? 3f : 1f);
            if (score > bestScore)
            {
                bestScore = score;
                best = s;
            }
        }
        return best;
    }

    // Yürüme yönünde en fazla range kadar ilerideki (ya da hemen yanındaki) çıkışlardan en yakını; görülen çıkış son seçenek.
    private Transform PickExitAhead(Vector3 from, int heading, float range)
    {
        Transform pick = null;
        float bestScore = float.MaxValue;
        foreach (Transform e in exits)
        {
            if (e == null)
                continue;
            float ahead = (e.position.z - from.z) * heading;
            if (ahead < -4f || ahead > range)
                continue;
            float score = Vector3.Distance(e.position, from) * (IsSeen(e.position) ? 10f : 1f);
            if (score < bestScore)
            {
                bestScore = score;
                pick = e;
            }
        }
        return pick;
    }

    private NpcSpot PickSpot(Vector3 from)
    {
        NpcSpot best = null;
        float bestScore = 0f;
        foreach (NpcSpot s in spots)
        {
            if (s.FreeSlots == 0 || (s.transform.position - from).sqrMagnitude > spotRange * spotRange)
                continue;
            // Biri bekleyen sohbet noktası daha çekici: insanlar bir araya gelsin.
            float score = Random.value * (s.Activity == NpcActivity.Talk && s.OccupantCount > 0 ? 3f : 1f);
            if (score > bestScore)
            {
                bestScore = score;
                best = s;
            }
        }
        return best;
    }

    private Vector3 PickWanderPoint(Vector3 from)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector3 p = wanderPoints[Random.Range(0, wanderPoints.Count)];
            float d = (p - from).sqrMagnitude;
            if (d <= wanderRange * wanderRange && d > 4f)
                return Jitter(p);
        }

        // Menzilde nokta çıkmadıysa en yakınlardan biri.
        Vector3 nearest = wanderPoints[0];
        float best = float.MaxValue;
        foreach (Vector3 p in wanderPoints)
        {
            float d = (p - from).sqrMagnitude;
            if (d > 4f && d < best)
            {
                best = d;
                nearest = p;
            }
        }
        return Jitter(nearest);
    }

    private Vector3 Jitter(Vector3 p)
    {
        Vector2 r = Random.insideUnitCircle * wanderJitter;
        Vector3 q = p + new Vector3(r.x, 0f, r.y);
        return NavMesh.SamplePosition(q, out NavMeshHit hit, wanderJitter + 1f, NavMesh.AllAreas) ? hit.position : p;
    }

    private Transform PickExit(Vector3 from)
    {
        // Yakın çıkışlardan rastgele biri: NPC caddenin öbür ucuna yürümesin.
        Transform pick = null;
        float bestScore = float.MaxValue;
        foreach (Transform e in exits)
        {
            if (e == null)
                continue;
            float score = Vector3.Distance(e.position, from) * Random.Range(0.6f, 1.4f);
            // Oyuncunun gördüğü çıkış ancak başka seçenek yoksa (orada kaybolamaz).
            if (IsSeen(e.position))
                score *= 10f;
            if (score < bestScore)
            {
                bestScore = score;
                pick = e;
            }
        }
        return pick;
    }

    public void Despawn(NpcAgent npc)
    {
        agents.Remove(npc);
        Destroy(npc.gameObject);
        // Finalde yerine yenisi gelmez (gelen sıradan NPC benzerlerin kalabalığına karışırdı).
        if (!finale)
            StartCoroutine(RespawnLater());
    }

    private IEnumerator RespawnLater()
    {
        yield return new WaitForSeconds(Random.Range(respawnDelay.x, respawnDelay.y));

        while (true)
        {
            Transform exit = exits[Random.Range(0, exits.Length)];
            // Çıkışta başka NPC duruyorsa orada doğma (11.11, "NPC'ler iç içe girip anlık yok olup geliyor"): yeni gelen
            // bekleyenin tam üstünde doğuyor, ajanlar ayrılırken ~1 m ışınlanıyordu.
            if (exit != null && !IsSeen(exit.position)
                && NavMesh.SamplePosition(exit.position, out NavMeshHit hit, 2f, NavMesh.AllAreas) && !Occupied(hit.position, 1.3f))
            {
                Spawn(hit.position, exit.rotation, null);
                yield break;
            }
            yield return new WaitForSeconds(1f);
        }
    }

    /// <summary>Bu noktanın yakınında (yatayda) başka bir NPC var mı.</summary>
    private bool Occupied(Vector3 p, float radius)
    {
        foreach (NpcAgent a in agents)
        {
            if (a == null)
                continue;
            Vector3 d = a.transform.position - p;
            d.y = 0f;
            if (d.sqrMagnitude < radius * radius)
                return true;
        }
        return false;
    }

    public bool IsSeen(Vector3 position)
    {
        if (player == null || playerCamera == null)
            return false;
        if ((position - player.position).sqrMagnitude > hiddenDistance * hiddenDistance)
            return false;
        if ((position - player.position).sqrMagnitude < 10f * 10f)
            return true;
        Vector3 v = playerCamera.WorldToViewportPoint(position + Vector3.up);
        bool inView = v.z > 0f && v.x > -0.1f && v.x < 1.1f && v.y > -0.1f && v.y < 1.1f;
        if (!inView)
            return false;
        // Binanın/köşenin arkasında kalan nokta görülmüyor sayılır (10.11): eskiden sadece görüş alanına bakılıyordu,
        // ara sokağın köşesini dönmüş NPC de "görülüyor" sayılıp çıkışta bekliyor, çıkışlarda yığın oluşuyordu.
        Vector3 eye = playerCamera.transform.position;
        return !Physics.Linecast(eye, position + Vector3.up * 1.6f, OcclusionMask, QueryTriggerInteraction.Ignore)
               || !Physics.Linecast(eye, position + Vector3.up * 0.9f, OcclusionMask, QueryTriggerInteraction.Ignore);
    }

    // Oyuncu ve NPC'ler (Ignore Raycast) görüşü kesmez.
    private const int OcclusionMask = ~(1 << 2);

    /// <summary>Akışta yön değiştirir (çıkışı görülen ve başka çıkış bulamayan NPC geri döner, çıkışta beklemez).</summary>
    public void TurnAround(NpcAgent npc)
    {
        if (!streetFlow || routes == null || routes.Count == 0)
            return;
        npc.Heading = -npc.Heading;
        npc.Route = NearestRoute(npc.transform.position, npc.Heading);
    }

    /// <summary>Verilen noktadan uzak olmayan, oyuncunun görmediği başka bir çıkış (yoksa null).</summary>
    public Transform PickUnseenExit(Vector3 from, Vector3 except)
    {
        if (exits == null)
            return null;
        Transform pick = null;
        float bestScore = float.MaxValue;
        foreach (Transform e in exits)
        {
            if (e == null || (e.position - except).sqrMagnitude < 4f || IsSeen(e.position))
                continue;
            float score = Vector3.Distance(e.position, from) * Random.Range(0.8f, 1.2f);
            if (score < bestScore && score < 80f)
            {
                bestScore = score;
                pick = e;
            }
        }
        return pick;
    }
}
