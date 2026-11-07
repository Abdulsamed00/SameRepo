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

    [Header("Uzakta bulanık")]
    [Tooltip("0'dan büyükse: oyuncu camdan bundan uzaktayken yansıma her kare çizilmez, açılışta bir kez çekilmiş " +
             "küçük (bulanık) görüntü gösterilir; yaklaşınca canlı ve tam kaliteli yansımaya geçer (m). 0 = hep canlı.")]
    [SerializeField] private float sharpDistance = 0f;
    [Tooltip("Bulanık görüntünün çekildiği doku yüksekliği (piksel).")]
    [SerializeField] private int blurTextureHeight = 128;
    [Tooltip("Yumuşatma: görüntü bu kadar kez yarıya küçültülüp geri büyütülür; arttıkça daha bulanık.")]
    [Range(1, 6)] [SerializeField] private int blurSteps = 3;
    [Tooltip("Bulanık görüntü, camın tam önünde bu uzaklıkta göz hizasında duran bir izleyiciye göre çekilir (m).")]
    [SerializeField] private float blurEyeDistance = 3f;

    private RenderTexture blurTexture;
    private RenderTexture blurResult;
    private RenderTexture shownTexture;
    private int blurRenderFrame = -1;
    private bool blurReady;
    private bool showingSharp;

    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
    private Vector4 shownSt;

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
        // FPS: yansıma kamerası sahneyi bir kez daha çizer; gölge haritası geçişleri (ve alt ışıkları) en pahalı kısım,
        // aynada gölge farkı pek seçilmiyor.
        // Gölgesiz çizim ev aynasında gündüz güneşi ve gece lambalarını duvarların ötesine sızdırıp yansımayı aşırı
        // parlak yapıyordu (07.11): gölge sadece evdeki yakın aynada (sharpDistance 0) açık; mağaza aynalarında
        // ışıklar zaten gölgesiz, kapalı kalır.
        data.renderShadows = sharpDistance <= 0f;
    }

    // Sahnede serileştirilmiş eski değerler (1.5 / 2048) aynaya bakınca FPS düşürüyordu: tavan koddan uygulanır.
    private const float MaxResolutionScale = 1f;
    private const int MaxTextureCap = 1280;

    private void OnDestroy()
    {
        if (texture != null)
            texture.Release();
        if (blurTexture != null)
            blurTexture.Release();
        if (blurResult != null)
            blurResult.Release();
    }

    // Çekilen küçük görüntü yarıya küçültülerek ve geri büyütülerek (çift doğrusal) yumuşatılır: blok blok
    // piksel yerine düzgün bir bulanıklık. Sonuç çekilen dokunun yarı boyunda tutulur, bir kez yapılır.
    private void SmoothBlur()
    {
        int w = blurTexture.width, h = blurTexture.height;
        RenderTexture[] down = new RenderTexture[blurSteps];
        RenderTexture current = blurTexture;
        for (int i = 0; i < blurSteps; i++)
        {
            w = Mathf.Max(2, w / 2);
            h = Mathf.Max(2, h / 2);
            down[i] = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.DefaultHDR);
            down[i].filterMode = FilterMode.Bilinear;
            Graphics.Blit(current, down[i]);
            current = down[i];
        }

        blurResult = new RenderTexture(blurTexture.width / 2, blurTexture.height / 2, 0, RenderTextureFormat.DefaultHDR)
        {
            name = "Ayna_Yansima_Bulanik_Sonuc",
            filterMode = FilterMode.Bilinear
        };
        for (int i = blurSteps - 2; i >= 0; i--)
        {
            Graphics.Blit(current, down[i]);
            current = down[i];
        }
        Graphics.Blit(current, blurResult);

        foreach (RenderTexture rt in down)
            RenderTexture.ReleaseTemporary(rt);
    }

    // Doku camın sub dikdörtgenini (yerel metre) kaplar; yüzeyin UV'si (0-1 = camın tamamı) ona eşlenir.
    private void ShowTexture(RenderTexture rt, Rect sub)
    {
        if (rt == null)
            return;
        Vector4 st = new Vector4(width / sub.width, height / sub.height,
            (-0.5f * width - sub.xMin) / sub.width, (-0.5f * height - sub.yMin) / sub.height);
        if (rt == shownTexture && st == shownSt)
            return;
        shownTexture = rt;
        shownSt = st;
        block.SetTexture(BaseMapId, rt);
        block.SetVector(BaseMapStId, st);
        block.SetColor(BaseColorId, tint);
        surfaceRenderer.SetPropertyBlock(block);
    }

    private Rect FullGlass => new Rect(-0.5f * width, -0.5f * height, width, height);

    // Camın oyuncu ekranında görünen kısmı (yerel metre) ve ekranda kapladığı piksel boyutu. Cam bir ızgarayla
    // örneklenir; görünen örneklerin sınırı bir ızgara adımı genişletilir. Yaklaşınca sadece görünen parça
    // çizilir: doku ekran piksel yoğunluğunda kalır (eskiden 3.9 m'lik camın tamamı sabit dokuya çiziliyordu,
    // dibinde ekranı dolduran küçük parça birkaç yüz piksele düşüyordu).
    private Rect VisibleRect(out Vector2 pixels)
    {
        const int nx = 12, ny = 24;
        float stepX = width / nx, stepY = height / ny;
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i <= nx; i++)
        {
            for (int j = 0; j <= ny; j++)
            {
                float x = -0.5f * width + i * stepX, y = -0.5f * height + j * stepY;
                Vector3 vp = viewer.WorldToViewportPoint(transform.TransformPoint(new Vector3(x, y, 0f)));
                if (vp.z <= 0f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f)
                    continue;
                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
            }
        }

        if (minX > maxX)
        {
            pixels = new Vector2(Screen.width, Screen.height);
            return FullGlass;
        }

        minX = Mathf.Max(-0.5f * width, minX - stepX); maxX = Mathf.Min(0.5f * width, maxX + stepX);
        minY = Mathf.Max(-0.5f * height, minY - stepY); maxY = Mathf.Min(0.5f * height, maxY + stepY);
        Rect sub = Rect.MinMaxRect(minX, minY, maxX, maxY);

        // Parçanın ekrandaki boyutu: köşeleri ekrana izdüşür (ekranla kırpılır); köşe kameranın arkasındaysa tam ekran.
        float pxMin = 1f, pxMax = 0f, pyMin = 1f, pyMax = 0f;
        bool behind = false;
        Vector2[] corners = { sub.min, sub.max, new Vector2(sub.xMin, sub.yMax), new Vector2(sub.xMax, sub.yMin) };
        foreach (Vector2 c in corners)
        {
            Vector3 vp = viewer.WorldToViewportPoint(transform.TransformPoint(new Vector3(c.x, c.y, 0f)));
            if (vp.z <= 0f) { behind = true; break; }
            pxMin = Mathf.Min(pxMin, Mathf.Clamp01(vp.x)); pxMax = Mathf.Max(pxMax, Mathf.Clamp01(vp.x));
            pyMin = Mathf.Min(pyMin, Mathf.Clamp01(vp.y)); pyMax = Mathf.Max(pyMax, Mathf.Clamp01(vp.y));
        }
        pixels = behind
            ? new Vector2(Screen.width, Screen.height)
            : new Vector2((pxMax - pxMin) * Screen.width, (pyMax - pyMin) * Screen.height);
        return sub;
    }

    // Bulanık görüntü: küçük dokuya tek kare çizilir (kamera bir kare açık kalır), sonra dokunulmaz.
    private void RequestBlur()
    {
        if (blurTexture == null)
        {
            int h = Mathf.Max(8, blurTextureHeight);
            int w = Mathf.Max(4, Mathf.RoundToInt(h * width / height));
            blurTexture = new RenderTexture(w, h, 24, RenderTextureFormat.DefaultHDR)
            {
                name = "Ayna_Yansima_Bulanik",
                filterMode = FilterMode.Bilinear
            };
        }
        reflectionCamera.targetTexture = blurTexture;
        SetupCamera(new Vector3(0f, 0f, blurEyeDistance), FullGlass);
        reflectionCamera.enabled = true;
        blurRenderFrame = Time.frameCount;
    }

    // Doku, camın görünen parçasının ekranda kapladığı piksel x resolutionScale boyundadır (en fazla
    // maxTextureHeight). Her kare yeniden oluşturulmasın diye 32'nin katına yuvarlanır; büyümesi gerekince hemen,
    // %60'ın altına inince küçülür.
    private void EnsureTexture(Vector2 pixels)
    {
        float scale = Mathf.Min(resolutionScale, MaxResolutionScale) * GameSettings.MirrorQuality;
        int cap = Mathf.RoundToInt(Mathf.Min(maxTextureHeight, MaxTextureCap) * GameSettings.MirrorQuality);
        int needW = Mathf.Clamp(Mathf.CeilToInt(pixels.x * scale / 32f) * 32, 32, cap);
        int needH = Mathf.Clamp(Mathf.CeilToInt(pixels.y * scale / 32f) * 32, 32, cap);
        if (texture != null)
        {
            bool grow = needW > texture.width || needH > texture.height;
            bool shrink = needW < texture.width * 0.6f || needH < texture.height * 0.6f;
            if (!grow && !shrink)
                return;
            texture.Release();
        }

        int textureWidth = needW, textureHeight = needH;
        // Mipmap + anizotropik: ayna uzaktan küçük görünürken yansıma titremesin.
        texture = new RenderTexture(textureWidth, textureHeight, 24, RenderTextureFormat.DefaultHDR)
        {
            name = "Ayna_Yansima",
            antiAliasing = 2,
            useMipMap = true,
            autoGenerateMips = true,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 4,
            wrapMode = TextureWrapMode.Clamp
        };
        shownTexture = null;
    }

    // Kamera hareketi (bakış, kafa sallanması, poz) bittikten sonra.
    private void LateUpdate()
    {
        if (viewer == null || !viewer.isActiveAndEnabled)
            viewer = Camera.main;

        Vector3 eye = viewer != null ? transform.InverseTransformPoint(viewer.transform.position) : Vector3.zero;
        bool render = viewer != null && viewer.isActiveAndEnabled && eye.z > 0.01f && eye.z < maxDistance
            && IsVisibleTo(viewer);

        if (sharpDistance > 0f)
        {
            // Bulanık kare çiziliyor: bu kare çizilsin, sonraki karede hazır.
            if (blurRenderFrame >= 0)
            {
                if (Time.frameCount <= blurRenderFrame)
                    return;
                blurRenderFrame = -1;
                blurReady = true;
                reflectionCamera.enabled = false;
                SmoothBlur();
            }

            // Camın dikdörtgenine en yakın noktaya uzaklık; geri dönüşte 0.5 m pay (sınırda gidip gelmesin).
            Vector2 offset = new Vector2(Mathf.Max(0f, Mathf.Abs(eye.x) - 0.5f * width),
                Mathf.Max(0f, Mathf.Abs(eye.y) - 0.5f * height));
            float distanceToGlass = Mathf.Sqrt(offset.sqrMagnitude + eye.z * eye.z);
            bool sharp = render && distanceToGlass < sharpDistance + (showingSharp ? 0.5f : 0f);
            if (!sharp)
            {
                showingSharp = false;
                if (!blurReady)
                {
                    RequestBlur();
                    return;
                }
                reflectionCamera.enabled = false;
                ShowTexture(blurResult, FullGlass);
                return;
            }
            showingSharp = true;
        }

        reflectionCamera.enabled = render;
        if (!render)
            return;
        Rect sub = VisibleRect(out Vector2 pixels);
        EnsureTexture(pixels);
        reflectionCamera.targetTexture = texture;
        ShowTexture(texture, sub);
        SetupCamera(eye, sub);
    }

    // sub: frustumun oturduğu cam parçası (yerel metre).
    private void SetupCamera(Vector3 eye, Rect sub)
    {
        // Göz ayna düzleminin arkasına yansıtılır; kamera yüzeyle aynı yöne (odaya) bakar.
        reflectionCamera.transform.SetPositionAndRotation(
            transform.TransformPoint(new Vector3(eye.x, eye.y, -eye.z)), transform.rotation);

        // Yakın düzlem camın hemen arkasında: yüzeyin kendisi yansımaya girmesin.
        float distance = eye.z;
        float near = distance + 0.005f;
        float scale = near / distance;
        float left = (sub.xMin - eye.x) * scale;
        float right = (sub.xMax - eye.x) * scale;
        float bottom = (sub.yMin - eye.y) * scale;
        float top = (sub.yMax - eye.y) * scale;

        // LOD seçimi için yaklaşık görüş açısı; projeksiyon bundan sonra elle verilir.
        reflectionCamera.fieldOfView = 2f * Mathf.Atan(0.5f * (top - bottom) / near) * Mathf.Rad2Deg;
        reflectionCamera.aspect = (right - left) / (top - bottom);
        reflectionCamera.nearClipPlane = near;
        reflectionCamera.projectionMatrix = Matrix4x4.Frustum(left, right, bottom, top, near, farClip);
    }

    private bool IsVisibleTo(Camera cam)
    {
        GeometryUtility.CalculateFrustumPlanes(cam, viewerPlanes);
        return GeometryUtility.TestPlanesAABB(viewerPlanes, surfaceRenderer.bounds)
            && HasLineOfSight(cam.transform.position);
    }

    // Cam bir duvarın arkasında kalmışsa (yan kabinin aynası, kolonun öbür yüzü) çizilmez: gözden camın 3x3
    // noktasına ışın atılır, hepsi engele çarparsa görünmüyor sayılır. Ignore Raycast (NPC'ler) ve trigger'lar sayılmaz;
    // ışın oyuncunun kendi kapsülünün içinden başladığı için onu görmez. Camın son 15 cm'si bakılmaz: aynanın kendi
    // çerçevesinin/ayağının collider'ı camın önünü kapsayabilir (evdeki boy aynası).
    private bool HasLineOfSight(Vector3 eye)
    {
        for (int i = -1; i <= 1; i++)
        {
            for (int j = -1; j <= 1; j++)
            {
                Vector3 point = transform.TransformPoint(new Vector3(0.4f * width * i, 0.4f * height * j, 0f));
                Vector3 delta = point - eye;
                float distance = delta.magnitude - 0.15f;
                if (distance <= 0f || !Physics.Raycast(eye, delta.normalized, distance, ~(1 << 2), QueryTriggerInteraction.Ignore))
                    return true;
            }
        }
        return false;
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
