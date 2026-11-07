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
            sitState = "Man_Sit_Talk",
            walkClipSpeed = 1.55f,
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
            sitState = "Women_Sit_Talk",
            walkClipSpeed = 1.0f,
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
    [SerializeField] private float ringRadius = 1.15f;
    [SerializeField] private float innerJitter = 1.1f;
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

    private bool finale;
    private bool frozen;
    private Vector3 finaleCenter;
    private NavMeshPath finalePath;

    private struct CrowdSlot
    {
        public Vector3 point;
        public float key;
        public NpcAgent owner;
    }

    private readonly List<CrowdSlot> crowdSlots = new List<CrowdSlot>();
    private readonly HashSet<NpcAgent> lookalikes = new HashSet<NpcAgent>();
    private readonly HashSet<NpcAgent> joiners = new HashSet<NpcAgent>();
    private int ringArrived;

    /// <summary>Kalabalığın çoğu yerini aldı (final kamerası bunu bekler).</summary>
    public bool RingFormed => joiners.Count == 0 || ringArrived >= Mathf.CeilToInt(Mathf.Min(joiners.Count, crowdSlots.Count) * 0.8f);

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

        if (wanderPoints.Count == 0 || totalWeight <= 0f)
        {
            Debug.LogWarning($"{name}: gezinme noktası ya da NPC prefab'ı yok, kalabalık kapalı.");
            return;
        }

        for (int i = 0; i < population; i++)
            SpawnInitial();
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

        Vector3 pos = startSpot != null ? startSpot.transform.position : Jitter(wanderPoints[Random.Range(0, wanderPoints.Count)]);
        if (!NavMesh.SamplePosition(pos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            return;
        Spawn(hit.position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), startSpot);
    }

    private NpcAgent Spawn(Vector3 position, Quaternion rotation, NpcSpot startSpot, NpcProfile forced = null)
    {
        NpcProfile profile = forced ?? PickProfile();
        GameObject go = Instantiate(profile.prefab, position, rotation, transform);
        go.name = profile.prefab.name;

        // Prefab'daki tek animasyonlu eski bileşenler kalabalıkta kullanılmaz.
        foreach (var old in go.GetComponents<NpcAnimationPlayer>())
            DestroyImmediate(old);
        foreach (var old in go.GetComponents<NpcPatrol>())
            DestroyImmediate(old);

        profile.ApplyRandomOutfit(go.transform);

        var npc = go.AddComponent<NpcAgent>();
        npc.Init(this, profile, Random.Range(speedRange.x, speedRange.y));
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

    // Orta şeritler (|x| < 4.5) sağdan akar: +x şeridi +z'ye, -x şeridi -z'ye. Kaldırımlar iki yöne de açık.
    private bool RouteAllows(int route, int heading)
    {
        float x = routeX[route];
        if (Mathf.Abs(x) >= finaleLaneX.y)
            return true;
        return x * heading >= 0f;
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
    public void BeginFinale(Vector3 center)
    {
        if (finale || wanderPoints.Count == 0)
            return;
        finale = true;
        finaleCenter = center;
        finalePath = new NavMeshPath();
        // Uzaktakiler (görünmezler) kalabalığa yürüyüp gelmeye çalışmaz, yerler yakındakilere ve yeni doğanlara kalır.
        foreach (NpcAgent agent in agents)
        {
            Vector3 d = agent.transform.position - center;
            d.y = 0f;
            if (d.sqrMagnitude < joinDistance * joinDistance)
                joiners.Add(agent);
            agent.Interrupt();
        }

        BuildCrowdSlots(center, ringCount + finaleExtra + joiners.Count + 8);

        Camera cam = playerCamera != null ? playerCamera : Camera.main;
        SpawnRing(center, cam);

        int spawned = 0;
        for (int attempt = 0; attempt < finaleExtra * 8 && spawned < finaleExtra; attempt++)
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            Vector3 p = new Vector3(Random.Range(finaleLaneX.x, finaleLaneX.y), center.y,
                center.z + side * Random.Range(finaleSpawnBand.x, finaleSpawnBand.y));
            if (p.z < finaleLaneZ.x || p.z > finaleLaneZ.y || (cam != null && InView(cam, p)))
                continue;
            if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
                continue;
            // Caddeye bağlı olmayan adacıklar (binanın kapı basamakları) elenir: orada doğan NPC yerinden kıpırdamıyordu.
            if (!NavMesh.CalculatePath(hit.position, center, NavMesh.AllAreas, finalePath)
                || finalePath.status != NavMeshPathStatus.PathComplete)
                continue;
            Vector3 toCenter = center - hit.position;
            toCenter.y = 0f;
            NpcAgent npc = Spawn(hit.position, Quaternion.LookRotation(toCenter), null);
            npc.GoalLimit = int.MaxValue;
            npc.SetSpeed(Random.Range(1.2f, 1.6f));
            joiners.Add(npc);
            spawned++;
        }
    }

    // Oyuncuya benzeyen kadınlar görüş dışında, oyuncunun cadde boyunca önünde/arkasında doğar, her biri halkada bir
    // yere yürür ve ona dönük durur. Halkanın içi boş kalır: final kamerası karakteri tepeden, halkanın üstünden görür.
    private void SpawnRing(Vector3 center, Camera cam)
    {
        if (ringCount <= 0 || ringProfile < 0 || ringProfile >= profiles.Length || profiles[ringProfile].prefab == null)
            return;

        NpcProfile profile = profiles[ringProfile];
        int placed = 0;
        for (int attempt = 0; attempt < ringCount * 20 && placed < ringCount; attempt++)
        {
            // Yer bulunamazsa bant giderek genişler.
            float widen = attempt / (float)(ringCount * 10);
            float side = Random.value < 0.5f ? -1f : 1f;
            Vector3 p = new Vector3(Random.Range(finaleLaneX.x, finaleLaneX.y), center.y,
                center.z + side * Random.Range(ringSpawnBand.x, ringSpawnBand.y * (1f + widen)));
            if (p.z < finaleLaneZ.x || p.z > finaleLaneZ.y || (cam != null && InView(cam, p) && widen < 1f))
                continue;
            if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)
                || !NavMesh.CalculatePath(hit.position, center, NavMesh.AllAreas, finalePath)
                || finalePath.status != NavMeshPathStatus.PathComplete)
                continue;

            Vector3 toCenter = center - hit.position;
            toCenter.y = 0f;
            NpcAgent npc = Spawn(hit.position, Quaternion.LookRotation(toCenter), null, profile);
            npc.GoalLimit = int.MaxValue;
            npc.SetSpeed(ringWalkSpeed * Random.Range(0.9f, 1.15f));
            SetLook(npc.transform, profile, ringLook);
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
            if (crowdSlots[i].owner != null)
                continue;
            if (lookalike && i >= innerLimit && best >= 0)
                break;
            // En içteki birkaç sıra benzerlere ayrılır; diğerleri onlardan hemen sonra gelir (boşluk kalmasın).
            if (!lookalike && i < innerLimit * 0.35f)
                continue;
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

    /// <summary>Halkadaki NPC yerine vardı.</summary>
    public void NotifyRingArrived(NpcAgent npc)
    {
        if (joiners.Contains(npc))
            ringArrived++;
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
            {
                Vector3 p = crowdSlots[slot].point;
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
                    rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.LookRotation(look),
                    talk = talk
                };
            }
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
        StartCoroutine(RespawnLater());
    }

    private IEnumerator RespawnLater()
    {
        yield return new WaitForSeconds(Random.Range(respawnDelay.x, respawnDelay.y));

        while (true)
        {
            Transform exit = exits[Random.Range(0, exits.Length)];
            if (exit != null && !IsSeen(exit.position)
                && NavMesh.SamplePosition(exit.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                Spawn(hit.position, exit.rotation, null);
                yield break;
            }
            yield return new WaitForSeconds(1f);
        }
    }

    public bool IsSeen(Vector3 position)
    {
        if (player == null || playerCamera == null)
            return false;
        if ((position - player.position).sqrMagnitude > hiddenDistance * hiddenDistance)
            return false;
        Vector3 v = playerCamera.WorldToViewportPoint(position + Vector3.up);
        bool inView = v.z > 0f && v.x > -0.1f && v.x < 1.1f && v.y > -0.1f && v.y < 1.1f;
        return inView || (position - player.position).sqrMagnitude < 10f * 10f;
    }
}
