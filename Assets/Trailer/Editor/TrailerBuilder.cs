using System.Collections.Generic;
using System.IO;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using UnityEngine.UI;

/// <summary>
/// Fragman sahnelerini (Assets/Trailer/Scenes/Trailer_*.unity) çekim tablolarından kurar. Her fragman sahnesi kendi
/// oyun sahnesinin üstüne additive açılır; içinde kamera (CinemachineBrain), çekimler (CinemachineCamera +
/// TrailerShot), Timeline (Cinemachine Track), post-process Volume'u ve TrailerController vardır.
/// Çekim değiştirmek için aşağıdaki tabloyu düzenleyip menüden (Same > Trailer) yeniden kur.
/// </summary>
public static partial class TrailerBuilder
{
    private const string Root = "Assets/Trailer";

    private class Shot
    {
        public string name;
        public float duration;
        public Vector3 posA, posB, lookA, lookB;
        public Vector3? arc;
        public float fovA = 40f, fovB = 40f, rollA, rollB;
        public float aperture = 4f, focalLength = 60f, focus;
        public TrailerShot.Ease ease = TrailerShot.Ease.Smooth;
        public string noise;          // Cinemachine gürültü profili adı (boş = sabit kamera)
        public float noiseAmplitude = 1f, noiseFrequency = 1f;
        public bool follow;           // konum/bakış oyuncunun köküne göre (kökün yatay dönüşüyle)
        public bool blonde;           // bakış en yakın sarışın NPC'nin kafasına kilitlenir
    }

    private class Spec
    {
        public string name;           // Trailer_<name>
        public string gameScene;
        public float timeOffset;      // fragmanın kaçıncı saniyesi
        public float preRoll = 2f;
        public int dayIndex;
        public ProductType[] owned = new ProductType[0];
        public bool blonde, surgery;
        public int population;
        public TrailerController.PlayerMode playerMode = TrailerController.PlayerMode.Hidden;
        public List<Shot> shots = new List<Shot>();
        public System.Action<VolumeProfile> post;
        public AnimationCurve chromatic, motionBlur, distortion, vignette;
        public bool logo, logoTypewriter;
        public float logoTime, logoFade = 1.2f;
        public AnimationCurve blackout;
        public List<TrailerAction> actions = new List<TrailerAction>();
        public System.Action<GameObject> extras;   // sahneye özel ek objeler (kök altında)

        public void Act(float time, TrailerAction.Kind kind, Vector3 point = default, float yaw = 0f,
            float duration = 1f, string target = null, int index = 0, float speed = 1.4f)
        {
            actions.Add(new TrailerAction
            {
                time = time, kind = kind, point = point, yaw = yaw, duration = duration, target = target,
                index = index, speed = speed
            });
        }
    }

    // Kısa siyah flaş eğrisi: [t, t + length] arası tam siyah, kenarlar sert.
    private static AnimationCurve Flashes(float end, params float[] startLength)
    {
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(new Keyframe(0f, 0f));
        for (int i = 0; i + 1 < startLength.Length; i += 2)
        {
            float a = startLength[i], b = a + startLength[i + 1];
            curve.AddKey(new Keyframe(a - 0.001f, 0f));
            curve.AddKey(new Keyframe(a, 1f));
            curve.AddKey(new Keyframe(b, 1f));
            curve.AddKey(new Keyframe(b + 0.001f, 0f));
        }
        curve.AddKey(new Keyframe(end, 0f));
        for (int i = 0; i < curve.length; i++)
        {
            UnityEditor.AnimationUtility.SetKeyLeftTangentMode(curve, i, UnityEditor.AnimationUtility.TangentMode.Constant);
            UnityEditor.AnimationUtility.SetKeyRightTangentMode(curve, i, UnityEditor.AnimationUtility.TangentMode.Constant);
        }
        return curve;
    }

    private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

    // ------------------------------------------------------------------ Faz 1: mağaza

    [MenuItem("Same/Trailer/Kur: Faz 1 Magaza")]
    public static void BuildShop()
    {
        Spec s = new Spec { name = "Faz1_Magaza", gameScene = "Assets/Scenes/ShopScene.unity", timeOffset = 0f, preRoll = 3f };
        // Açılış: ana koridorda ağır ilerleyen geniş plan, mağazanın tamamı.
        s.shots.Add(new Shot
        {
            name = "01_Koridor", duration = 3.5f,
            posA = V(0f, 1.45f, 2.6f), posB = V(0f, 1.45f, 5.0f),
            lookA = V(0f, 1.3f, 25f), lookB = V(0f, 1.3f, 25f),
            fovA = 38f, fovB = 38f, aperture = 8f, focalLength = 35f, focus = 12f, ease = TrailerShot.Ease.Linear
        });
        // Paltolar: askıdaki sıranın yanından paralel ray. Sığ netlik.
        s.shots.Add(new Shot
        {
            name = "02_Paltolar", duration = 4.5f,
            posA = V(-9.7f, 1.25f, 11.5f), posB = V(-7.2f, 1.25f, 11.5f),
            lookA = V(-9.4f, 1.1f, 13.0f), lookB = V(-7.5f, 1.1f, 13.0f),
            fovA = 30f, fovB = 30f, aperture = 2.0f, focalLength = 85f, ease = TrailerShot.Ease.Linear
        });
        // Gözlükler: tezgâhın üstünde alçak, yana kayan yakın çekim (gözlükler müşteriye, -z'ye bakar).
        s.shots.Add(new Shot
        {
            name = "03_Gozlukler", duration = 3.5f,
            posA = V(-5.55f, 1.0f, 4.55f), posB = V(-4.15f, 1.0f, 4.55f),
            lookA = V(-5.3f, 0.77f, 5.39f), lookB = V(-3.9f, 0.77f, 5.39f),
            fovA = 26f, fovB = 26f, aperture = 5.6f, focalLength = 60f, ease = TrailerShot.Ease.Linear
        });
        // Kasiyerler: tek tip iki çalışan, simetrik kadraj, yavaş yaklaşma (6 m içinde kameraya bakarlar).
        s.shots.Add(new Shot
        {
            name = "04_Kasiyerler", duration = 3.5f,
            posA = V(6.6f, 1.5f, 7.5f), posB = V(6.6f, 1.5f, 7.05f),
            lookA = V(6.6f, 1.4f, 4.1f), lookB = V(6.6f, 1.4f, 4.1f),
            fovA = 48f, fovB = 48f, aperture = 5.6f, focalLength = 50f, ease = TrailerShot.Ease.EaseOut
        });
        s.post = profile =>
        {
            // Steril lüks: temiz, hafif soğuk-nötr, yumuşak parlama, belirgin netlik derinliği.
            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.15f);
            color.contrast.Override(8f);
            color.saturation.Override(-8f);
            WhiteBalance wb = profile.Add<WhiteBalance>(true);
            wb.temperature.Override(-6f);
            Bloom bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.45f);
            bloom.scatter.Override(0.75f);
        };
        s.vignette = AnimationCurve.Constant(0f, 15f, 0.22f);
        Build(s);
    }

    // ------------------------------------------------------------------ Faz 2: sokak

    private static readonly ProductType[] AllProducts =
        { ProductType.Palto, ProductType.Pantolon, ProductType.Ayakkabi, ProductType.Canta, ProductType.Gozluk };

    private static void StreetPost(VolumeProfile profile)
    {
        ColorAdjustments color = profile.Add<ColorAdjustments>(true);
        color.contrast.Override(12f);
        FilmGrain grain = profile.Add<FilmGrain>(true);
        grain.intensity.Override(0.25f);
    }

    /// <summary>Faz 2'nin ilk yarısı: 2. gün, sokak daha çeşitli. Omuz kamerası.</summary>
    [MenuItem("Same/Trailer/Kur: Faz 2a Sokak")]
    public static void BuildStreetA()
    {
        Spec s = new Spec
        {
            name = "Faz2a_Sokak", gameScene = "Assets/Scenes/StreetScene.unity", timeOffset = 15f, preRoll = 6f,
            dayIndex = 1, owned = new[] { ProductType.Palto }, population = 80
        };
        s.shots.Add(new Shot
        {
            name = "01_Kalabalik", duration = 3.0f,
            posA = V(7.2f, 1.6f, 39.5f), posB = V(7.2f, 1.6f, 43.7f),
            lookA = V(6.6f, 1.5f, 80f), lookB = V(6.6f, 1.5f, 84f),
            fovA = 50f, fovB = 50f, aperture = 8f, focalLength = 28f, focus = 6f, ease = TrailerShot.Ease.Linear,
            noise = "Handheld_normal_strong", noiseAmplitude = 1.0f, noiseFrequency = 1.2f
        });
        s.shots.Add(new Shot
        {
            name = "02_Simit", duration = 2.2f,
            posA = V(-2.6f, 1.45f, 67.4f), posB = V(-2.9f, 1.45f, 69.4f),
            lookA = V(-5.4f, 1.2f, 70f), lookB = V(-5.4f, 1.2f, 70f),
            fovA = 42f, fovB = 42f, aperture = 4f, focalLength = 45f, ease = TrailerShot.Ease.Linear,
            noise = "Handheld_normal_mild", noiseAmplitude = 1.2f, noiseFrequency = 1.2f
        });
        s.shots.Add(new Shot
        {
            name = "03_Calgici", duration = 2.3f,
            posA = V(-4.2f, 1.4f, 75.8f), posB = V(-4.5f, 1.35f, 75.2f),
            lookA = V(-6.7f, 1.15f, 74f), lookB = V(-6.7f, 1.15f, 74f),
            fovA = 34f, fovB = 34f, aperture = 2.8f, focalLength = 60f, ease = TrailerShot.Ease.Linear,
            noise = "Handheld_tele_mild", noiseAmplitude = 1.0f, noiseFrequency = 1.0f
        });
        s.post = StreetPost;
        Build(s);
    }

    /// <summary>Faz 2'nin ikinci yarısı: herkes mağazadaki kıyafetlerin aynısını giymiş (asimilasyon).</summary>
    [MenuItem("Same/Trailer/Kur: Faz 2b Sokak")]
    public static void BuildStreetB()
    {
        Spec s = new Spec
        {
            name = "Faz2b_Sokak", gameScene = "Assets/Scenes/StreetScene.unity", timeOffset = 22.5f, preRoll = 6f,
            dayIndex = 4, owned = AllProducts, blonde = true, surgery = true, population = 80
        };
        s.shots.Add(new Shot
        {
            name = "01_Panolar", duration = 2.3f,
            posA = V(-1.5f, 1.5f, 69.5f), posB = V(-1.5f, 1.5f, 71.5f),
            lookA = V(-8.9f, 9.0f, 77f), lookB = V(-8.9f, 10.2f, 77f),
            fovA = 40f, fovB = 40f, aperture = 8f, focalLength = 35f, focus = 10f, ease = TrailerShot.Ease.Linear,
            noise = "Handheld_normal_mild", noiseAmplitude = 1.2f, noiseFrequency = 1.2f
        });
        s.shots.Add(new Shot
        {
            name = "02_Tek_Tip", duration = 2.6f,
            posA = V(0f, 1.5f, 48f), posB = V(0f, 1.5f, 49f),
            lookA = V(0f, 1.4f, 80f), lookB = V(0f, 1.4f, 80f),
            fovA = 18f, fovB = 18f, aperture = 5.6f, focalLength = 135f, focus = 14f, ease = TrailerShot.Ease.Linear,
            noise = "Handheld_tele_mild", noiseAmplitude = 0.8f, noiseFrequency = 1.0f
        });
        s.shots.Add(new Shot
        {
            name = "03_Kalabalik", duration = 2.6f,
            posA = V(7.2f, 1.6f, 39.5f), posB = V(7.2f, 1.6f, 43.1f),
            lookA = V(6.6f, 1.5f, 80f), lookB = V(6.6f, 1.5f, 83.6f),
            fovA = 50f, fovB = 50f, aperture = 8f, focalLength = 28f, focus = 6f, ease = TrailerShot.Ease.Linear,
            noise = "Handheld_normal_strong", noiseAmplitude = 1.0f, noiseFrequency = 1.2f
        });
        s.post = StreetPost;
        Build(s);
    }

    // ------------------------------------------------------------------ Faz 3: tüketim çılgınlığı (hızlı kesmeler)

    // Hızlı kesme: kısa süre, daralan açı (fovA > fovB), eğik kadraj ve sert omuz kamerası.
    private static Shot Cut(string name, float duration, Vector3 posA, Vector3 posB, Vector3 lookA, Vector3 lookB,
        float fovA, float fovB, float roll = 0f, float aperture = 4f, float noise = 1.4f)
    {
        return new Shot
        {
            name = name, duration = duration, posA = posA, posB = posB, lookA = lookA, lookB = lookB,
            fovA = fovA, fovB = fovB, rollA = roll * 0.4f, rollB = roll, aperture = aperture, focalLength = 70f,
            ease = TrailerShot.Ease.EaseIn, noise = "Handheld_tele_strong", noiseAmplitude = noise * Mathf.Clamp(fovB / 30f, 0.2f, 1f), noiseFrequency = 1.6f
        };
    }

    private static void FrenzyPost(VolumeProfile profile)
    {
        ColorAdjustments color = profile.Add<ColorAdjustments>(true);
        color.contrast.Override(22f);
        color.saturation.Override(14f);
        FilmGrain grain = profile.Add<FilmGrain>(true);
        grain.intensity.Override(0.45f);
        Bloom bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.9f);
        bloom.intensity.Override(0.6f);
    }

    /// <summary>Faz 3'ün ilk yarısı: mağazada ürünler, kasa, tek tip çalışanlar. Kesmeler giderek hızlanır.</summary>
    [MenuItem("Same/Trailer/Kur: Faz 3a Magaza")]
    public static void BuildFrenzyShop()
    {
        Spec s = new Spec { name = "Faz3a_Magaza", gameScene = "Assets/Scenes/ShopScene.unity", timeOffset = 30f, preRoll = 3f };
        s.shots.Add(Cut("01_Kart_Cihazi", 1.0f, V(8.38f, 1.5f, 5.9f), V(8.38f, 1.38f, 5.5f), V(8.38f, 1.07f, 4.9f), V(8.38f, 1.07f, 4.9f), 26f, 18f, 0f, 4f, 0.8f));
        s.shots.Add(Cut("02_Cantalar", 0.9f, V(-9.5f, 1.05f, 15.35f), V(-7.3f, 1.05f, 15.35f), V(-9.1f, 0.86f, 16.5f), V(-7.7f, 0.86f, 16.5f), 28f, 24f, 3f, 2.8f, 0.9f));
        s.shots.Add(Cut("03_Ayakkabilar", 0.9f, V(5.4f, 0.98f, 22.85f), V(3.4f, 0.98f, 22.85f), V(5.0f, 0.72f, 24f), V(3.8f, 0.72f, 24f), 26f, 22f, -3f, 2.8f, 0.9f));
        s.shots.Add(Cut("04_Kasiyer_Kadin", 0.8f, V(8.47f, 1.6f, 5.7f), V(8.47f, 1.6f, 5.25f), V(8.47f, 1.57f, 4.1f), V(8.47f, 1.57f, 4.1f), 20f, 15f, 4f, 2.8f, 1.0f));
        s.shots.Add(Cut("05_Paltolar", 0.7f, V(-7.0f, 1.3f, 11.7f), V(-9.6f, 1.3f, 11.7f), V(-7.4f, 1.15f, 13f), V(-9.3f, 1.15f, 13f), 24f, 20f, -5f, 2.0f, 1.2f));
        s.shots.Add(Cut("06_Pantolonlar", 0.7f, V(3.4f, 1.55f, 15.5f), V(5.3f, 1.55f, 15.5f), V(3.7f, 0.8f, 16.5f), V(5.1f, 0.8f, 16.5f), 26f, 20f, 6f, 2.8f, 1.2f));
        s.shots.Add(Cut("07_Kasiyer_Erkek", 0.7f, V(4.73f, 1.7f, 5.5f), V(4.73f, 1.7f, 5.1f), V(4.73f, 1.68f, 4.1f), V(4.73f, 1.68f, 4.1f), 18f, 13f, -6f, 2.8f, 1.3f));
        s.shots.Add(Cut("08_Kart_Cihazi_Yakin", 0.6f, V(8.42f, 1.75f, 5.3f), V(8.4f, 1.6f, 5.2f), V(8.38f, 1.07f, 4.9f), V(8.38f, 1.07f, 4.9f), 18f, 11f, 8f, 11f, 1.5f));
        s.shots.Add(Cut("09_Mankenler", 0.6f, V(0f, 1.45f, 21.6f), V(0f, 1.45f, 22.9f), V(0f, 1.5f, 24.5f), V(0f, 1.5f, 24.5f), 30f, 17f, -8f, 4f, 1.5f));
        s.shots.Add(Cut("10_Gozluk", 0.6f, V(-4.75f, 0.95f, 4.7f), V(-4.45f, 0.93f, 4.8f), V(-4.35f, 0.77f, 5.39f), V(-4.35f, 0.77f, 5.39f), 18f, 12f, 9f, 5.6f, 1.6f));
        s.post = FrenzyPost;
        s.chromatic = AnimationCurve.Linear(0f, 0.15f, 7.5f, 0.55f);
        s.motionBlur = AnimationCurve.Linear(0f, 0.3f, 7.5f, 0.65f);
        s.distortion = AnimationCurve.Linear(0f, 0f, 7.5f, -0.25f);
        s.vignette = AnimationCurve.Linear(0f, 0.25f, 7.5f, 0.4f);
        Build(s);
    }

    /// <summary>Faz 3'ün ikinci yarısı: sokakta üstüne gelen panolar ve tek tip kalabalık. En dar açılar, en hızlı kesmeler.</summary>
    [MenuItem("Same/Trailer/Kur: Faz 3b Sokak")]
    public static void BuildFrenzyStreet()
    {
        Spec s = new Spec
        {
            name = "Faz3b_Sokak", gameScene = "Assets/Scenes/StreetScene.unity", timeOffset = 37.5f, preRoll = 6f,
            dayIndex = 4, owned = AllProducts, blonde = true, surgery = true, population = 90
        };
        s.shots.Add(Cut("01_Pano_Sol", 0.8f, V(-1.5f, 1.5f, 71f), V(-1.9f, 1.5f, 71.6f), V(-8.9f, 10f, 77f), V(-8.9f, 10f, 77f), 30f, 17f, 6f, 8f, 1.4f));
        s.shots.Add(Cut("02_Tek_Tip", 0.8f, V(0f, 1.55f, 50f), V(0f, 1.55f, 50.6f), V(0f, 1.5f, 80f), V(0f, 1.5f, 80f), 12f, 9f, -4f, 5.6f, 1.2f));
        s.shots.Add(Cut("03_Pano_Sag", 0.6f, V(2f, 1.5f, 62f), V(2.4f, 1.5f, 62.6f), V(8.9f, 9.2f, 69f), V(8.9f, 9.2f, 69f), 26f, 15f, -9f, 8f, 1.5f));
        s.shots.Add(Cut("04_Cati_Panosu", 0.6f, V(2f, 1.5f, 96f), V(2.4f, 1.5f, 95.4f), V(14.6f, 24.4f, 73.4f), V(14.6f, 24.4f, 73.4f), 22f, 13f, 10f, 8f, 1.6f));
        s.shots.Add(Cut("05_Kaldirim", 0.6f, V(7.2f, 1.5f, 44f), V(7.2f, 1.5f, 45f), V(6.8f, 1.45f, 80f), V(6.8f, 1.45f, 80f), 30f, 22f, -6f, 4f, 1.8f));
        s.shots.Add(Cut("06_Ayakli_Pano", 0.5f, V(3.4f, 1.4f, 76f), V(4.5f, 1.4f, 76f), V(6.6f, 1.35f, 76f), V(6.6f, 1.35f, 76f), 28f, 18f, 11f, 4f, 1.8f));
        s.shots.Add(Cut("07_Kuafor", 0.5f, V(3f, 1.5f, 60.2f), V(3.5f, 1.5f, 60.5f), V(8.6f, 3.7f, 62f), V(8.6f, 3.7f, 62f), 22f, 13f, 9f, 5.6f, 1.9f));
        s.shots.Add(Cut("08_Klinik", 0.5f, V(-2.2f, 1.5f, 40f), V(-2.7f, 1.5f, 39.7f), V(-8.2f, 3.9f, 38f), V(-8.2f, 3.9f, 38f), 22f, 13f, -10f, 5.6f, 1.9f));
        s.shots.Add(Cut("09_Tek_Tip_2", 0.5f, V(-2.5f, 1.55f, 60f), V(-2.5f, 1.55f, 60.5f), V(-2.5f, 1.5f, 95f), V(-2.5f, 1.5f, 95f), 9f, 7f, 7f, 5.6f, 1.6f));
        s.shots.Add(Cut("10_Pano_Sol_Yakin", 0.5f, V(-1.9f, 1.5f, 71.6f), V(-2.2f, 1.5f, 72f), V(-8.9f, 10f, 77f), V(-8.9f, 10f, 77f), 12f, 7.5f, 13f, 8f, 2.0f));
        s.shots.Add(Cut("11_Tek_Tip_3", 0.5f, V(0.5f, 1.55f, 72f), V(0.5f, 1.55f, 71.5f), V(0.5f, 1.5f, 40f), V(0.5f, 1.5f, 40f), 10f, 7f, -12f, 5.6f, 2.0f));
        s.shots.Add(Cut("12_Pano_Son", 1.1f, V(0f, 1.5f, 46.5f), V(-0.8f, 1.5f, 47.2f), V(-8.9f, 10.1f, 51f), V(-8.9f, 10.1f, 51f), 16f, 5.5f, -16f, 8f, 2.2f));
        s.post = FrenzyPost;
        s.chromatic = AnimationCurve.Linear(0f, 0.55f, 7.5f, 1f);
        s.motionBlur = AnimationCurve.Linear(0f, 0.65f, 7.5f, 1f);
        s.distortion = AnimationCurve.Linear(0f, -0.25f, 7.5f, -0.55f);
        s.vignette = AnimationCurve.Linear(0f, 0.4f, 7.5f, 0.58f);
        Build(s);
    }

    // ------------------------------------------------------------------ Faz 4: ev (kapanış)

    /// <summary>Kapanış: karakter kanepede, etrafı satın aldıklarıyla dolu, TV'ye bakıyor. Sabit geniş plan + SAME logosu.</summary>
    [MenuItem("Same/Trailer/Kur: Faz 4 Ev")]
    public static void BuildHouse()
    {
        Spec s = new Spec
        {
            name = "Faz4_Ev", gameScene = "Assets/Scenes/HouseScene.unity", timeOffset = 45f, preRoll = 5f,
            dayIndex = 4, owned = AllProducts, blonde = true, surgery = true,
            playerMode = TrailerController.PlayerMode.SeatedOnCouch,
            logo = true, logoTime = 1.6f, logoFade = 1.2f
        };
        s.shots.Add(new Shot
        {
            name = "01_Kanepe", duration = 5f,
            posA = V(-3.68f, 2.62f, 3.5f), posB = V(-3.68f, 2.62f, 3.5f),
            lookA = V(-3.68f, 2.08f, 1.0f), lookB = V(-3.68f, 2.08f, 1.0f),
            fovA = 58f, fovB = 58f, aperture = 8f, focalLength = 24f, focus = 2.4f, ease = TrailerShot.Ease.Linear
        });
        s.post = profile =>
        {
            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.6f);
            color.contrast.Override(10f);
            color.saturation.Override(-12f);
            FilmGrain grain = profile.Add<FilmGrain>(true);
            grain.intensity.Override(0.3f);
        };
        s.vignette = AnimationCurve.Constant(0f, 5f, 0.34f);
        s.extras = root =>
        {
            BuildPiles(root);
            // TV'nin ışığı tek başına çekim için zayıf: ekranın yerinden kanepeye soğuk, yumuşak dolgu.
            GameObject fill = new GameObject("TV_Dolgu_Isigi");
            fill.transform.SetParent(root.transform, false);
            fill.transform.position = V(-3.5f, 2.45f, 3.3f);
            fill.transform.rotation = Quaternion.LookRotation(V(-3.7f, 1.9f, 1.0f) - fill.transform.position);
            Light light = fill.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = 120f;
            light.innerSpotAngle = 60f;
            light.range = 7f;
            light.intensity = 7f;
            light.color = new Color(0.72f, 0.84f, 1f);
            light.shadows = LightShadows.Soft;
        };
        Build(s);
    }

    // Kanepenin çevresine satın alınan eşyalardan yığınlar: editörde fizikle düşürülür, son pozlar sahnede sabit kalır.
    private static void BuildPiles(GameObject root)
    {
        // Görsel kaynak: dolaptaki ürün modelleri (mesh + malzemeler).
        string[] sources = { "Palto_Askida", "Pantolon_Askida", "Canta_Dolapta", "Ayakkabi_Dolapta", "Gozluk_Dolapta" };
        int[] weights = { 12, 10, 10, 8, 6 };
        List<MeshFilter> filters = new List<MeshFilter>();
        foreach (string name in sources)
        {
            MeshFilter found = null;
            foreach (WardrobeClothing clothing in Object.FindObjectsByType<WardrobeClothing>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (clothing.name != name)
                    continue;
                foreach (MeshFilter filter in clothing.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.name != "Askilik" && (found == null || filter.sharedMesh.vertexCount > found.sharedMesh.vertexCount))
                        found = filter;
                }
            }
            if (found == null)
            {
                Debug.LogWarning("Trailer: yığın için model bulunamadı: " + name);
                return;
            }
            filters.Add(found);
        }

        GameObject piles = new GameObject("Esya_Yiginlari");
        piles.transform.SetParent(root.transform, false);

        // Oturan karakterin yeri boş kalsın (sol koltuk, x -3.83).
        GameObject blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        // Tavana kadar uzanır: üstüne düşen parça kalmasın (engel silinince havada asılı kalıyordu).
        blocker.transform.position = V(-3.83f, 1.36f + 1.6f, 1.35f);
        blocker.transform.localScale = V(0.66f, 3.2f, 1.5f);

        // Bölgeler: x aralığı, z aralığı, adet.
        float[,] zones =
        {
            { -3.35f, -2.80f, 0.75f, 1.35f, 12 },    // kanepenin boş koltuğu
            { -2.55f, -1.40f, 0.20f, 2.20f, 34 },   // kanepenin sağı
            { -5.20f, -4.45f, 0.40f, 2.20f, 30 },   // kanepenin solu (yan sehpa)
            { -4.90f, -2.20f, 1.80f, 2.45f, 14 },   // sehpa ve önü
            { -4.40f, -2.60f, 0.10f, 0.50f, 12 },    // sırtlığın üstü/arkası
        };

        SimulationMode previousMode = Physics.simulationMode;
        Physics.simulationMode = SimulationMode.Script;
        System.Random random = new System.Random(4711);
        System.Func<float, float, float> range = (a, b) => a + (float)random.NextDouble() * (b - a);
        List<Rigidbody> bodies = new List<Rigidbody>();
        int total = 0;
        foreach (int w in weights)
            total += w;

        int count = 0;
        for (int z = 0; z < zones.GetLength(0); z++)
        {
            for (int i = 0; i < (int)zones[z, 4]; i++)
            {
                int pick = random.Next(total), kind = 0;
                while (pick >= weights[kind])
                {
                    pick -= weights[kind];
                    kind++;
                }
                MeshFilter source = filters[kind];
                GameObject item = new GameObject(sources[kind].Split('_')[0] + "_" + count++);
                item.transform.SetParent(piles.transform, false);
                item.transform.position = V(range(zones[z, 0], zones[z, 1]), range(2.3f, 2.9f), range(zones[z, 2], zones[z, 3]));
                // Uzun kıyafetler yatık atılmış gibi başlar.
                item.transform.rotation = Quaternion.Euler(kind <= 1 ? range(70f, 110f) : range(0f, 360f), range(0f, 360f), range(-25f, 25f));
                item.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                MeshRenderer renderer = item.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;
                MeshCollider collider = item.AddComponent<MeshCollider>();
                collider.sharedMesh = source.sharedMesh;
                collider.convex = true;
                Rigidbody body = item.AddComponent<Rigidbody>();
                body.mass = 0.5f;
                body.linearDamping = 0.4f;
                body.angularDamping = 0.8f;
                bodies.Add(body);

                Physics.SyncTransforms();
                for (int step = 0; step < 30; step++)
                    Physics.Simulate(0.02f);
            }
        }
        for (int step = 0; step < 200; step++)
            Physics.Simulate(0.02f);

        foreach (Rigidbody body in bodies)
        {
            GameObject item = body.gameObject;
            Object.DestroyImmediate(body);
            Object.DestroyImmediate(item.GetComponent<MeshCollider>());
            // Zeminin altına düşen ya da odadan çıkan parça olmasın.
            Vector3 p = item.transform.position;
            if (p.y < 1.2f || p.x < -6.9f || p.x > -0.2f || p.z < -1.7f || p.z > 3.8f)
                Object.DestroyImmediate(item);
        }
        Object.DestroyImmediate(blocker);
        Physics.simulationMode = previousMode;
    }

    // ------------------------------------------------------------------ ortak kurulum

    private static void Build(Spec spec)
    {
        Directory.CreateDirectory(Root + "/Scenes");
        Directory.CreateDirectory(Root + "/Timelines");
        Directory.CreateDirectory(Root + "/Profiles");
        AssetDatabase.Refresh();

        // Oyun sahnesi tek başına açılır, fragman sahnesi üstüne yeni kurulur. Oyun sahnesi KAYDEDİLMEZ.
        EditorSceneManager.OpenScene(spec.gameScene, OpenSceneMode.Single);
        Scene rig = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(rig);

        GameObject root = new GameObject("Trailer_" + spec.name);

        // Kamera
        GameObject camObject = new GameObject("Trailer_Kamera");
        camObject.transform.SetParent(root.transform, false);
        Camera camera = camObject.AddComponent<Camera>();
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 1000f;
        camera.allowHDR = true;
        camera.allowMSAA = true;
        camObject.AddComponent<AudioListener>();
        UniversalAdditionalCameraData data = camObject.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        data.renderShadows = true;
        CinemachineBrain brain = camObject.AddComponent<CinemachineBrain>();
        brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
        brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;

        // Post-process profili
        string profilePath = Root + "/Profiles/Trailer_" + spec.name + ".asset";
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
        }
        for (int i = profile.components.Count - 1; i >= 0; i--)
        {
            VolumeComponent old = profile.components[i];
            profile.components.RemoveAt(i);
            Object.DestroyImmediate(old, true);
        }
        DepthOfField dof = profile.Add<DepthOfField>(true);
        dof.mode.Override(DepthOfFieldMode.Bokeh);
        dof.focusDistance.Override(3f);
        dof.aperture.Override(4f);
        dof.focalLength.Override(60f);
        dof.bladeCount.Override(7);
        profile.Add<ChromaticAberration>(true).intensity.Override(0f);
        MotionBlur blur = profile.Add<MotionBlur>(true);
        blur.intensity.Override(0f);
        blur.quality.Override(MotionBlurQuality.High);
        profile.Add<LensDistortion>(true).intensity.Override(0f);
        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.2f);
        vignette.smoothness.Override(0.45f);
        spec.post?.Invoke(profile);
        foreach (VolumeComponent component in profile.components)
        {
            if (!AssetDatabase.Contains(component))
                AssetDatabase.AddObjectToAsset(component, profile);
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        }
        EditorUtility.SetDirty(profile);

        GameObject volumeObject = new GameObject("Trailer_Volume");
        volumeObject.transform.SetParent(root.transform, false);
        Volume volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 50f;
        volume.sharedProfile = profile;

        // Timeline
        string timelinePath = Root + "/Timelines/Trailer_" + spec.name + ".playable";
        TimelineAsset timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath);
        if (timeline == null)
        {
            timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(timeline, timelinePath);
        }
        foreach (TrackAsset oldTrack in new List<TrackAsset>(timeline.GetRootTracks()))
            timeline.DeleteTrack(oldTrack);
        timeline.editorSettings.frameRate = TrailerRecorderTool.FrameRate;
        timeline.durationMode = TimelineAsset.DurationMode.BasedOnClips;

        PlayableDirector director = root.AddComponent<PlayableDirector>();
        director.playableAsset = timeline;
        director.playOnAwake = false;
        director.extrapolationMode = DirectorWrapMode.Hold;

        CinemachineTrack track = timeline.CreateTrack<CinemachineTrack>(null, "Kameralar");
        director.SetGenericBinding(track, brain);

        GameObject shotsRoot = new GameObject("Cekimler");
        shotsRoot.transform.SetParent(root.transform, false);
        float time = 0f;
        foreach (Shot shot in spec.shots)
        {
            GameObject go = new GameObject("Cekim_" + shot.name);
            go.transform.SetParent(shotsRoot.transform, false);
            CinemachineCamera vcam = go.AddComponent<CinemachineCamera>();
            LensSettings lens = LensSettings.Default;
            lens.FieldOfView = shot.fovA;
            lens.NearClipPlane = 0.05f;
            lens.FarClipPlane = 1000f;
            vcam.Lens = lens;
            vcam.Priority = 0;

            TrailerShot ts = go.AddComponent<TrailerShot>();
            ts.start = time;
            ts.duration = shot.duration;
            ts.positionA = shot.posA;
            ts.positionB = shot.posB;
            ts.useArc = shot.arc.HasValue;
            ts.arcPoint = shot.arc ?? Vector3.zero;
            ts.lookA = shot.lookA;
            ts.lookB = shot.lookB;
            ts.rollA = shot.rollA;
            ts.rollB = shot.rollB;
            ts.fovA = shot.fovA;
            ts.fovB = shot.fovB;
            ts.aperture = shot.aperture;
            ts.focalLength = shot.focalLength;
            ts.focusDistance = shot.focus;
            ts.ease = shot.ease;
            ts.followPlayer = shot.follow;
            ts.lookAtBlonde = shot.blonde;
            ts.Apply(time);

            if (!string.IsNullOrEmpty(shot.noise))
            {
                NoiseSettings noise = AssetDatabase.LoadAssetAtPath<NoiseSettings>(
                    "Packages/com.unity.cinemachine/Presets/Noise/" + shot.noise + ".asset");
                CinemachineBasicMultiChannelPerlin perlin = go.AddComponent<CinemachineBasicMultiChannelPerlin>();
                perlin.NoiseProfile = noise;
                perlin.AmplitudeGain = shot.noiseAmplitude;
                perlin.FrequencyGain = shot.noiseFrequency;
            }

            TimelineClip clip = track.CreateClip<CinemachineShot>();
            clip.start = time;
            clip.duration = shot.duration;
            clip.displayName = shot.name;
            CinemachineShot asset = (CinemachineShot)clip.asset;
            asset.DisplayName = shot.name;
            asset.VirtualCamera.exposedName = GUID.Generate().ToString();
            director.SetReferenceValue(asset.VirtualCamera.exposedName, vcam);
            EditorUtility.SetDirty(asset);
            time += shot.duration;
        }
        EditorUtility.SetDirty(timeline);

        // Ses kaynakları (klipler sonradan bağlanacak)
        AudioSource elevator = NewAudio(root, "Ses_Asansor_Muzigi", true);
        AudioSource house = NewAudio(root, "Ses_House_Muzigi", true);
        house.gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = 22000f;
        AudioSource receipt = NewAudio(root, "Ses_Yazar_Kasa_Fisi", false);

        CanvasGroup logo = spec.logo ? BuildLogo(root) : null;
        CanvasGroup black = spec.blackout != null ? BuildBlack(root) : null;
        TrailerActor actor = null;
        if (spec.playerMode == TrailerController.PlayerMode.Actor)
        {
            actor = root.AddComponent<TrailerActor>();
            actor.SetActions(spec.actions);
            EditorUtility.SetDirty(actor);
        }

        TrailerController controller = root.AddComponent<TrailerController>();
        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("director").objectReferenceValue = director;
        so.FindProperty("preRoll").floatValue = spec.preRoll;
        so.FindProperty("trailerTimeOffset").floatValue = spec.timeOffset;
        so.FindProperty("trailerCamera").objectReferenceValue = camera;
        so.FindProperty("dayIndex").intValue = spec.dayIndex;
        SerializedProperty owned = so.FindProperty("ownedProducts");
        owned.arraySize = spec.owned.Length;
        for (int i = 0; i < spec.owned.Length; i++)
            owned.GetArrayElementAtIndex(i).enumValueIndex = (int)spec.owned[i];
        so.FindProperty("hairDyed").boolValue = spec.blonde;
        so.FindProperty("surgeryDone").boolValue = spec.surgery;
        so.FindProperty("playerMode").enumValueIndex = (int)spec.playerMode;
        so.FindProperty("crowdPopulation").intValue = spec.population;
        so.FindProperty("elevatorMusic").objectReferenceValue = elevator;
        so.FindProperty("houseMusic").objectReferenceValue = house;
        so.FindProperty("receiptSound").objectReferenceValue = receipt;
        so.FindProperty("volume").objectReferenceValue = volume;
        so.FindProperty("chromaticAberration").animationCurveValue = spec.chromatic ?? AnimationCurve.Constant(0f, time, 0f);
        so.FindProperty("motionBlur").animationCurveValue = spec.motionBlur ?? AnimationCurve.Constant(0f, time, 0f);
        so.FindProperty("lensDistortion").animationCurveValue = spec.distortion ?? AnimationCurve.Constant(0f, time, 0f);
        so.FindProperty("vignette").animationCurveValue = spec.vignette ?? AnimationCurve.Constant(0f, time, 0.2f);
        so.FindProperty("logo").objectReferenceValue = logo;
        so.FindProperty("logoTime").floatValue = spec.logoTime;
        so.FindProperty("logoFade").floatValue = spec.logoFade;
        so.FindProperty("logoTypewriter").boolValue = spec.logoTypewriter;
        so.FindProperty("blackScreen").objectReferenceValue = black;
        if (spec.blackout != null)
            so.FindProperty("blackout").animationCurveValue = spec.blackout;
        so.FindProperty("actor").objectReferenceValue = actor;
        so.ApplyModifiedPropertiesWithoutUndo();

        spec.extras?.Invoke(root);

        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(rig, Root + "/Scenes/Trailer_" + spec.name + ".unity");
        // Işık ve gökyüzü oyun sahnesinden gelsin.
        SceneManager.SetActiveScene(SceneManager.GetSceneByPath(spec.gameScene));
        Debug.Log("Trailer kuruldu: " + spec.name + " (" + time.ToString("F1") + " sn, " + spec.shots.Count + " çekim)");
    }

    /// <summary>Oyun sahnesini ve fragman sahnesini çekime hazır açar (Play'e basmaya hazır).</summary>
    public static void Open(string name, string gameScene)
    {
        EditorSceneManager.OpenScene(gameScene, OpenSceneMode.Single);
        EditorSceneManager.OpenScene(Root + "/Scenes/Trailer_" + name + ".unity", OpenSceneMode.Additive);
        SceneManager.SetActiveScene(SceneManager.GetSceneByPath(gameScene));
    }

    private static AudioSource NewAudio(GameObject root, string name, bool loop)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        return source;
    }

    private static CanvasGroup BuildBlack(GameObject root)
    {
        GameObject canvasObject = new GameObject("Karartma_Canvas");
        canvasObject.transform.SetParent(root.transform, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        CanvasGroup group = canvasObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        GameObject imageObject = new GameObject("Siyah", typeof(RectTransform));
        imageObject.transform.SetParent(canvasObject.transform, false);
        Image image = imageObject.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return group;
    }

    private static CanvasGroup BuildLogo(GameObject root)
    {
        GameObject canvasObject = new GameObject("Logo_Canvas");
        canvasObject.transform.SetParent(root.transform, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        CanvasGroup group = canvasObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        GameObject textObject = new GameObject("SAME");
        textObject.transform.SetParent(canvasObject.transform, false);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.font = Resources.Load<TMP_FontAsset>("UI/Fontlar/IBMPlexMono-Regular SDF");
        text.text = "SAME";
        text.fontSize = 210f;
        text.characterSpacing = 38f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return group;
    }
}
