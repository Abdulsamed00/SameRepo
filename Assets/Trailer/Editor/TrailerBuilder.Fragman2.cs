using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// İkinci fragman (60 sn, 5 faz, sessiz): Eksiklik → Tüketim → Dönüşüm → Kaos → Asimilasyon.
/// Sahneler Trailer_T2_*. Her mekân tek işte görünür: mağaza sadece Faz 2'de mekân olarak, Faz 4'te sadece ürün
/// yakın çekimleri (Faz 2'deki palto/kasa yok); sokak yürüyüşü sadece Faz 2'de.
/// Zaman çizgisi: Faz1 0-14, Faz2 14-27, Faz3 27-38, Faz4 38-46, Faz5 46-60.
/// </summary>
public static partial class TrailerBuilder
{
    private static TrailerActor.Cue Cue(float time, TrailerActor.Act act, string target = null, Vector3 point = default, float value = 0f)
        => new TrailerActor.Cue { time = time, act = act, target = target, point = point, value = value };

    private static Shot Still(string name, float duration, Vector3 pos, Vector3 look, float fov, float aperture = 5.6f, float focus = 0f)
        => new Shot { name = name, duration = duration, posA = pos, posB = pos, lookA = look, lookB = look, fovA = fov, fovB = fov,
                      aperture = aperture, focalLength = 50f, focus = focus, ease = TrailerShot.Ease.Linear };

    // Kaos kesmesi: kamera nesneye doğru atılır (nesne üstüne gelir), açı daralır, kadraj yatar.
    private static Shot Rush(string name, float duration, Vector3 posA, Vector3 posB, Vector3 look, float fovA, float fovB, float roll)
        => new Shot
        {
            name = name, duration = duration, posA = posA, posB = posB, lookA = look, lookB = look,
            fovA = fovA, fovB = fovB, rollA = -roll * 0.3f, rollB = roll, aperture = 4f, focalLength = 70f,
            ease = TrailerShot.Ease.EaseIn, noise = "Handheld_tele_strong", noiseAmplitude = 0.9f, noiseFrequency = 2.2f
        };

    private static void ChaosPost(VolumeProfile profile)
    {
        ColorAdjustments color = profile.Add<ColorAdjustments>(true);
        color.contrast.Override(28f);
        color.saturation.Override(20f);
        color.postExposure.Override(0.2f);
        FilmGrain grain = profile.Add<FilmGrain>(true);
        grain.intensity.Override(0.5f);
        Bloom bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.85f);
        bloom.intensity.Override(0.8f);
    }

    // ------------------------------------------------------------------ Faz 4: kaos (38-46)

    [MenuItem("Same/Trailer 2/Kur: Faz 4a Magaza (kaos)")]
    public static void BuildT2ChaosShop()
    {
        Spec s = new Spec { name = "T2_Faz4a_Magaza", gameScene = "Assets/Scenes/ShopScene.unity", timeOffset = 38f, preRoll = 3f,
                            dayIndex = 4, owned = AllProducts };
        s.shots.Add(Rush("01_Cantalar", 0.7f, V(-8.3f, 1.35f, 13.9f), V(-8.1f, 1.08f, 15.55f), V(-8.1f, 0.86f, 16.4f), 32f, 16f, 6f));
        s.shots.Add(Rush("02_Ayakkabilar", 0.55f, V(4.4f, 1.1f, 21.4f), V(4.4f, 0.9f, 23.3f), V(4.4f, 0.7f, 24f), 30f, 12f, -9f));
        s.shots.Add(Rush("03_Gozlukler", 0.55f, V(-4.65f, 1.3f, 3.6f), V(-4.72f, 1.05f, 4.72f), V(-4.75f, 0.8f, 5.35f), 28f, 14f, 11f));
        s.shots.Add(Rush("04_Pantolonlar", 0.5f, V(4.4f, 1.7f, 14.3f), V(4.4f, 1.1f, 15.9f), V(4.4f, 0.8f, 16.5f), 28f, 11f, -13f));
        s.shots.Add(Rush("05_Mankenler", 0.65f, V(0f, 1.5f, 19.5f), V(0f, 1.62f, 22.8f), V(0f, 1.62f, 24.5f), 34f, 14f, 15f));
        s.shots.Add(Rush("06_Kart", 0.65f, V(8.4f, 1.7f, 6.5f), V(8.5f, 1.38f, 5.65f), V(8.53f, 1.08f, 4.95f), 26f, 11f, -18f));
        s.post = ChaosPost;
        s.chromatic = AnimationCurve.Linear(0f, 0.6f, 3.6f, 0.9f);
        s.motionBlur = AnimationCurve.Linear(0f, 0.7f, 3.6f, 0.95f);
        s.distortion = AnimationCurve.Linear(0f, -0.2f, 3.6f, -0.4f);
        s.vignette = AnimationCurve.Linear(0f, 0.35f, 3.6f, 0.5f);
        // Saniyelik kesmelerde araya tek karelik siyah flaşlar.
        s.blackout = Flashes(new[] { 0.7f, 1.8f, 2.95f }, 0.05f);
        Build(s);
    }

    [MenuItem("Same/Trailer 2/Kur: Faz 4b Sokak (kaos)")]
    public static void BuildT2ChaosStreet()
    {
        Spec s = new Spec
        {
            name = "T2_Faz4b_Sokak", gameScene = "Assets/Scenes/StreetScene.unity", timeOffset = 41.6f, preRoll = 6f,
            dayIndex = 4, owned = AllProducts, blonde = true, surgery = true, population = 90
        };
        s.shots.Add(Rush("01_Pano_Sol", 0.7f, V(-1.5f, 3.5f, 69.5f), V(-6.6f, 8.6f, 74.6f), V(-8.9f, 10f, 77f), 42f, 16f, 10f));
        s.shots.Add(Rush("02_Ayakli_Pano", 0.6f, V(2.2f, 1.45f, 76f), V(5.5f, 1.4f, 76f), V(6.6f, 1.35f, 76f), 36f, 13f, -12f));
        s.shots.Add(Rush("03_Pano_Sag_Guney", 0.7f, V(1.5f, 2.0f, 21.5f), V(6.2f, 9.4f, 25.8f), V(9.1f, 9.1f, 26.75f), 34f, 16f, 15f));
        s.shots.Add(Rush("04_Pano_Sag", 0.6f, V(2f, 1.6f, 62f), V(7f, 7.6f, 67.4f), V(8.9f, 9.2f, 69f), 36f, 11f, -16f));
        s.shots.Add(Rush("05_Ayni_Kalabalik", 0.7f, V(0f, 1.55f, 92f), V(0f, 1.55f, 87f), V(0f, 1.5f, 55f), 16f, 7f, 8f));
        s.shots.Add(Rush("06_Pano_Son", 1.1f, V(0f, 1.5f, 46.5f), V(-2.4f, 4.9f, 47.9f), V(-8.9f, 10.1f, 51f), 34f, 5f, -22f));
        s.post = ChaosPost;
        s.chromatic = AnimationCurve.Linear(0f, 0.9f, 4.4f, 1f);
        s.motionBlur = AnimationCurve.Linear(0f, 0.95f, 4.4f, 1f);
        s.distortion = AnimationCurve.Linear(0f, -0.4f, 4.4f, -0.65f);
        s.vignette = AnimationCurve.Linear(0f, 0.5f, 4.4f, 0.62f);
        s.blackout = Flashes(new[] { 0.7f, 2.0f, 2.6f }, 0.05f);
        Build(s);
    }

    // Verilen anlarda kısa (tek-iki kare) siyah flaşlar.
    private static AnimationCurve Flashes(float[] times, float width)
    {
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(new Keyframe(0f, 0f));
        foreach (float t in times)
        {
            curve.AddKey(new Keyframe(t - 0.001f, 0f));
            curve.AddKey(new Keyframe(t, 1f));
            curve.AddKey(new Keyframe(t + width, 1f));
            curve.AddKey(new Keyframe(t + width + 0.001f, 0f));
        }
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
        }
        return curve;
    }

    // ------------------------------------------------------------------ Faz 5: asimilasyon (46-60)

    /// <summary>
    /// Kaos bıçak gibi kesilir: tam tepeden, oyunun final kalabalığı (oyuncunun 40 kopyası + benzer kadınlar) karakterin
    /// çevresine yürüyerek toplanır, hep birlikte aynı yöne döner; kamera yavaşça yükselir, karakter kaybolur.
    /// Ekran kararır, daktiloyla "SAME".
    /// </summary>
    [MenuItem("Same/Trailer 2/Kur: Faz 5 Final")]
    public static void BuildT2Final()
    {
        Vector3 center = V(0f, 0f, 60f);
        Spec s = new Spec
        {
            name = "T2_Faz5_Final", gameScene = "Assets/Scenes/StreetScene.unity", timeOffset = 46f, preRoll = 5f,
            dayIndex = 4, owned = AllProducts, blonde = true, surgery = true,
            playerMode = TrailerController.PlayerMode.Acting,
            logo = true, typewriter = true, logoTime = 11.3f, logoFade = 0.17f
        };
        // Tepeden bakışı kesen bayrak dizileri kalkar; oyuncunun (kapalı) kamerası göğe bakar ki final kalabalığı
        // dışarıdakileri hemen silsin ve yeni gelenleri kameranın gördüğü yerde doğurabilsin.
        s.cues.Add(Cue(-5f, TrailerActor.Act.HideObject, "StreetBlockout/Bayrak_Dizileri"));
        s.cues.Add(Cue(-4.99f, TrailerActor.Act.Place, null, center, 0f));
        s.cues.Add(Cue(-4.98f, TrailerActor.Act.SkyCamera));
        s.cues.Add(Cue(-4.95f, TrailerActor.Act.CrowdSize, null, V(70f, 150f, 0f)));
        s.cues.Add(Cue(-4.9f, TrailerActor.Act.Finale, null, center));
        s.cues.Add(Cue(5.5f, TrailerActor.Act.Conform, null, V(0f, 0f, 1f)));

        s.shots.Add(new Shot
        {
            name = "01_Tepeden", duration = 11f,
            posA = V(0f, 4.2f, 60f), posB = V(1.2f, 25f, 61.0f),
            lookA = V(0f, 0f, 60.03f), lookB = V(1.2f, 0f, 61.03f),
            rollA = 0f, rollB = 18f, fovA = 46f, fovB = 40f,
            aperture = 11f, focalLength = 35f, ease = TrailerShot.Ease.Smooth
        });
        s.shots.Add(Still("02_Siyah", 3f, V(1.2f, 25f, 61.0f), V(1.2f, 0f, 61.03f), 40f, 11f));
        s.post = profile =>
        {
            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            color.contrast.Override(14f);
            color.saturation.Override(-25f);
            WhiteBalance wb = profile.Add<WhiteBalance>(true);
            wb.temperature.Override(-10f);
            FilmGrain grain = profile.Add<FilmGrain>(true);
            grain.intensity.Override(0.2f);
        };
        s.vignette = AnimationCurve.Linear(0f, 0.25f, 11f, 0.4f);
        s.blackout = AnimationCurve.EaseInOut(9.9f, 0f, 11f, 1f);
        Build(s);
    }

    // ------------------------------------------------------------------ Faz 2: sokağa çıkış ve tüketim (14-27)

    private static void WarmPost(VolumeProfile profile)
    {
        ColorAdjustments color = profile.Add<ColorAdjustments>(true);
        color.contrast.Override(10f);
        color.saturation.Override(4f);
        FilmGrain grain = profile.Add<FilmGrain>(true);
        grain.intensity.Override(0.2f);
        Bloom bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(1f);
        bloom.intensity.Override(0.35f);
    }

    /// <summary>Ara sokaktan caddeye çıkış (arkadan takip), simitçinin önünden yandan takip, mağaza cephesi büyür.</summary>
    [MenuItem("Same/Trailer 2/Kur: Faz 2a Sokak")]
    public static void BuildT2Street()
    {
        Spec s = new Spec
        {
            name = "T2_Faz2a_Sokak", gameScene = "Assets/Scenes/StreetScene.unity", timeOffset = 14f, preRoll = 5f,
            dayIndex = 0, population = 60, playerMode = TrailerController.PlayerMode.Acting
        };
        s.cues.Add(Cue(-4f, TrailerActor.Act.Place, null, V(14.2f, 0.1f, 14.5f), -90f));
        s.cues.Add(Cue(-0.6f, TrailerActor.Act.Walk, null, V(7.5f, 0.1f, 14.5f)));
        s.cues.Add(Cue(2.6f, TrailerActor.Act.Place, null, V(2.5f, 0.05f, 22.6f), 0f));
        s.cues.Add(Cue(2.61f, TrailerActor.Act.Walk, null, V(2.5f, 0.05f, 40f)));
        s.cues.Add(Cue(5.2f, TrailerActor.Act.Place, null, V(0.3f, 0.05f, 110.2f), 0f));
        s.cues.Add(Cue(5.21f, TrailerActor.Act.Walk, null, V(0.3f, 0.05f, 119f)));

        s.shots.Add(new Shot
        {
            name = "01_Ara_Sokak", duration = 2.6f,
            posA = V(16.2f, 1.7f, 14.85f), posB = V(12.5f, 1.7f, 14.85f),
            lookA = V(12.8f, 1.35f, 14.4f), lookB = V(9.1f, 1.35f, 14.4f),
            fovA = 44f, fovB = 44f, aperture = 4f, focalLength = 50f, ease = TrailerShot.Ease.Linear,
            noise = "Handheld_normal_mild", noiseAmplitude = 0.6f, noiseFrequency = 0.8f
        });
        s.shots.Add(new Shot
        {
            name = "02_Simitci", duration = 2.6f,
            posA = V(-0.7f, 1.45f, 23.2f), posB = V(-0.7f, 1.45f, 26.8f),
            lookA = V(2.5f, 1.25f, 23.4f), lookB = V(2.5f, 1.25f, 27.0f),
            fovA = 40f, fovB = 40f, aperture = 2.8f, focalLength = 60f, ease = TrailerShot.Ease.Linear,
            noise = "Handheld_normal_mild", noiseAmplitude = 0.5f, noiseFrequency = 0.8f
        });
        s.shots.Add(new Shot
        {
            name = "03_Magaza", duration = 2.2f,
            posA = V(0.7f, 1.05f, 107.4f), posB = V(0.7f, 1.15f, 110.2f),
            lookA = V(0.3f, 3.2f, 120f), lookB = V(0.3f, 3.6f, 120f),
            fovA = 52f, fovB = 46f, aperture = 5.6f, focalLength = 35f, ease = TrailerShot.Ease.Linear
        });
        s.post = WarmPost;
        s.vignette = AnimationCurve.Constant(0f, 7.4f, 0.25f);
        Build(s);
    }

    /// <summary>Mağaza: askıdan paltoyu alır, kasada öder (ürün poşete girer).</summary>
    [MenuItem("Same/Trailer 2/Kur: Faz 2b Magaza")]
    public static void BuildT2Shop()
    {
        Spec s = new Spec
        {
            name = "T2_Faz2b_Magaza", gameScene = "Assets/Scenes/ShopScene.unity", timeOffset = 21.4f, preRoll = 4f,
            dayIndex = 0, playerMode = TrailerController.PlayerMode.Acting
        };
        s.cues.Add(Cue(-3f, TrailerActor.Act.Place, null, V(-6.95f, 0.05f, 13.0f), -90f));
        s.cues.Add(Cue(0.5f, TrailerActor.Act.Hold, "Palto_4"));
        s.cues.Add(Cue(3.0f, TrailerActor.Act.Place, null, V(8.5f, 0.05f, 5.85f), 180f));
        s.cues.Add(Cue(4.4f, TrailerActor.Act.Buy, "Kasa"));
        s.cues.Add(Cue(4.41f, TrailerActor.Act.BagInHand));

        s.shots.Add(new Shot
        {
            name = "01_Palto", duration = 3.0f,
            posA = V(-6.95f, 1.5f, 15.2f), posB = V(-7.1f, 1.45f, 14.85f),
            lookA = V(-7.5f, 1.2f, 13.0f), lookB = V(-7.5f, 1.2f, 13.0f),
            fovA = 42f, fovB = 36f, aperture = 2.8f, focalLength = 60f, ease = TrailerShot.Ease.Smooth
        });
        s.shots.Add(new Shot
        {
            name = "02_Kasa", duration = 3.2f,
            posA = V(9.05f, 1.68f, 3.45f), posB = V(9.0f, 1.66f, 3.75f),
            lookA = V(8.5f, 1.35f, 5.85f), lookB = V(8.5f, 1.3f, 5.85f),
            fovA = 38f, fovB = 34f, aperture = 2.8f, focalLength = 60f, ease = TrailerShot.Ease.Linear
        });
        s.post = profile =>
        {
            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.1f);
            color.contrast.Override(8f);
            Bloom bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.4f);
        };
        s.vignette = AnimationCurve.Constant(0f, 6.2f, 0.25f);
        Build(s);
    }

    // ------------------------------------------------------------------ Faz 3: etkilenme ve dönüşüm (27-38)

    /// <summary>
    /// Dönüş yolu: karakter kameraya doğru yürür, durur, karşıdan gelen sarışın ve estetikli kadınlara bakar (omuz üstü,
    /// sığ netlik), yüzüne yakın çekim. Sonra tempo kırılır: klinik ve kuaför tabelaları/kapıları hızlı kesmelerle,
    /// siyah flaşlarla; son kare saçı sarı, burnu yeni yüz.
    /// </summary>
    [MenuItem("Same/Trailer 2/Kur: Faz 3 Donusum")]
    public static void BuildT2Transform()
    {
        Spec s = new Spec
        {
            name = "T2_Faz3_Donusum", gameScene = "Assets/Scenes/StreetScene.unity", timeOffset = 27f, preRoll = 5f,
            dayIndex = 3, owned = new[] { ProductType.Palto }, population = 70,
            playerMode = TrailerController.PlayerMode.Acting
        };
        s.cues.Add(Cue(-4f, TrailerActor.Act.Place, null, V(-2.5f, 0.05f, 100.6f), 180f));
        s.cues.Add(Cue(-0.5f, TrailerActor.Act.Walk, null, V(-2.5f, 0.05f, 95.8f)));
        // Klinik kapısına yürür.
        s.cues.Add(Cue(6.8f, TrailerActor.Act.Place, null, V(-4.3f, 0.15f, 38f), -90f));
        s.cues.Add(Cue(6.81f, TrailerActor.Act.Walk, null, V(-6.75f, 0.15f, 38f)));
        // Kuaför kapısına yürür.
        s.cues.Add(Cue(8.1f, TrailerActor.Act.Place, null, V(4.3f, 0.15f, 62f), 90f));
        s.cues.Add(Cue(8.11f, TrailerActor.Act.Walk, null, V(6.9f, 0.15f, 62f)));
        // Siyah flaşta dönüşür: sarı saç, estetikli yüz; kuaförün önünde caddeye döner.
        s.cues.Add(Cue(9.42f, TrailerActor.Act.Dye));
        s.cues.Add(Cue(9.43f, TrailerActor.Act.Surgery));
        s.cues.Add(Cue(9.44f, TrailerActor.Act.Place, null, V(6.1f, 0.15f, 62.4f), -90f));

        s.shots.Add(new Shot
        {
            name = "01_Donus", duration = 3.0f,
            posA = V(-2.35f, 1.5f, 97.9f), posB = V(-2.4f, 1.5f, 94.5f),
            lookA = V(-2.5f, 1.3f, 100.4f), lookB = V(-2.5f, 1.35f, 96.2f),
            fovA = 38f, fovB = 38f, aperture = 2.8f, focalLength = 60f, ease = TrailerShot.Ease.EaseOut
        });
        s.shots.Add(new Shot
        {
            name = "02_Bakis", duration = 2.3f,
            posA = V(-2.05f, 1.62f, 96.75f), posB = V(-2.1f, 1.62f, 96.55f),
            lookA = V(-2.9f, 1.45f, 84f), lookB = V(-2.9f, 1.45f, 84f),
            fovA = 30f, fovB = 26f, aperture = 2.0f, focalLength = 85f, focus = 9f, ease = TrailerShot.Ease.Linear
        });
        s.shots.Add(new Shot
        {
            name = "03_Yuz", duration = 1.5f,
            posA = V(-2.48f, 1.52f, 94.75f), posB = V(-2.49f, 1.52f, 94.95f),
            lookA = V(-2.5f, 1.5f, 95.6f), lookB = V(-2.5f, 1.5f, 95.6f), headLook = true, headOffset = V(0f, 0.03f, -0.08f),
            fovA = 22f, fovB = 18f, aperture = 2.0f, focalLength = 85f, ease = TrailerShot.Ease.Linear
        });
        s.shots.Add(Cut("04_Klinik_Tabela", 0.6f, V(-2.0f, 1.6f, 34.5f), V(-3.2f, 2.4f, 35.6f), V(-7.6f, 4.2f, 38f), V(-7.6f, 4.2f, 38f), 34f, 22f, -6f, 5.6f, 0.9f));
        s.shots.Add(Cut("05_Klinik_Kapi", 0.7f, V(-2.6f, 1.55f, 38.35f), V(-3.3f, 1.55f, 38.3f), V(-7.2f, 1.45f, 38f), V(-7.2f, 1.45f, 38f), 40f, 34f, 3f, 4f, 0.7f));
        s.shots.Add(Cut("06_Kuafor_Tabela", 0.6f, V(2.2f, 1.6f, 58.5f), V(3.3f, 2.4f, 59.7f), V(7.9f, 4.0f, 62f), V(7.9f, 4.0f, 62f), 34f, 22f, 6f, 5.6f, 0.9f));
        s.shots.Add(Cut("07_Kuafor_Kapi", 0.7f, V(2.6f, 1.55f, 61.65f), V(3.3f, 1.55f, 61.7f), V(7.6f, 1.45f, 62f), V(7.6f, 1.45f, 62f), 40f, 34f, -3f, 4f, 0.7f));
        s.shots.Add(new Shot
        {
            name = "08_Sonra", duration = 1.6f,
            posA = V(6.05f, 1.55f, 63.3f), posB = V(5.2f, 1.55f, 62.45f), arc = V(5.45f, 1.55f, 63.05f),
            lookA = V(5.95f, 1.53f, 62.4f), lookB = V(5.92f, 1.53f, 62.4f), headLook = true, headOffset = V(-0.08f, 0.03f, 0f),
            fovA = 30f, fovB = 26f, aperture = 2.0f, focalLength = 85f, ease = TrailerShot.Ease.EaseOut
        });
        s.post = WarmPost;
        s.chromatic = Steps(new[] { 0f, 6.8f, 9.4f, 11f }, new[] { 0f, 0.25f, 0.1f, 0.1f });
        s.motionBlur = Steps(new[] { 0f, 6.8f, 9.4f, 11f }, new[] { 0.1f, 0.5f, 0.15f, 0.15f });
        s.vignette = AnimationCurve.Constant(0f, 11f, 0.28f);
        s.blackout = Flashes(new[] { 8.1f, 9.4f }, 0.12f);
        Build(s);
    }

    // Basamaklı eğri: times[i]'den itibaren values[i].
    private static AnimationCurve Steps(float[] times, float[] values)
    {
        AnimationCurve curve = new AnimationCurve();
        for (int i = 0; i < times.Length; i++)
            curve.AddKey(new Keyframe(times[i], values[i]));
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
        }
        return curve;
    }

    // ------------------------------------------------------------------ Faz 1: eksiklik hissi (0-14)

    /// <summary>
    /// Gece, loş salon: karakter kanepede TV izler (arkasından, TV ışığında), ayağa kalkar (yandan, tüm boy), yatak
    /// odasında dolabını açar (omuz üstü, boş dolabın içi), aynaya bakar ve burnuna dokunur (omuz üstü, yansıma),
    /// eli burnundayken yüzüne yakın çekim. Sabit ve yavaş açılar.
    /// </summary>
    [MenuItem("Same/Trailer 2/Kur: Faz 1 Ev")]
    public static void BuildT2House()
    {
        Spec s = new Spec
        {
            name = "T2_Faz1_Ev", gameScene = "Assets/Scenes/HouseScene.unity", timeOffset = 0f, preRoll = 4f,
            dayIndex = 0, playerMode = TrailerController.PlayerMode.Acting
        };
        Vector3 seat = V(-3.83f, 1.36f, 1.14f);
        s.cues.Add(Cue(-3f, TrailerActor.Act.Sit, "Kanepe", seat));
        s.cues.Add(Cue(3.7f, TrailerActor.Act.StandUp));
        // Dolap: önünde durur, iki kapağı birden açar.
        s.cues.Add(Cue(6.5f, TrailerActor.Act.Place, null, V(4.7f, 1.4f, -0.08f), 180f));
        s.cues.Add(Cue(6.9f, TrailerActor.Act.Toggle, "Dolap"));
        // Ayna: karşısında durur, burnuna dokunur (yenilgi klibi karakteri kameradan çeviriyordu, kullanılmadı).
        s.cues.Add(Cue(9.5f, TrailerActor.Act.Place, null, V(2.06f, 1.4f, -0.62f), -90f));
        s.cues.Add(Cue(9.52f, TrailerActor.Act.PrepareForPose));
        s.cues.Add(Cue(10.1f, TrailerActor.Act.Touch, null, V(0.0f, -0.12f, 0.12f), 3.6f));

        s.shots.Add(new Shot
        {
            name = "01_TV", duration = 3.5f,
            posA = V(-1.25f, 2.6f, -1.25f), posB = V(-1.55f, 2.52f, -0.95f),
            lookA = V(-3.6f, 2.15f, 2.6f), lookB = V(-3.6f, 2.15f, 2.6f),
            fovA = 46f, fovB = 44f, aperture = 4f, focalLength = 35f, ease = TrailerShot.Ease.Linear
        });
        s.shots.Add(new Shot
        {
            name = "02_Kalkis", duration = 3.0f,
            posA = V(-1.15f, 2.2f, 1.35f), posB = V(-1.2f, 2.2f, 1.3f),
            lookA = V(-3.83f, 2.15f, 1.2f), lookB = V(-3.83f, 2.2f, 1.25f),
            fovA = 42f, fovB = 42f, aperture = 4f, focalLength = 35f, ease = TrailerShot.Ease.Linear
        });
        s.shots.Add(new Shot
        {
            name = "03_Dolap", duration = 3.0f,
            posA = V(5.45f, 3.25f, 1.9f), posB = V(5.35f, 3.15f, 1.6f),
            lookA = V(4.7f, 2.3f, -1.2f), lookB = V(4.7f, 2.3f, -1.2f),
            fovA = 48f, fovB = 44f, aperture = 4f, focalLength = 35f, ease = TrailerShot.Ease.Linear
        });
        s.shots.Add(new Shot
        {
            name = "04_Ayna", duration = 2.5f,
            posA = V(2.88f, 3.06f, -0.38f), posB = V(2.76f, 3.04f, -0.4f),
            lookA = V(0.46f, 2.5f, -0.62f), lookB = V(0.46f, 2.5f, -0.62f),
            fovA = 34f, fovB = 30f, aperture = 2.8f, focalLength = 50f, focus = 3.9f, ease = TrailerShot.Ease.Linear
        });
        s.shots.Add(new Shot
        {
            name = "05_Yuz", duration = 2.0f,
            posA = V(1.22f, 2.88f, -1.2f), posB = V(1.3f, 2.86f, -1.12f),
            lookA = V(1.88f, 2.82f, -0.62f), lookB = V(1.88f, 2.78f, -0.62f), headLook = true, headOffset = V(-0.08f, 0.03f, 0f),
            fovA = 30f, fovB = 27f, aperture = 2.8f, focalLength = 85f, ease = TrailerShot.Ease.Linear
        });
        s.post = profile =>
        {
            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.35f);
            color.contrast.Override(12f);
            color.saturation.Override(-15f);
            WhiteBalance wb = profile.Add<WhiteBalance>(true);
            wb.temperature.Override(-8f);
            FilmGrain grain = profile.Add<FilmGrain>(true);
            grain.intensity.Override(0.3f);
        };
        s.vignette = AnimationCurve.Constant(0f, 14f, 0.38f);
        // İlk kare karanlıktan açılır.
        s.blackout = AnimationCurve.EaseInOut(0f, 1f, 1.2f, 0f);
        s.extras = root =>
        {
            // TV'nin ışığı çekimde zayıf: ekranın önünden kanepeye soğuk, yumuşak dolgu.
            GameObject fill = new GameObject("TV_Dolgu_Isigi");
            fill.transform.SetParent(root.transform, false);
            fill.transform.position = V(-3.5f, 2.45f, 3.3f);
            fill.transform.rotation = Quaternion.LookRotation(V(-3.7f, 1.9f, 1.0f) - fill.transform.position);
            Light light = fill.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = 110f;
            light.innerSpotAngle = 50f;
            light.range = 6f;
            light.intensity = 4f;
            light.color = new Color(0.7f, 0.82f, 1f);
            light.shadows = LightShadows.Soft;
        };
        Build(s);
    }
}
