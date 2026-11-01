using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Düzlemsel ayna yansıması. Oyuncu kamerasının ayna düzlemine göre simetriğinde duran bir kamera, ayna yüzeyine
/// tam oturan eğik (off-axis) bir frustum ile RenderTexture'a çizer; yüzey bu dokuyu gösterir. Yakın düzlem ayna
/// düzlemindedir, aynanın arkasında kalan (duvar vb.) çizilmez. Kamera dönüşü yüzeyle aynı olduğu için görüntü
/// ayna çevirmesi olmadan doğru çıkar: yüzeyin UV'si izleyiciye göre yatayda zaten ters.
/// Bu objenin +z ekseni odaya bakar, merkezi camın ortasındadır (ölçek 1). Sadece ayna oyuncu kamerasında
/// görünürken çizilir. Yansıma kamerası oyuncu kamerasının culling mask'ını kullanmaz: kafa (PlayerHead) aynada görünür.
/// </summary>
[DefaultExecutionOrder(10000)]
public class PlanarMirror : MonoBehaviour
{
    [Header("Yüzey")]
    [Tooltip("Camın genişliği ve yüksekliği (m).")]
    [SerializeField] private float width = 0.406f;
    [SerializeField] private float height = 1.719f;
    [Tooltip("URP Unlit malzeme; dokusu yansımayla değiştirilir.")]
    [SerializeField] private Material material;
    [Tooltip("Ayna camı yansımayı biraz soluk ve soğuk gösterir.")]
    [SerializeField] private Color tint = new Color(0.9f, 0.92f, 0.93f);

    [Header("Yansıma")]
    [Tooltip("Doku yüksekliği = ekran yüksekliği x bu (pozda cam ekranın neredeyse tamamını kaplıyor).")]
    [SerializeField] private float resolutionScale = 1.5f;
    [SerializeField] private int maxTextureHeight = 2048;
    [Tooltip("Oyuncu kamerası bundan uzaktaysa yansıma çizilmez (m).")]
    [SerializeField] private float maxDistance = 10f;
    [SerializeField] private float farClip = 30f;

    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private Camera reflectionCamera;
    private RenderTexture texture;
    private MeshRenderer surfaceRenderer;
    private Camera viewer;
    private MaterialPropertyBlock block;
    private readonly Plane[] viewerPlanes = new Plane[6];

    private void Awake()
    {
        GameObject surface = new GameObject("Ayna_Yuzey");
        surface.transform.SetParent(transform, false);
        // Yansımayı yansıma kamerası zaten soldurdu (oyuncu hariç); yüzey ikinci kez solmasın.
        int exemptLayer = LayerMask.NameToLayer(SceneFade.ExemptLayerName);
        if (exemptLayer >= 0)
            surface.layer = exemptLayer;
        surface.AddComponent<MeshFilter>().sharedMesh = BuildQuad();
        surfaceRenderer = surface.AddComponent<MeshRenderer>();
        surfaceRenderer.sharedMaterial = material;
        surfaceRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        surfaceRenderer.receiveShadows = false;
        block = new MaterialPropertyBlock();

        reflectionCamera = new GameObject("Ayna_Yansima_Kamera").AddComponent<Camera>();
        reflectionCamera.transform.SetParent(transform, false);
        reflectionCamera.farClipPlane = farClip;
        reflectionCamera.enabled = false;
        UniversalAdditionalCameraData data = reflectionCamera.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = false;
        data.requiresDepthTexture = false;
        data.requiresColorTexture = false;
    }

    private void OnDestroy()
    {
        if (texture != null)
            texture.Release();
    }

    // Ekran çözünürlüğü değişince doku yeniden oluşturulur.
    private void EnsureTexture()
    {
        int textureHeight = Mathf.Min(maxTextureHeight, Mathf.RoundToInt(Screen.height * resolutionScale));
        if (texture != null && texture.height == textureHeight)
            return;
        if (texture != null)
            texture.Release();

        int textureWidth = Mathf.Max(16, Mathf.RoundToInt(textureHeight * width / height));
        // Mipmap + anizotropik: ayna uzaktan küçük görünürken yansıma titremesin.
        texture = new RenderTexture(textureWidth, textureHeight, 24, RenderTextureFormat.DefaultHDR)
        {
            name = "Ayna_Yansima",
            antiAliasing = 4,
            useMipMap = true,
            autoGenerateMips = true,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 8
        };
        reflectionCamera.targetTexture = texture;
        block.SetTexture(BaseMapId, texture);
        block.SetColor(BaseColorId, tint);
        surfaceRenderer.SetPropertyBlock(block);
    }

    // Kamera hareketi (bakış, kafa sallanması, poz) bittikten sonra.
    private void LateUpdate()
    {
        if (viewer == null || !viewer.isActiveAndEnabled)
            viewer = Camera.main;

        Vector3 eye = viewer != null ? transform.InverseTransformPoint(viewer.transform.position) : Vector3.zero;
        bool render = viewer != null && viewer.isActiveAndEnabled && eye.z > 0.01f && eye.z < maxDistance
            && IsVisibleTo(viewer);
        reflectionCamera.enabled = render;
        if (!render)
            return;
        EnsureTexture();

        // Göz ayna düzleminin arkasına yansıtılır; kamera yüzeyle aynı yöne (odaya) bakar.
        reflectionCamera.transform.SetPositionAndRotation(
            transform.TransformPoint(new Vector3(eye.x, eye.y, -eye.z)), transform.rotation);

        // Yakın düzlem camın hemen arkasında: yüzeyin kendisi yansımaya girmesin.
        float distance = eye.z;
        float near = distance + 0.005f;
        float scale = near / distance;
        float left = (-0.5f * width - eye.x) * scale;
        float right = (0.5f * width - eye.x) * scale;
        float bottom = (-0.5f * height - eye.y) * scale;
        float top = (0.5f * height - eye.y) * scale;

        // LOD seçimi için yaklaşık görüş açısı; projeksiyon bundan sonra elle verilir.
        reflectionCamera.fieldOfView = 2f * Mathf.Atan(0.5f * (top - bottom) / near) * Mathf.Rad2Deg;
        reflectionCamera.aspect = (right - left) / (top - bottom);
        reflectionCamera.nearClipPlane = near;
        reflectionCamera.projectionMatrix = Matrix4x4.Frustum(left, right, bottom, top, near, farClip);
    }

    private bool IsVisibleTo(Camera cam)
    {
        GeometryUtility.CalculateFrustumPlanes(cam, viewerPlanes);
        return GeometryUtility.TestPlanesAABB(viewerPlanes, surfaceRenderer.bounds);
    }

    // +z'den bakınca ön yüz; u = 0 yerel -x'te (yansıma kamerasının solu, izleyicinin sağı).
    private Mesh BuildQuad()
    {
        float x = 0.5f * width;
        float y = 0.5f * height;
        Mesh mesh = new Mesh { name = "Ayna_Yuzey" };
        mesh.vertices = new[]
        {
            new Vector3(-x, -y, 0f), new Vector3(x, -y, 0f), new Vector3(-x, y, 0f), new Vector3(x, y, 0f)
        };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        mesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
        mesh.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
        mesh.RecalculateBounds();
        return mesh;
    }
}
