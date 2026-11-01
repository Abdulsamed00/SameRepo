using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// <see cref="NpcCrowd"/>'un doğurduğu bir NPC. NavMesh üzerinde yürür, kalabalığın verdiği hedefe gider:
/// rastgele bir noktaya gezinme, bir <see cref="NpcSpot"/>'ta vakit geçirme ya da çıkışa gidip kaybolma.
/// Yürüme klibi yerinde oynar, animasyon hızı ajanın gerçek hızına göre ayarlanır (ayak kaymasın).
/// Noktadayken ajan kapanır, yerine engel (NavMeshObstacle) açılır ki diğerleri etrafından dolaşsın.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class NpcAgent : MonoBehaviour
{
    private const float WalkStartSpeed = 0.25f;
    private const float WalkStopSpeed = 0.1f;
    private const float CrossFade = 0.25f;
    private const float AlignTime = 0.6f;
    // Ignore Raycast (NPC gövdeleri) hariç.
    private const int GroundMask = ~(1 << 2);

    private NpcCrowd crowd;
    private NpcProfile profile;
    private Animator animator;
    private NavMeshAgent agent;
    private NavMeshObstacle obstacle;

    private string currentState;
    private bool walking;
    private bool busy;
    private NpcSpot spot;

    public bool AtSpot { get; private set; }
    /// <summary>Şu an ne yaptığı (hata ayıklama için).</summary>
    public string Status { get; private set; }
    public int GoalsDone { get; private set; }
    public int GoalLimit { get; set; }

    // Kalabalık yeni bir düzene geçti (final): o anki yürüyüş ya da noktada bekleme yarıda bırakılır.
    private bool interrupted;

    /// <summary>O anki hedefi bırakıp kalabalıktan yenisini ister (noktadaysa kalkar).</summary>
    public void Interrupt() => interrupted = true;

    public void Init(NpcCrowd owner, NpcProfile npcProfile, float speed)
    {
        crowd = owner;
        profile = npcProfile;

        animator = GetComponent<Animator>();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        agent = GetComponent<NavMeshAgent>();
        agent.speed = speed;
        agent.angularSpeed = 300f;
        agent.acceleration = 4f;
        agent.radius = 0.3f;
        agent.height = 1.8f;
        agent.stoppingDistance = 0.15f;
        agent.autoBraking = true;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
        agent.avoidancePriority = Random.Range(40, 60);
        agent.updatePosition = false;

        // Engel eklenirken açık gelir; ajanla aynı anda açık olmasın diye ajan bu sırada kapalı.
        agent.enabled = false;
        obstacle = gameObject.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Capsule;
        obstacle.radius = 0.3f;
        obstacle.height = 1.8f;
        obstacle.center = new Vector3(0f, 0.9f, 0f);
        obstacle.carving = false;
        obstacle.enabled = false;
        agent.enabled = true;

        // Oyuncu NPC'lerin içinden geçmesin. Ignore Raycast: etkileşim ışınını kesmez.
        gameObject.layer = 2;
        var body = gameObject.AddComponent<CapsuleCollider>();
        body.center = new Vector3(0f, 0.9f, 0f);
        body.height = 1.75f;
        body.radius = 0.25f;
        var rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        Play(profile.idleState, Random.value);
    }

    /// <summary>Doğduğu anda bir noktada (bankta, vitrin önünde) başlasın diye: oraya ışınlanır, sonra normal döngüye girer.</summary>
    public void StartAt(NpcSpot startSpot)
    {
        StartCoroutine(Life(startSpot));
    }

    private void OnDestroy()
    {
        if (spot != null)
            spot.Release(this);
    }

    private IEnumerator Life(NpcSpot startSpot)
    {
        if (startSpot != null)
        {
            int slot = startSpot.Reserve(this);
            if (slot >= 0)
                yield return DoSpot(startSpot, slot, true);
        }
        else
        {
            // Herkes aynı anda yola çıkmasın.
            yield return new WaitForSeconds(Random.Range(0f, 3f));
        }

        while (true)
        {
            interrupted = false;
            NpcCrowd.Goal goal = crowd.NextGoal(this);
            switch (goal.kind)
            {
                case NpcCrowd.GoalKind.Spot:
                    int slot = goal.spot.Reserve(this);
                    if (slot >= 0)
                        yield return DoSpot(goal.spot, slot, false);
                    break;

                case NpcCrowd.GoalKind.Wander:
                    Status = "Gezinme " + goal.point;
                    yield return WalkTo(goal.point);
                    Status = "Bekleme";
                    for (float wait = Random.Range(0f, 3f); wait > 0f && !interrupted; wait -= Time.deltaTime)
                        yield return null;
                    break;

                case NpcCrowd.GoalKind.Leave:
                    Status = "Çıkış " + goal.point;
                    yield return WalkTo(goal.point);
                    if (interrupted)
                        break;
                    // Oyuncunun gözü önünde kaybolmasın: biraz bekler, hâlâ görülüyorsa başka bir çıkışa yürür.
                    for (float wait = 0f; wait < 5f && crowd.IsSeen(transform.position); wait += 0.5f)
                        yield return new WaitForSeconds(0.5f);
                    if (crowd.IsSeen(transform.position))
                        break;
                    crowd.Despawn(this);
                    yield break;
            }

            GoalsDone++;
            yield return null;
        }
    }

    private IEnumerator WalkTo(Vector3 target)
    {
        if (!agent.enabled || !agent.isOnNavMesh || !agent.SetDestination(target))
        {
            yield return new WaitForSeconds(1f);
            yield break;
        }

        float timeout = Vector3.Distance(transform.position, target) / Mathf.Max(agent.speed, 0.1f) * 2f + 10f;
        Vector3 lastPos = transform.position;
        float stuckTimer = 0f;

        while (timeout > 0f && !interrupted)
        {
            if (!agent.pathPending)
            {
                if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
                    break;
                if (agent.remainingDistance <= agent.stoppingDistance + 0.05f)
                    break;
            }

            // Sıkıştıysa (kalabalıkta kilitlenme) vazgeç, sıradaki hedefe geç.
            stuckTimer += Time.deltaTime;
            if (stuckTimer > 5f)
            {
                if ((transform.position - lastPos).sqrMagnitude < 0.3f * 0.3f)
                    break;
                lastPos = transform.position;
                stuckTimer = 0f;
            }

            timeout -= Time.deltaTime;
            yield return null;
        }

        if (agent.enabled && agent.isOnNavMesh)
            agent.ResetPath();
    }

    private IEnumerator DoSpot(NpcSpot target, int slotIndex, bool teleport)
    {
        spot = target;
        Status = "Nokta " + target.name;
        Transform slot = target.Slot(slotIndex);
        Vector3 approach = target.ApproachPoint(slotIndex);
        if (NavMesh.SamplePosition(approach, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
            approach = hit.position;

        if (teleport)
        {
            agent.Warp(approach);
        }
        else
        {
            yield return WalkTo(approach);
            if ((transform.position - approach).sqrMagnitude > 1.2f * 1.2f)
            {
                // Varamadı (yol yok ya da tıkandı).
                target.Release(this);
                spot = null;
                yield break;
            }
        }

        busy = true;
        walking = false;
        animator.speed = 1f;
        agent.enabled = false;
        obstacle.enabled = true;

        // Oturma slotu kökün bankın üstündeki yeridir; ayakta duranlar zemine basar.
        Vector3 slotPos = target.Activity == NpcActivity.Sit ? slot.position : Grounded(slot.position);
        approach = Grounded(approach);
        string state = target.Activity == NpcActivity.Sit ? profile.sitState : profile.idleState;
        if (teleport)
        {
            transform.SetPositionAndRotation(slotPos, slot.rotation);
            Play(state, Random.value);
        }
        else
        {
            Play(state);
            yield return Align(slotPos, slot.rotation);
        }

        AtSpot = true;
        float remaining = target.RandomDuration;
        if (teleport)
            remaining *= Random.value;
        while (remaining > 0f && !interrupted)
        {
            if (target.Activity == NpcActivity.Talk)
                Play(target.HasCompany(this) ? profile.talkState : profile.idleState);
            remaining -= Time.deltaTime;
            yield return null;
        }
        AtSpot = false;

        Play(profile.idleState);
        yield return Align(approach, transform.rotation);

        target.Release(this);
        spot = null;
        obstacle.enabled = false;
        agent.enabled = true;
        agent.Warp(approach);
        busy = false;
    }

    private IEnumerator Align(Vector3 position, Quaternion rotation)
    {
        Vector3 fromPos = transform.position;
        Quaternion fromRot = transform.rotation;
        for (float t = 0f; t < AlignTime; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / AlignTime);
            transform.SetPositionAndRotation(Vector3.Lerp(fromPos, position, k), Quaternion.Slerp(fromRot, rotation, k));
            yield return null;
        }
        transform.SetPositionAndRotation(position, rotation);
    }

    private void Update()
    {
        if (busy || animator == null || !agent.enabled)
            return;

        float speed = agent.velocity.magnitude;
        if (!walking && speed > WalkStartSpeed)
        {
            walking = true;
            Play(profile.walkState);
        }
        else if (walking && speed < WalkStopSpeed)
        {
            walking = false;
            Play(profile.idleState);
        }

        animator.speed = walking ? Mathf.Clamp(speed / profile.walkClipSpeed, 0.5f, 2f) : 1f;
    }

    private void LateUpdate()
    {
        // NavMesh yüzeyi zeminden birkaç cm yukarıda kalıyor (voksel hassasiyeti): konum ajandan, yükseklik gerçek zeminden.
        if (agent != null && agent.enabled)
            transform.position = Grounded(agent.nextPosition);
    }

    private static Vector3 Grounded(Vector3 p)
    {
        if (Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 1f, GroundMask, QueryTriggerInteraction.Ignore))
            p.y = hit.point.y;
        return p;
    }

    private void Play(string state, float normalizedTime = -1f)
    {
        if (state == currentState)
            return;
        currentState = state;
        if (normalizedTime >= 0f)
            animator.Play(state, 0, normalizedTime);
        else
            animator.CrossFadeInFixedTime(state, CrossFade);
    }
}
