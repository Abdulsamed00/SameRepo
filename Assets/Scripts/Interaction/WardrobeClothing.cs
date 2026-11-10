using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Dolapta asılı, satın alınmış kıyafet. Ürün alınana kadar görünmez; alınınca dolapta asılı durur.
/// F ile incelenir: kıyafet kameranın önüne gelir, fareyle sağa sola döndürülür. İncelerken F giyer
/// (<see cref="PlayerOutfit"/>, kıyafet dolaptan kalkar), G geri asar. Oyuncu inceleme boyunca durur.
/// Görünen model <see cref="model"/> altındadır, collider'ı da orada olmalı (ışın ona çarpar).
/// </summary>
public class WardrobeClothing : Interactable
{
    // Açık dolabın önünde: askıdakine ~0.6 m, tabandaki ayakkabıya en yakın 1.43 m (ölçüldü).
    protected override float DefaultMaxDistance => 1.7f;

    [SerializeField] private ProductType product;
    [Tooltip("Askıdaki model (collider'ıyla). Ürün alınana ya da giyilene kadar kapalı tutulur.")]
    [SerializeField] private Transform model;
    [Tooltip("Döndürme (Look) ve geri asma (Drop) bu asset'in Player map'inden okunur.")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("İnceleme")]
    [Tooltip("İncelerken kıyafetin kameradan uzaklığı (m). Önünde engel (dolap, duvar) varsa kıyafet daha yakına gelir ve gerekirse küçülür, hiçbir şeyin içine girmez.")]
    [SerializeField] private float inspectDistance = 1.2f;
    [Tooltip("Kıyafet ile önündeki engel arasında bırakılan boşluk (m).")]
    [SerializeField] private float obstacleMargin = 0.05f;
    [Tooltip("Modelin altında bu adda çocuk varsa (askı) incelerken gizlenir: sadece kıyafet görünsün.")]
    [SerializeField] private string hangerName = "Askilik";
    [SerializeField] private float moveDuration = 0.4f;
    [Tooltip("Fareyle döndürme hızı (derece/piksel).")]
    [SerializeField] private float rotateSpeed = 0.4f;
    [Tooltip("İncelerken modelin ek dönüşü (derece). Önü +z'ye bakmayan modeller için (ör. çantanın yüzü yana bakıyor).")]
    [SerializeField] private Vector3 inspectEuler;
    [SerializeField] private string wearPrompt = "Giy";
    [SerializeField] private string putBackHint = "Geri as";

    [Header("Ses")]
    [Tooltip("Askıdan alınınca, geri asılınca ve giyilince çalar.")]
    [SerializeField] private AudioClip handleSound;

    private static readonly System.Collections.Generic.List<WardrobeClothing> instances =
        new System.Collections.Generic.List<WardrobeClothing>();

    /// <summary>Sahnede bu ürün için dolapta kıyafet var mı (yoksa günlük döngüde giyme adımı atlanır).</summary>
    public static bool ExistsFor(ProductType type) => instances.Exists(c => c.product == type);

    private InputAction lookAction;
    private InputAction dropAction;
    private PlayerInteractor inspector;
    private Transform view;
    private Vector3 homePosition;
    private Quaternion homeRotation;
    private Vector3 modelCenter;
    private Vector3 homeScale;
    private Vector3 homeLossyScale;
    private Transform[] hangers;
    private float inspectScale = 1f;
    private float inspectDist;
    private float yaw;
    private bool busy;

    /// <summary>Dolap sesi (kıyafet alma/bırakma); <see cref="OutfitChange"/> da kullanır.</summary>
    public static AudioClip SharedHandleSound { get; private set; }

    public ProductType Product => product;

    /// <summary>Dolapta asılı (alındı, giyilmedi).</summary>
    public bool Hanging => Available;

    /// <summary>Şu an inceleniyor (kameranın önünde).</summary>
    public bool Inspecting => inspector != null;

    /// <summary>Alındı ve henüz giyilmedi: dolapta asılı.</summary>
    private bool Available => PlayerPurchases.Owns(product) && !PlayerOutfit.IsWorn(product);

    public override string Prompt => inspector != null ? wearPrompt : $"{product.Accusative()} incele";

    private void Awake()
    {
        KeyBindings.Ensure(inputActions); // kayıtlı tuş atamaları
        InputActionMap playerMap = inputActions.FindActionMap("Player", true);
        lookAction = playerMap.FindAction("Look", true);
        dropAction = playerMap.FindAction("Drop", true);
        if (SharedHandleSound == null)
            SharedHandleSound = handleSound;
        homePosition = model.localPosition;
        homeRotation = model.localRotation;
        homeScale = model.localScale;
        homeLossyScale = model.lossyScale;
        // İnce, çift yüzlü kıyafet kendi gölgesini alınca (özellikle incelerken) kollar saydam/benekli görünüyordu (10.11):
        // gölge almaz (atmaya devam eder).
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            r.receiveShadows = false;
        System.Collections.Generic.List<Transform> found = new System.Collections.Generic.List<Transform>();
        foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            if (t != model && t.name == hangerName)
                found.Add(t);
        hangers = found.ToArray();
        MeshFilter filter = model.GetComponentInChildren<MeshFilter>(true);
        modelCenter = filter != null ? filter.sharedMesh.bounds.center : Vector3.zero;
        model.gameObject.SetActive(Available);
    }

    private void OnEnable() => instances.Add(this);

    private void OnDisable() => instances.Remove(this);

    public override bool CanInteract(PlayerInteractor interactor)
    {
        if (busy)
            return false;
        if (inspector != null)
            return inspector == interactor;
        PlayerSitController sitter = interactor.GetComponent<PlayerSitController>();
        // Sadece günün yeni ürünü tek tek incelenip giyilir; öncekiler sabah tek seferde giyilir (OutfitChange).
        return Available && product == GameDay.Product && !DayRoutine.WornToday && !interactor.IsHolding
               && (sitter == null || !sitter.IsSeated);
    }

    public override void Interact(PlayerInteractor interactor)
    {
        if (busy)
            return;
        if (inspector == null)
            StartCoroutine(Inspect(interactor));
        else
            Wear();
    }

    private void Update()
    {
        if (inspector == null)
        {
            bool show = Available;
            if (model.gameObject.activeSelf != show)
                model.gameObject.SetActive(show);
            return;
        }
        if (busy)
            return;

        if (dropAction.WasPressedThisFrame())
        {
            StartCoroutine(PutBack());
            return;
        }

        yaw += lookAction.ReadValue<Vector2>().x * rotateSpeed;
        model.SetPositionAndRotation(InspectPosition(InspectRotation()), InspectRotation());
    }

    private IEnumerator Inspect(PlayerInteractor interactor)
    {
        FirstPersonController controller = interactor.GetComponent<FirstPersonController>();
        Camera cam = interactor.GetComponentInChildren<Camera>();
        if (controller == null || cam == null)
            yield break;

        busy = true;
        ShoppingBag.Put(); // poşet dolabın önünde bırakılır, kıyafet çıkar
        inspector = interactor;
        view = cam.transform;
        yaw = 0f;
        controller.Frozen = true;
        // Kıyafet kameranın önüne gelince ışın ona bakmayabilir; F her durumda giysin.
        interactor.Zone = this;
        interactor.HintKey = interactor.DropKeyLabel;
        interactor.Hint = putBackHint;
        GameAudio.PlayAt(handleSound, model.position);

        SetHangersVisible(false);
        FitToFreeSpace();
        yield return Move(model.position, model.rotation, () => InspectRotation(), true);
        busy = false;
    }

    private IEnumerator PutBack()
    {
        busy = true;
        GameAudio.PlayAt(handleSound, model.position);
        Transform parent = model.parent;
        yield return Move(model.position, model.rotation, () => parent.rotation * homeRotation, false);
        model.SetLocalPositionAndRotation(homePosition, homeRotation);
        model.localScale = homeScale;
        EndInspection();
    }

    private void Wear()
    {
        GameAudio.PlayAt(handleSound, model.position);
        PlayerOutfit.Wear(product);
        model.SetLocalPositionAndRotation(homePosition, homeRotation);
        model.localScale = homeScale;
        model.gameObject.SetActive(false);
        EndInspection();
        // Günün ürünü giyildiyse uyku gelmeye başlar (esneme).
        DayRoutine.NotifyWorn(product);
    }

    private void EndInspection()
    {
        PlayerInteractor interactor = inspector;
        inspector = null;
        busy = false;
        SetHangersVisible(true);
        if (interactor == null)
            return;

        if (interactor.Zone == this)
            interactor.Zone = null;
        interactor.Hint = null;
        FirstPersonController controller = interactor.GetComponent<FirstPersonController>();
        if (controller != null)
            controller.Frozen = false;
    }

    // Hedef sürekli yeniden hesaplanır (inceleme hedefi kameraya, geri asma hedefi askıya bağlı).
    private IEnumerator Move(Vector3 fromPos, Quaternion fromRot, System.Func<Quaternion> targetRotation, bool toInspect)
    {
        Vector3 fromScale = model.localScale;
        Vector3 toScale = toInspect ? homeScale * inspectScale : homeScale;
        for (float t = 0f; t < 1f; t += Time.deltaTime / moveDuration)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            Quaternion rot = targetRotation();
            Vector3 pos = toInspect ? InspectPosition(rot) : model.parent.TransformPoint(homePosition);
            model.SetPositionAndRotation(Vector3.Lerp(fromPos, pos, k), Quaternion.Slerp(fromRot, rot, k));
            model.localScale = Vector3.Lerp(fromScale, toScale, k);
            yield return null;
        }
        model.localScale = toScale;
    }

    private void SetHangersVisible(bool visible)
    {
        foreach (Transform hanger in hangers)
            if (hanger != null)
                hanger.gameObject.SetActive(visible);
    }

    // Kameranın önündeki ilk engele (dolap gövdesi, duvar, mobilya) kadar boşluğu ölçer. Kıyafet bu boşluğa sığacak
    // uzaklıkta durur; boşluk darsa küçülür (kameradan 1.9 yarıçap uzaklıkta görüntüye sığar, arkası 1 yarıçap).
    private void FitToFreeSpace()
    {
        Bounds bounds = default;
        bool any = false;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(false))
        {
            if (System.Array.IndexOf(hangers, r.transform) >= 0)
                continue;
            if (!any)
                bounds = r.bounds;
            else
                bounds.Encapsulate(r.bounds);
            any = true;
        }
        float radius = any ? bounds.extents.magnitude : 0.3f;

        const float lookAhead = 5f;
        float free = lookAhead;
        RaycastHit[] hits = Physics.SphereCastAll(view.position, Mathf.Min(radius * 0.5f, 0.3f), view.forward, lookAhead,
            // Ignore Raycast sayılmaz: dolabın önündeki görünmez giriş engeli (Dolap/Giris_Engeli, 10.11) kameranın
            // dibinde kalıp kıyafeti en küçük ölçeğe ve 0.3 m'ye çekiyordu.
            ~(1 << 2), QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            Transform t = hit.collider.transform;
            if (hit.distance <= 0f || t.IsChildOf(inspector.transform) || t.IsChildOf(model))
                continue;
            free = Mathf.Min(free, hit.distance);
        }

        inspectScale = Mathf.Clamp(free / (radius * 2.9f), 0.35f, 1f);
        inspectDist = Mathf.Clamp(Mathf.Min(inspectDistance, free - radius * inspectScale - obstacleMargin), 0.3f, inspectDistance);
    }

    // Kıyafetin önü (+z) kameraya bakar; fareyle kendi dikey ekseni etrafında döner.
    private Quaternion InspectRotation() =>
        Quaternion.LookRotation(-view.forward, view.up) * Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(inspectEuler);

    // Modelin pivotu askıda (üst orta); görüntünün ortasına modelin merkezi gelir.
    private Vector3 InspectPosition(Quaternion rotation) =>
        view.position + view.forward * inspectDist - rotation * Vector3.Scale(modelCenter, homeLossyScale * inspectScale);
}
