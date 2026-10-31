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
        Leave
    }

    public struct Goal
    {
        public GoalKind kind;
        public Vector3 point;
        public NpcSpot spot;
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
                new NpcOutfitSlot("Ayakkabı", 0f, "LeftBoot+RightBoot", "ShoeLeft+ShoeRight", "BlackShoeLeft+BlackShoeRight", "BrownShoeLeft+BrownShoeRight"),
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

    [Header("Yeniden Doğma")]
    [SerializeField] private Vector2 respawnDelay = new Vector2(2f, 6f);
    [Tooltip("Oyuncuya bundan yakın ve görüş alanındaki çıkışlarda NPC doğmaz/kaybolmaz.")]
    [SerializeField] private float hiddenDistance = 35f;

    private readonly List<Vector3> wanderPoints = new List<Vector3>();
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
            foreach (Transform t in root.GetComponentsInChildren<Transform>())
            {
                if (t.childCount == 0 && NavMesh.SamplePosition(t.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                    wanderPoints.Add(hit.position);
            }
        }
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

    private NpcAgent Spawn(Vector3 position, Quaternion rotation, NpcSpot startSpot)
    {
        NpcProfile profile = PickProfile();
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
        npc.StartAt(startSpot);
        agents.Add(npc);
        return npc;
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

    public Goal NextGoal(NpcAgent npc)
    {
        Vector3 from = npc.transform.position;

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
