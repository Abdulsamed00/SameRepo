using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Fragmanda karakterin oyunculuğu: Timeline zamanına bağlı komutlar (ışınla, yürü, dön, kalk, dolabı aç, ürünü al,
/// öde, aynada yenil, burnuna dokun, saçı boya, final kalabalığını topla...). Oyunun kendi sistemleri kullanılır
/// (FirstPersonController.ScriptedVelocity, PlayerSitController, PlayerBody, Openable, Checkout, NpcCrowd): karakter
/// oyunda nasıl hareket ediyorsa fragmanda da öyle. Negatif zamanlı komutlar Timeline başlamadan (preRoll) çalışır.
/// <see cref="TrailerController"/> oyuncuyu <c>Acting</c> modunda (görünür, üçüncü şahıs, girdisiz) kurar.
/// </summary>
[DefaultExecutionOrder(200)]
public class TrailerActor : MonoBehaviour
{
    public enum Act
    {
        Place,          // point'e ışınla, value = yön (derece)
        Walk,           // point'e yürü, value = hız (0 = 1.4 m/sn)
        Face,           // value = yön (derece), 0.6 sn'de döner
        Sit,            // target = Seat adı, animasyonsuz oturur
        StandUp,
        Toggle,         // target = Openable adı, eliyle açar/kapar
        Hold,           // target = PickupItem adı, eline alır
        Buy,            // target = Checkout adı, elindekini satın alır (poşet gelir)
        PrepareForPose, // boşta durur (yürüme animasyonu kesilir)
        Defeat,         // yenilgi pozu (tek sefer, son karede kalır)
        Pose,           // ayna pozu
        StopPose,
        Touch,          // sağ el kafaya göre point'e (sağ, yukarı, ileri) uzanır, value = kalma süresi
        Dye,            // saçı boyar (anında)
        Surgery,        // estetik (anında)
        Finale,         // kalabalık point'in çevresine toplanır (oyunun final kalabalığı)
        CrowdSize,      // final kalabalığının boyu: point.x = benzer (ringCount), point.y = ek (finaleExtra)
        Conform,        // kalabalık point yönüne döner
        SkyCamera,      // oyuncunun (kapalı) kamerası göğe bakar: final kalabalığı onu "görüş" saymaz
        BagInHand,      // kasadan sonra gelen poşet (oyunda kameraya bağlı) sağ elde taşınır
        HideObject,     // target = sahnedeki obje adı (yol), kapatılır
        ShowObject,
    }

    [System.Serializable]
    public class Cue
    {
        public float time;
        public Act act;
        public string target;
        public Vector3 point;
        public float value;
    }

    public List<Cue> cues = new List<Cue>();

    private FirstPersonController player;
    private PlayerInteractor interactor;
    private PlayerSitController sitter;
    private PlayerBody body;
    private Coroutine walking;
    private int next;
    private bool bagInHand;
    private Transform bag;

    public void Begin(FirstPersonController fpc)
    {
        player = fpc;
        interactor = fpc.GetComponent<PlayerInteractor>();
        sitter = fpc.GetComponent<PlayerSitController>();
        body = fpc.GetComponentInChildren<PlayerBody>(true);
        cues.Sort((a, b) => a.time.CompareTo(b.time));
        next = 0;
    }

    /// <summary>Kontrolcü her kare çağırır; zamanı gelen komutlar sırayla çalışır.</summary>
    public void Tick(float clock)
    {
        if (player == null)
            return;
        while (next < cues.Count && cues[next].time <= clock)
        {
            Cue cue = cues[next++];
            try
            {
                Run(cue);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Trailer komutu çalışmadı: " + cue.act + " " + cue.target + "\n" + e);
            }
        }
    }

    private void Run(Cue cue)
    {
        switch (cue.act)
        {
            case Act.Place: Place(cue.point, cue.value); break;
            case Act.Walk:
                StopWalk();
                walking = StartCoroutine(WalkTo(cue.point, cue.value > 0f ? cue.value : 1.4f));
                break;
            case Act.Face: StartCoroutine(FaceTo(cue.value)); break;
            case Act.Sit:
                foreach (Seat seat in FindObjectsByType<Seat>(FindObjectsSortMode.None))
                {
                    if (seat.name == cue.target)
                        sitter.SitImmediately(seat, cue.point);
                }
                break;
            case Act.StandUp: sitter.StandUp(); break;
            case Act.Toggle: Toggle(Find<Openable>(cue.target)); break;
            case Act.Hold:
                PickupItem item = Find<PickupItem>(cue.target);
                if (item != null)
                    interactor.Hold(item);
                break;
            case Act.Buy:
                Checkout checkout = Find<Checkout>(cue.target);
                if (checkout != null)
                    checkout.Interact(interactor);
                break;
            case Act.PrepareForPose: StopWalk(); body.PrepareForPose(); break;
            case Act.Defeat: body.PlayDefeat(); break;
            case Act.Pose: body.PlayPose(); break;
            case Act.StopPose: body.StopPose(); break;
            case Act.Touch:
            {
                Animator animator = body.GetComponent<Animator>();
                Transform head = animator != null ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
                if (head != null)
                {
                    // point: kafaya göre (sağ, yukarı, ileri) metre, karakterin gövde yönünde.
                    Transform root = player.transform;
                    Vector3 target = head.position + root.right * cue.point.x + Vector3.up * cue.point.y + root.forward * cue.point.z;
                    body.BeginTouch(head, target, cue.value);
                }
                break;
            }
            case Act.Dye: PlayerLook.DyeHair(); break;
            case Act.Surgery: PlayerLook.HaveSurgery(); break;
            case Act.Finale:
            {
                NpcCrowd crowd = FindFirstObjectByType<NpcCrowd>();
                if (crowd != null)
                {
                    crowd.BeginFinale(cue.point, cue.point);
                    crowd.PlayerArrived(cue.point);
                }
                break;
            }
            case Act.CrowdSize:
            {
                NpcCrowd crowd = FindFirstObjectByType<NpcCrowd>();
                BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                if (crowd != null)
                {
                    typeof(NpcCrowd).GetField("ringCount", flags)?.SetValue(crowd, Mathf.RoundToInt(cue.point.x));
                    typeof(NpcCrowd).GetField("finaleExtra", flags)?.SetValue(crowd, Mathf.RoundToInt(cue.point.y));
                }
                break;
            }
            case Act.Conform:
            {
                NpcCrowd crowd = FindFirstObjectByType<NpcCrowd>();
                if (crowd != null)
                    crowd.Conform(cue.point);
                break;
            }
            case Act.SkyCamera:
                player.CameraRoot.localRotation = Quaternion.Euler(-89f, 0f, 0f);
                break;
            case Act.BagInHand: bagInHand = true; break;
            case Act.HideObject:
            case Act.ShowObject:
                GameObject go = FindPath(cue.target);
                if (go != null)
                    go.SetActive(cue.act == Act.ShowObject);
                break;
        }
    }

    // Poşet sapından sağ avuçta, dik sarkar (oyundaki görünüm kameraya bağlı, üçüncü şahısta havada kalıyordu).
    private void LateUpdate()
    {
        if (!bagInHand || body == null || body.RightHandHold == null)
            return;
        if (bag == null)
        {
            GameObject found = GameObject.Find("Alisveris_Poseti");
            if (found == null)
                return;
            bag = found.transform;
        }
        bag.SetPositionAndRotation(body.RightHandHold.position,
            Quaternion.Euler(0f, player.transform.eulerAngles.y + 90f, 0f));
    }

    private void Place(Vector3 point, float yaw)
    {
        StopWalk();
        CharacterController cc = player.GetComponent<CharacterController>();
        bool ccOn = cc != null && cc.enabled;
        if (ccOn)
            cc.enabled = false;
        player.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, yaw, 0f));
        player.CameraRoot.localRotation = Quaternion.identity;
        player.SyncLookToTransforms();
        if (ccOn)
            cc.enabled = true;
        Physics.SyncTransforms();
    }

    private void StopWalk()
    {
        if (walking != null)
            StopCoroutine(walking);
        walking = null;
        if (player != null)
            player.ScriptedVelocity = Vector3.zero;
    }

    private IEnumerator WalkTo(Vector3 target, float speed)
    {
        Transform root = player.transform;
        for (float time = 0f; time < 30f; time += Time.deltaTime)
        {
            Vector3 to = target - root.position;
            to.y = 0f;
            float distance = to.magnitude;
            if (distance < 0.08f)
                break;
            Vector3 direction = to / distance;
            player.ScriptedVelocity = direction * speed * Mathf.Clamp01(distance / 0.5f + 0.2f);
            root.rotation = Quaternion.RotateTowards(root.rotation, Quaternion.LookRotation(direction), 200f * Time.deltaTime);
            player.SyncLookToTransforms();
            yield return null;
        }
        player.ScriptedVelocity = Vector3.zero;
        walking = null;
    }

    private IEnumerator FaceTo(float yaw)
    {
        Transform root = player.transform;
        Quaternion from = root.rotation;
        Quaternion to = Quaternion.Euler(0f, yaw, 0f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.6f)
        {
            root.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            player.SyncLookToTransforms();
            yield return null;
        }
        root.rotation = to;
        player.SyncLookToTransforms();
    }

    // Openable.Toggle'ı kapağın gerçek bir noktasıyla çağırır: oyunda F'ye basılmış gibi sağ el kapağa uzanır.
    private void Toggle(Openable openable)
    {
        if (openable == null)
            return;
        Vector3 eye = player.CameraRoot.position;
        Collider best = null;
        Vector3 bestPoint = Vector3.zero;
        float bestDistance = float.MaxValue;
        foreach (Collider c in openable.GetComponentsInChildren<Collider>(true))
        {
            if (c.isTrigger || !c.enabled || c.name.StartsWith("Govde"))
                continue;
            Vector3 p = c.ClosestPoint(eye);
            float d = (p - eye).sqrMagnitude;
            if (d < bestDistance)
            {
                bestDistance = d;
                best = c;
                bestPoint = p;
            }
        }
        MethodInfo toggle = typeof(Openable).GetMethod("Toggle", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo durationField = typeof(Openable).GetField("duration", BindingFlags.Instance | BindingFlags.NonPublic);
        float duration = durationField != null ? (float)durationField.GetValue(openable) : 0.6f;
        Vector3 normal = (eye - bestPoint).normalized;
        toggle?.Invoke(openable, new object[] { interactor, duration, best, bestPoint, normal, eye });
    }

    private static T Find<T>(string name) where T : Component
    {
        foreach (T c in FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c.name == name)
                return c;
        }
        Debug.LogWarning("Trailer: bulunamadı: " + typeof(T).Name + " " + name);
        return null;
    }

    // "Kok/Alt/Obje" yolu ya da düz ad; kapalı objeler de bulunur.
    private static GameObject FindPath(string path)
    {
        foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (path == root.name)
                return root;
            if (path.StartsWith(root.name + "/"))
            {
                Transform t = root.transform.Find(path.Substring(root.name.Length + 1));
                if (t != null)
                    return t.gameObject;
            }
        }
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name == path)
                return t.gameObject;
        }
        Debug.LogWarning("Trailer: obje bulunamadı: " + path);
        return null;
    }
}
