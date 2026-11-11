using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using A = TrailerAction.Kind;

/// <summary>
/// Hikâye fragmanı (11.11, kullanıcının 5 fazlı senaryosu): karakter görünür ve <see cref="TrailerActor"/> ile oynatılır.
/// F1 eksiklik (ev, gece) → F2 tüketim (sokak + mağaza) → F3 dönüşüm (sarışın, klinik/kuaför, ayna) →
/// F4 kaos (Faz3a_Magaza + Faz3b_Sokak, eski hızlı kesmeler) → F5 asimilasyon (tepeden kalabalık, SAME).
/// </summary>
public static partial class TrailerBuilder
{
    private const string House = "Assets/Scenes/HouseScene.unity";
    private const string Street = "Assets/Scenes/StreetScene.unity";
    private const string Shop = "Assets/Scenes/ShopScene.unity";

    private static Shot S(string name, float duration, Vector3 posA, Vector3 posB, Vector3 lookA, Vector3 lookB,
        float fovA, float fovB, float aperture = 4f, bool follow = false, string noise = null, float noiseAmp = 0.8f)
    {
        return new Shot
        {
            name = name, duration = duration, posA = posA, posB = posB, lookA = lookA, lookB = lookB,
            fovA = fovA, fovB = fovB, aperture = aperture, focalLength = 50f, ease = TrailerShot.Ease.Smooth,
            follow = follow, noise = noise, noiseAmplitude = noiseAmp, noiseFrequency = 1f
        };
    }

    // ------------------------------------------------------------------ F1: eksiklik (ev, gece)

    [MenuItem("Same/Trailer/Hikaye/F1 Ev")]
    public static void BuildF1()
    {
        Spec s = new Spec
        {
            name = "F1_Ev", gameScene = House, timeOffset = 0f, preRoll = 5f,
            playerMode = TrailerController.PlayerMode.Actor
        };
        // 1a: kanepede TV ışığında oturuyor.
        s.Act(-4.5f, A.SitOnSeat, V(-3.83f, 1.36f, 1.14f), target: "Kanepe");
        s.shots.Add(S("1a_Kanepe", 4f, V(-1.3f, 2.35f, 2.7f), V(-1.45f, 2.33f, 2.6f), V(-3.8f, 2.15f, 1.2f), V(-3.8f, 2.12f, 1.2f), 42f, 40f, 2.8f));
        // 1b: ayağa kalkar, tüm vücut.
        s.Act(4.2f, A.StandUp);
        s.shots.Add(S("1b_Kalkis", 3f, V(-0.9f, 2.2f, 3.2f), V(-1.0f, 2.25f, 3.0f), V(-3.8f, 2.25f, 1.1f), V(-3.75f, 2.45f, 1.3f), 46f, 46f, 4f));
        // 1c: dolabın önünde, iki kapağı açar.
        // İki kapak tek Openable: bir kez açmak ikisini birden açar (ikinci çağrı kapatır).
        s.Act(7f, A.Place, V(4.55f, 1.36f, 0.35f), 180f);
        s.Act(7.4f, A.OpenPart, target: "Dolap", index: 0);
        s.shots.Add(S("1c_Dolap", 3.5f, V(6.0f, 2.9f, 1.45f), V(5.8f, 2.85f, 1.15f), V(4.6f, 2.3f, -0.9f), V(4.6f, 2.25f, -0.9f), 46f, 44f, 4f));
        // 1d: aynanın önünde, omuz üstünden yansıma.
        s.Act(10.5f, A.Place, V(1.50f, 1.36f, -0.62f), 270f);
        s.shots.Add(S("1d_Ayna", 2f, V(2.45f, 3.05f, -0.22f), V(2.32f, 3.02f, -0.28f), V(0.456f, 2.55f, -0.62f), V(0.456f, 2.6f, -0.62f), 40f, 38f, 5.6f));
        // 1e: yansımada yüz yakın; el burna gider, sonra omuzlar çöker.
        s.Act(12.8f, A.TouchNose, duration: 1.3f);
        s.Act(14.1f, A.Defeat);
        // Ayna 6.9° geriye yaslı: yansımadaki yüz göz hizasından ~0.2 m aşağıda görünür.
        s.shots.Add(S("1e_Yuz", 3f, V(2.15f, 3.0f, -0.38f), V(2.05f, 2.98f, -0.39f), V(-0.59f, 2.68f, -0.62f), V(-0.59f, 2.62f, -0.62f), 14f, 15f, 2.8f));
        s.post = profile =>
        {
            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            color.contrast.Override(12f);
            color.saturation.Override(-18f);
            WhiteBalance wb = profile.Add<WhiteBalance>(true);
            wb.temperature.Override(-10f);
            FilmGrain grain = profile.Add<FilmGrain>(true);
            grain.intensity.Override(0.3f);
        };
        s.vignette = AnimationCurve.Constant(0f, 16f, 0.32f);
        Build(s);
    }

    // ------------------------------------------------------------------ F2: tüketim (sokak + mağaza)

    [MenuItem("Same/Trailer/Hikaye/F2 Sokak")]
    public static void BuildF2Street()
    {
        Spec s = new Spec
        {
            name = "F2_Sokak", gameScene = Street, timeOffset = 15.5f, preRoll = 6f, population = 80,
            playerMode = TrailerController.PlayerMode.Actor
        };
        // 2a: caddede arkadan takip. 2b: simitçinin önünden geçerken yandan.
        s.Act(-1f, A.Place, V(-3.8f, 0.05f, 62.5f), 0f);
        s.Act(-0.6f, A.WalkTo, V(-3.8f, 0f, 80f));
        s.shots.Add(S("2a_Takip", 3.5f, V(0.55f, 1.85f, -2.4f), V(0.55f, 1.85f, -2.4f), V(0f, 1.45f, 5f), V(0f, 1.45f, 5f), 48f, 48f, 5.6f, true, "Handheld_normal_mild"));
        s.shots.Add(S("2b_Simitci", 2.5f, V(2.3f, 1.45f, 0.6f), V(2.3f, 1.45f, 0.2f), V(-1.5f, 1.25f, 0.9f), V(-1.5f, 1.25f, 0.5f), 40f, 40f, 4f, true, "Handheld_normal_mild"));
        // 2c: mağazanın cephesi, karakter kapıya yürür.
        s.Act(6f, A.Place, V(0.4f, 0.05f, 112.5f), 0f);
        s.Act(6.05f, A.WalkTo, V(0.4f, 0f, 118.8f));
        s.shots.Add(S("2c_Magaza_Cephe", 2.5f, V(2.4f, 0.7f, 109.2f), V(2.2f, 0.75f, 110f), V(0.3f, 3.8f, 120f), V(0.3f, 4.0f, 120f), 50f, 48f, 8f));
        s.post = StreetPost;
        Build(s);
    }

    [MenuItem("Same/Trailer/Hikaye/F2 Magaza")]
    public static void BuildF2Shop()
    {
        Spec s = new Spec
        {
            // Az müşteri: kalabalıkta NPC'ler çekimin önünden geçiyordu.
            name = "F2_Magaza", gameScene = Shop, timeOffset = 24f, preRoll = 3f, population = 2,
            playerMode = TrailerController.PlayerMode.Actor
        };
        // 2d: askıdan paltoyu iki eliyle alır.
        s.Act(-1f, A.Place, V(-7.35f, 0.02f, 13f), 270f);
        s.Act(0.5f, A.PickUp, V(-8.4f, 1.2f, 13f));
        s.shots.Add(S("2d_Palto", 3.2f, V(-6.3f, 1.55f, 15.0f), V(-6.45f, 1.55f, 14.8f), V(-7.8f, 1.25f, 13.0f), V(-7.8f, 1.3f, 13.0f), 42f, 40f, 2.8f));
        // 2e: kasada öder; kasiyerin arkasından.
        s.Act(3.2f, A.Place, V(8.4f, 0.02f, 5.85f), 180f);
        s.Act(4.4f, A.Checkout);
        s.shots.Add(S("2e_Kasa", 2.8f, V(8.85f, 1.75f, 3.35f), V(8.8f, 1.72f, 3.5f), V(8.35f, 1.35f, 5.9f), V(8.35f, 1.4f, 5.9f), 40f, 38f, 2.8f));
        s.post = profile =>
        {
            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.15f);
            color.contrast.Override(8f);
            Bloom bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.45f);
        };
        s.vignette = AnimationCurve.Constant(0f, 7f, 0.22f);
        Build(s);
    }

    // ------------------------------------------------------------------ F3: dönüşüm (hızlı montaj)

    [MenuItem("Same/Trailer/Hikaye/F3 Sokak")]
    public static void BuildF3Street()
    {
        Spec s = new Spec
        {
            name = "F3_Sokak", gameScene = Street, timeOffset = 30f, preRoll = 6f, dayIndex = 1,
            owned = new[] { ProductType.Palto }, population = 90,
            playerMode = TrailerController.PlayerMode.Actor
        };
        // Mağazadan dönüş: karakter eve doğru (-z) yürür, sarışın bir kadına kilitlenir.
        s.Act(-1f, A.Place, V(3.0f, 0.05f, 72f), 180f);
        s.Act(-0.6f, A.WalkTo, V(3.0f, 0f, 45f), speed: 1.3f);
        s.shots.Add(S("3a_Donus", 2f, V(0.4f, 1.75f, -1.7f), V(0.4f, 1.75f, -1.7f), V(0f, 1.55f, 6f), V(0f, 1.55f, 6f), 46f, 46f, 5.6f, true, "Handheld_normal_mild"));
        Shot lockOn = S("3b_Sarisin", 1.8f, V(0.4f, 1.75f, -1.7f), V(0.35f, 1.75f, -1.5f), V(0f, 1.55f, 6f), V(0f, 1.55f, 6f), 40f, 16f, 2.8f, true, "Handheld_tele_mild");
        lockOn.blonde = true;
        lockOn.ease = TrailerShot.Ease.EaseIn;
        s.shots.Add(lockOn);
        s.shots.Add(S("3c_Yuz", 0.8f, V(0.12f, 1.58f, 0.75f), V(0.12f, 1.58f, 0.68f), V(0f, 1.5f, 0f), V(0f, 1.5f, 0f), 32f, 28f, 2.8f, true));
        // Klinik ve kuaför: tabela + siyah flaş (iç mekân yok).
        s.shots.Add(Cut("3d_Klinik", 0.5f, V(-2.2f, 1.5f, 40f), V(-2.7f, 1.5f, 39.7f), V(-8.2f, 3.9f, 38f), V(-8.2f, 3.9f, 38f), 22f, 15f, -6f, 5.6f, 1.2f));
        s.shots.Add(Cut("3e_Kuafor", 1.0f, V(3f, 1.5f, 60.2f), V(3.5f, 1.5f, 60.5f), V(8.6f, 3.7f, 62f), V(8.6f, 3.7f, 62f), 22f, 15f, 6f, 5.6f, 1.2f));
        s.blackout = Flashes(6.1f, 5.1f, 0.22f, 5.85f, 0.25f);
        s.post = FrenzyPost;
        s.chromatic = AnimationCurve.Linear(0f, 0.05f, 6.1f, 0.35f);
        s.motionBlur = AnimationCurve.Linear(0f, 0.2f, 6.1f, 0.5f);
        s.vignette = AnimationCurve.Linear(0f, 0.25f, 6.1f, 0.35f);
        Build(s);
    }

    [MenuItem("Same/Trailer/Hikaye/F3 Ayna")]
    public static void BuildF3Mirror()
    {
        Spec s = new Spec
        {
            name = "F3_Ayna", gameScene = House, timeOffset = 36.1f, preRoll = 4f, dayIndex = 3,
            owned = new[] { ProductType.Palto, ProductType.Pantolon, ProductType.Ayakkabi }, blonde = true, surgery = true,
            playerMode = TrailerController.PlayerMode.Actor
        };
        // Aynada sonuç: yeni burun/dudak, sonra sarı saç. Yansımaya çok dar açı.
        s.Act(-2f, A.Place, V(1.50f, 1.36f, -0.62f), 270f);
        // Ayna geriye yaslı: yansımadaki yüz göz hizasından ~0.25 m aşağıda (F1'deki yakın çekimle ölçüldü).
        s.shots.Add(S("3f_Burun", 0.9f, V(2.1f, 2.95f, -0.40f), V(2.05f, 2.95f, -0.40f), V(-0.59f, 2.58f, -0.62f), V(-0.59f, 2.58f, -0.62f), 9f, 8f, 2.8f));
        s.shots.Add(S("3g_Sac", 0.9f, V(2.1f, 3.0f, -0.42f), V(2.0f, 3.0f, -0.42f), V(-0.59f, 2.74f, -0.62f), V(-0.59f, 2.72f, -0.62f), 13f, 10f, 2.8f));
        s.post = FrenzyPost;
        s.chromatic = AnimationCurve.Linear(0f, 0.35f, 1.8f, 0.5f);
        s.vignette = AnimationCurve.Constant(0f, 1.8f, 0.35f);
        Build(s);
    }

    // ------------------------------------------------------------------ F5: asimilasyon (tepeden kalabalık + SAME)

    [MenuItem("Same/Trailer/Hikaye/F5 Kalabalik")]
    public static void BuildF5()
    {
        Spec s = new Spec
        {
            name = "F5_Kalabalik", gameScene = Street, timeOffset = 53f, preRoll = 16f, dayIndex = 4,
            owned = AllProducts, blonde = true, surgery = true,
            playerMode = TrailerController.PlayerMode.Actor,
            logo = true, logoTypewriter = true, logoTime = 7.6f, logoFade = 0.22f
        };
        // Karakter caddenin ortasında; benzerler (ve kopyaları) etrafına toplanır, hepsi mağazaya döner.
        s.Act(-15.9f, A.Place, V(0f, 0.05f, 60f), 0f);
        s.Act(-15.5f, A.CrowdGather);
        s.Act(-4f, A.CrowdConform, yaw: 0f);
        s.Act(6.2f, A.CrowdFreeze);
        s.shots.Add(S("5a_Tepe", 7f, V(0f, 5.5f, 60f), V(0f, 24f, 60f), V(0f, 0f, 60.6f), V(0f, 0f, 61.5f), 40f, 40f, 8f));
        s.shots.Add(S("5b_Siyah", 3.5f, V(0f, 24f, 60f), V(0f, 24f, 60f), V(0f, 0f, 61.5f), V(0f, 0f, 61.5f), 40f, 40f, 8f));
        AnimationCurve black = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(6.4f, 0f), new Keyframe(7.0f, 1f), new Keyframe(10.5f, 1f));
        s.blackout = black;
        s.post = profile =>
        {
            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            color.contrast.Override(10f);
            color.saturation.Override(-10f);
        };
        s.vignette = AnimationCurve.Constant(0f, 10.5f, 0.25f);
        Build(s);
    }

    [MenuItem("Same/Trailer/Hikaye/Hepsini kur")]
    public static void BuildStory()
    {
        BuildF1();
        BuildF2Street();
        BuildF2Shop();
        BuildF3Street();
        BuildF3Mirror();
        BuildF5();
    }
}
