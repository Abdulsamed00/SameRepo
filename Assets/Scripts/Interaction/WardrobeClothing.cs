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
    [SerializeField] private ProductType product;
    [Tooltip("Askıdaki model (collider'ıyla). Ürün alınana ya da giyilene kadar kapalı tutulur.")]
    [SerializeField] private Transform model;
    [Tooltip("Döndürme (Look) ve geri asma (Drop) bu asset'in Player map'inden okunur.")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("İnceleme")]
    [Tooltip("İncelerken kıyafetin kameradan uzaklığı (m).")]
    [SerializeField] private float inspectDistance = 1.2f;
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
    private float yaw;
    private bool busy;

    /// <summary>Alındı ve henüz giyilmedi: dolapta asılı.</summary>
    private bool Available => PlayerPurchases.Owns(product) && !PlayerOutfit.IsWorn(product);

    public override string Prompt => inspector != null ? wearPrompt : $"{product.Accusative()} incele";

    private void Awake()
    {
        InputActionMap playerMap = inputActions.FindActionMap("Player", true);
        lookAction = playerMap.FindAction("Look", true);
        dropAction = playerMap.FindAction("Drop", true);
        homePosition = model.localPosition;
        homeRotation = model.localRotation;
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
        return Available && !interactor.IsHolding && (sitter == null || !sitter.IsSeated);
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
        inspector = interactor;
        view = cam.transform;
        yaw = 0f;
        controller.Frozen = true;
        // Kıyafet kameranın önüne gelince ışın ona bakmayabilir; F her durumda giysin.
        interactor.Zone = this;
        interactor.HintKey = interactor.DropKeyLabel;
        interactor.Hint = putBackHint;
        GameAudio.PlayAt(handleSound, model.position);

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
        EndInspection();
    }

    private void Wear()
    {
        GameAudio.PlayAt(handleSound, model.position);
        PlayerOutfit.Wear(product);
        model.SetLocalPositionAndRotation(homePosition, homeRotation);
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
        for (float t = 0f; t < 1f; t += Time.deltaTime / moveDuration)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            Quaternion rot = targetRotation();
            Vector3 pos = toInspect ? InspectPosition(rot) : model.parent.TransformPoint(homePosition);
            model.SetPositionAndRotation(Vector3.Lerp(fromPos, pos, k), Quaternion.Slerp(fromRot, rot, k));
            yield return null;
        }
    }

    // Kıyafetin önü (+z) kameraya bakar; fareyle kendi dikey ekseni etrafında döner.
    private Quaternion InspectRotation() =>
        Quaternion.LookRotation(-view.forward, view.up) * Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(inspectEuler);

    // Modelin pivotu askıda (üst orta); görüntünün ortasına modelin merkezi gelir.
    private Vector3 InspectPosition(Quaternion rotation) =>
        view.position + view.forward * inspectDistance - rotation * Vector3.Scale(modelCenter, model.lossyScale);
}
