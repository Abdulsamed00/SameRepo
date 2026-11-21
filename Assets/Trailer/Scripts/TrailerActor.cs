using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Fragmanda oyuncunun karakterini oynatır: <see cref="TrailerController"/> (PlayerMode.Actor) her kare Timeline
/// zamanını verir, sırası gelen eylem bir kez başlar. Eylemler oyunun kendi mekaniklerini çağırır (oturma/kalkma,
/// ScriptedVelocity ile yürüme, dolap kapağı + el IK'sı, ayna pozu, ürün alma, kasa, final kalabalığı).
/// Negatif zamanlı eylemler Timeline başlamadan, ön hazırlık (preRoll) sırasında çalışır.
/// Kesmeler arasında karakter <see cref="TrailerAction.Kind.Place"/> ile bir sonraki çekimin yerine ışınlanır.
/// </summary>
public class TrailerActor : MonoBehaviour
{
    [SerializeField] private List<TrailerAction> actions = new List<TrailerAction>();

    private FirstPersonController player;
    private CharacterController cc;
    private PlayerSitController sitter;
    private PlayerInteractor interactor;
    private PlayerBody body;
    private Animator animator;
    private Coroutine motion;
    private bool ready;

    /// <summary>Kurucu (editör) eylem listesini yazar.</summary>
    public void SetActions(List<TrailerAction> list) => actions = list;

    public void Setup()
    {
        player = FindFirstObjectByType<FirstPersonController>();
        if (player == null)
            return;
        cc = player.GetComponent<CharacterController>();
        sitter = player.GetComponent<PlayerSitController>();
        interactor = player.GetComponent<PlayerInteractor>();
        body = player.GetComponentInChildren<PlayerBody>(true);
        animator = body != null ? body.GetComponent<Animator>() : null;
        if (animator == null && body != null)
            animator = body.GetComponentInChildren<Animator>();

        player.InputEnabled = false;
        player.ScriptedVelocity = Vector3.zero;
        if (sitter != null)
            sitter.ControlsLocked = true;
        if (body != null)
            body.SetThirdPerson(true);
        foreach (TrailerAction action in actions)
            action.done = false;
        ready = true;
    }

    /// <param name="time">Timeline zamanı (ön hazırlık sırasında negatif).</param>
    public void Tick(float time)
    {
        if (!ready)
            return;
        foreach (TrailerAction action in actions)
        {
            if (action.done || time < action.time)
                continue;
            action.done = true;
            Run(action);
        }
    }

    private void Run(TrailerAction a)
    {
        switch (a.kind)
        {
            case TrailerAction.Kind.Place: Place(a.point, a.yaw); break;
            case TrailerAction.Kind.SitOnSeat: SitOnSeat(a.target, a.point); break;
            case TrailerAction.Kind.StandUp: if (sitter != null) sitter.StandUp(); break;
            case TrailerAction.Kind.WalkTo: StartMotion(Walk(a.point, a.speed)); break;
            case TrailerAction.Kind.Face: StartMotion(Face(a.yaw, a.duration)); break;
            case TrailerAction.Kind.OpenPart: OpenPart(a.target, a.index, true); break;
            case TrailerAction.Kind.OpenQuiet: OpenPart(a.target, a.index, false); break;
            case TrailerAction.Kind.TouchNose: TouchNose(a.duration); break;
            case TrailerAction.Kind.Defeat: if (body != null) { body.PrepareForPose(); body.PlayDefeat(); } break;
            case TrailerAction.Kind.Pose: if (body != null) { body.PrepareForPose(); body.PlayPose(); } break;
            case TrailerAction.Kind.Locomotion: if (body != null) body.PlayLocomotion(); break;
            case TrailerAction.Kind.PickUp: PickUp(a.point); break;
            case TrailerAction.Kind.Checkout: DoCheckout(); break;
            case TrailerAction.Kind.CrowdGather: CrowdGather(); break;
            case TrailerAction.Kind.CrowdConform: Crowd(c => c.Conform(Quaternion.Euler(0f, a.yaw, 0f) * Vector3.forward)); break;
            case TrailerAction.Kind.CrowdFreeze: Freeze(); break;
            case TrailerAction.Kind.Buy: PlayerPurchases.Buy((ProductType)a.index); break;
            case TrailerAction.Kind.Morning: DayRoutine.StartNextDay(); break;
        }
    }

    private void StartMotion(IEnumerator routine)
    {
        if (motion != null)
            StopCoroutine(motion);
        motion = StartCoroutine(routine);
    }

    // Karakteri (kesmede) bir sonraki çekimin yerine koyar.
    private void Place(Vector3 point, float yaw)
    {
        if (player == null)
            return;
        if (motion != null)
        {
            StopCoroutine(motion);
            motion = null;
        }
        player.ScriptedVelocity = Vector3.zero;
        bool ccOn = cc != null && cc.enabled;
        if (cc != null)
            cc.enabled = false;
        player.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, yaw, 0f));
        player.CameraRoot.localRotation = Quaternion.identity;
        if (cc != null)
            cc.enabled = ccOn || !player.Frozen;
        player.SyncLookToTransforms();
        Physics.SyncTransforms();
        if (interactor != null && interactor.HeldItem != null)
            interactor.HeldItem.UpdateHeldPose();
    }

    private void SitOnSeat(string seatName, Vector3 near)
    {
        if (sitter == null)
            return;
        foreach (Seat seat in FindObjectsByType<Seat>(FindObjectsSortMode.None))
        {
            if (seat.name == seatName)
            {
                sitter.SitImmediately(seat, near);
                return;
            }
        }
    }

    private IEnumerator Walk(Vector3 target, float speed)
    {
        Transform root = player.transform;
        for (float t = 0f; t < 30f; t += Time.deltaTime)
        {
            if (player.Frozen)
            {
                yield return null;
                continue;
            }
            Vector3 to = target - root.position;
            to.y = 0f;
            float distance = to.magnitude;
            if (distance < 0.06f)
                break;
            Vector3 dir = to / distance;
            // Son yarım metrede yavaşlar, hedefi geçmez.
            player.ScriptedVelocity = dir * speed * Mathf.Clamp01(distance / 0.5f + 0.2f);
            root.rotation = Quaternion.RotateTowards(root.rotation, Quaternion.LookRotation(dir), 180f * Time.deltaTime);
            yield return null;
        }
        player.ScriptedVelocity = Vector3.zero;
        player.SyncLookToTransforms();
        motion = null;
    }

    private IEnumerator Face(float yaw, float duration)
    {
        Transform root = player.transform;
        Quaternion from = root.rotation, to = Quaternion.Euler(0f, yaw, 0f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, duration))
        {
            root.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        root.rotation = to;
        player.SyncLookToTransforms();
        motion = null;
    }

    // Openable'ın bir parçasını (dolap kapağı) oyundaki gibi açar: el kapağa uzanır, kapak sonra döner.
    private void OpenPart(string name, int index, bool reach)
    {
        Openable openable = null;
        foreach (Openable candidate in FindObjectsByType<Openable>(FindObjectsSortMode.None))
            if (candidate.name == name)
                openable = candidate;
        if (openable == null || interactor == null)
            return;

        BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        Openable.Part[] parts = (Openable.Part[])typeof(Openable).GetField("parts", flags).GetValue(openable);
        Transform part = parts != null && index < parts.Length ? parts[index].target : null;
        // reach kapalı: el uzanmaz (vuruş yoksa Openable el IK'sını atlar), karakter olduğu gibi durur.
        Collider hit = reach && part != null ? part.GetComponentInChildren<Collider>(true) : null;
        Vector3 eye = player.transform.position + Vector3.up * 1.5f;
        Vector3 point = hit != null ? hit.ClosestPoint(eye) : openable.transform.position;
        Vector3 normal = (eye - point).normalized;
        MethodInfo toggle = typeof(Openable).GetMethod("Toggle", flags);
        toggle?.Invoke(openable, new object[] { interactor, openable.Duration, hit, point, normal, eye });
    }

    // Sağ el buruna gider ve orada kalır (kapı açarken kullanılan el IK'sı, kafa kemiğine bağlı).
    private void TouchNose(float hold)
    {
        if (body == null || animator == null)
            return;
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        if (head == null)
            return;
        Vector3 forward = animator.transform.forward;
        forward.y = 0f;
        forward.Normalize();
        // Kafa kemiği kafatasının dibinde; burun ucu ~0.11 önde, kemikle aynı yükseklikte (0.05 yukarısı saça gidiyordu).
        Vector3 nose = head.position + forward * 0.12f;
        body.BeginTouch(head, nose, hold);
    }

    private void PickUp(Vector3 near)
    {
        if (interactor == null)
            return;
        ShopProduct best = null;
        float bestDistance = float.MaxValue;
        foreach (ShopProduct product in FindObjectsByType<ShopProduct>(FindObjectsSortMode.None))
        {
            float d = (product.transform.position - near).sqrMagnitude;
            if (d < bestDistance)
            {
                bestDistance = d;
                best = product;
            }
        }
        if (best != null)
            interactor.Hold(best);
    }

    private void DoCheckout()
    {
        Checkout checkout = FindFirstObjectByType<Checkout>();
        if (checkout != null && interactor != null)
            checkout.Interact(interactor);
    }

    private void CrowdGather()
    {
        Vector3 p = player.transform.position;
        Crowd(c =>
        {
            c.BeginFinale(p, p);
            c.PlayerArrived(p);
        });
    }

    private void Freeze()
    {
        Crowd(c => c.FreezeFinale());
        if (animator != null)
            animator.speed = 0f;
    }

    private static void Crowd(System.Action<NpcCrowd> act)
    {
        NpcCrowd crowd = FindFirstObjectByType<NpcCrowd>();
        if (crowd != null)
            act(crowd);
    }
}

[System.Serializable]
public class TrailerAction
{
    public enum Kind
    {
        Place, SitOnSeat, StandUp, WalkTo, Face, OpenPart, TouchNose, Defeat, Pose, Locomotion, PickUp, Checkout,
        CrowdGather, CrowdConform, CrowdFreeze,
        OpenQuiet,  // sona eklendi: sahnelerde kayıtlı eski sıra numaraları bozulmasın
        Buy,        // index = ProductType: ürünü satın alınmış sayar (evde yığın belirir)
        Morning     // ilk gecenin sabahı: ev gündüz ışığına geçer
    }

    public float time;
    public Kind kind;
    public Vector3 point;
    public float yaw;
    public float speed = 1.4f;
    public float duration = 1f;
    public string target;
    public int index;
    [System.NonSerialized] public bool done;
}
