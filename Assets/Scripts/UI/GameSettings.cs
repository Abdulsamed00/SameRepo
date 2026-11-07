using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public enum AudioCategory { Music, Effects, Ambient }

/// <summary>
/// Ayarlar ekranındaki değerler (ses, kontrol, görüntü). PlayerPrefs'te saklanır, oyun açılırken uygulanır.
/// Ses: genel ses <see cref="AudioListener.volume"/>, kategoriler <see cref="Volume"/> ile kaynak sesine çarpılır.
/// Parlaklık: kodla oluşan global Volume'da Color Adjustments › Post Exposure.
/// </summary>
public static class GameSettings
{
    public const int DefaultVolume = 100;
    public const int DefaultSensitivity = 50;
    public const int DefaultBrightness = 50;

    public static int MasterVolume { get; private set; } = DefaultVolume;
    public static int MusicVolume { get; private set; } = DefaultVolume;
    public static int EffectsVolume { get; private set; } = DefaultVolume;
    public static int AmbientVolume { get; private set; } = DefaultVolume;

    /// <summary>0-100; 50 = FirstPersonController'daki hassasiyet olduğu gibi.</summary>
    public static int Sensitivity { get; private set; } = DefaultSensitivity;
    public static bool InvertY { get; private set; }

    public static bool Fullscreen { get; private set; } = true;
    public static int ResolutionIndex { get; private set; }
    public static int Brightness { get; private set; } = DefaultBrightness;
    public static bool VSync { get; private set; } = true;

    /// <summary>Herhangi bir ayar değişince.</summary>
    public static event Action Changed;

    /// <summary>Fare hassasiyeti çarpanı: 0 → x0.25, 50 → x1, 100 → x4.</summary>
    public static float LookSensitivityMultiplier => Mathf.Pow(2f, (Sensitivity - 50) / 25f);

    public static float Volume(AudioCategory category) => category switch
    {
        AudioCategory.Music => MusicVolume / 100f,
        AudioCategory.Ambient => AmbientVolume / 100f,
        _ => EffectsVolume / 100f,
    };

    private static List<Vector2Int> resolutions;
    private static Volume brightnessVolume;
    private static ColorAdjustments brightnessAdjustments;

    /// <summary>Ekranın desteklediği çözünürlükler (büyükten küçüğe, tekrarsız).</summary>
    public static IReadOnlyList<Vector2Int> Resolutions
    {
        get
        {
            if (resolutions == null)
            {
                resolutions = new List<Vector2Int>();
                foreach (Resolution r in Screen.resolutions)
                {
                    Vector2Int size = new Vector2Int(r.width, r.height);
                    if (!resolutions.Contains(size))
                        resolutions.Add(size);
                }
                if (resolutions.Count == 0)
                    resolutions.Add(new Vector2Int(Screen.width, Screen.height));
                resolutions.Sort((a, b) => b.x != a.x ? b.x.CompareTo(a.x) : b.y.CompareTo(a.y));
            }
            return resolutions;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        MasterVolume = PlayerPrefs.GetInt("Ayar_GenelSes", DefaultVolume);
        MusicVolume = PlayerPrefs.GetInt("Ayar_Muzik", DefaultVolume);
        EffectsVolume = PlayerPrefs.GetInt("Ayar_Efekt", DefaultVolume);
        AmbientVolume = PlayerPrefs.GetInt("Ayar_Ortam", DefaultVolume);
        Sensitivity = PlayerPrefs.GetInt("Ayar_Hassasiyet", DefaultSensitivity);
        InvertY = PlayerPrefs.GetInt("Ayar_TersY", 0) == 1;
        Fullscreen = PlayerPrefs.GetInt("Ayar_TamEkran", Screen.fullScreen ? 1 : 0) == 1;
        Brightness = PlayerPrefs.GetInt("Ayar_Parlaklik", DefaultBrightness);
        VSync = PlayerPrefs.GetInt("Ayar_VSync", 1) == 1;

        int saved = ((List<Vector2Int>)Resolutions).IndexOf(new Vector2Int(PlayerPrefs.GetInt("Ayar_GenislikPx", Screen.currentResolution.width),
            PlayerPrefs.GetInt("Ayar_YukseklikPx", Screen.currentResolution.height)));
        ResolutionIndex = saved >= 0 ? saved : 0;

        ApplyAudio();
        ApplyDisplay();
    }

    public static void SetMasterVolume(int value) { MasterVolume = Clamp(value); Save("Ayar_GenelSes", MasterVolume); ApplyAudio(); }
    public static void SetMusicVolume(int value) { MusicVolume = Clamp(value); Save("Ayar_Muzik", MusicVolume); ApplyAudio(); }
    public static void SetEffectsVolume(int value) { EffectsVolume = Clamp(value); Save("Ayar_Efekt", EffectsVolume); ApplyAudio(); }
    public static void SetAmbientVolume(int value) { AmbientVolume = Clamp(value); Save("Ayar_Ortam", AmbientVolume); ApplyAudio(); }
    public static void SetSensitivity(int value) { Sensitivity = Clamp(value); Save("Ayar_Hassasiyet", Sensitivity); Changed?.Invoke(); }
    public static void SetInvertY(bool value) { InvertY = value; Save("Ayar_TersY", value ? 1 : 0); Changed?.Invoke(); }
    public static void SetFullscreen(bool value) { Fullscreen = value; Save("Ayar_TamEkran", value ? 1 : 0); ApplyDisplay(); }
    public static void SetBrightness(int value) { Brightness = Clamp(value); Save("Ayar_Parlaklik", Brightness); ApplyDisplay(); }
    public static void SetVSync(bool value) { VSync = value; Save("Ayar_VSync", value ? 1 : 0); ApplyDisplay(); }

    public static void SetResolutionIndex(int index)
    {
        ResolutionIndex = Mathf.Clamp(index, 0, Resolutions.Count - 1);
        Vector2Int size = Resolutions[ResolutionIndex];
        PlayerPrefs.SetInt("Ayar_GenislikPx", size.x);
        PlayerPrefs.SetInt("Ayar_YukseklikPx", size.y);
        PlayerPrefs.Save();
        ApplyDisplay();
    }

    public static void ResetAudio()
    {
        SetMasterVolume(DefaultVolume);
        SetMusicVolume(DefaultVolume);
        SetEffectsVolume(DefaultVolume);
        SetAmbientVolume(DefaultVolume);
    }

    public static void ResetControls()
    {
        SetSensitivity(DefaultSensitivity);
        SetInvertY(false);
    }

    public static void ResetDisplay()
    {
        SetFullscreen(true);
        SetResolutionIndex(0);
        SetBrightness(DefaultBrightness);
        SetVSync(true);
    }

    private static int Clamp(int value) => Mathf.Clamp(value, 0, 100);

    private static void Save(string key, int value)
    {
        PlayerPrefs.SetInt(key, value);
        PlayerPrefs.Save();
    }

    private static void ApplyAudio()
    {
        AudioListener.volume = MasterVolume / 100f;
        Changed?.Invoke();
    }

    private static void ApplyDisplay()
    {
        QualitySettings.vSyncCount = VSync ? 1 : 0;

#if !UNITY_EDITOR
        Vector2Int size = Resolutions[Mathf.Clamp(ResolutionIndex, 0, Resolutions.Count - 1)];
        FullScreenMode mode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        if (Screen.width != size.x || Screen.height != size.y || Screen.fullScreenMode != mode)
            Screen.SetResolution(size.x, size.y, mode);
#endif

        EnsureBrightnessVolume();
        // 0 → -1 EV, 50 → 0, 100 → +1 EV.
        brightnessAdjustments.postExposure.Override((Brightness - 50) / 50f + SceneExposure);
        Changed?.Invoke();
    }

    /// <summary>
    /// Sahneye özel ek pozlama (EV), parlaklık ayarının üstüne eklenir (05.11: ev gündüz sadece güneşle aydınlandığı
    /// için karanlık kalıyordu; <see cref="HouseNightfall"/> gündüz açar, gece ve sahne kapanınca 0).
    /// </summary>
    public static float SceneExposure { get; private set; }

    public static void SetSceneExposure(float ev)
    {
        if (Mathf.Approximately(SceneExposure, ev))
            return;
        SceneExposure = ev;
        EnsureBrightnessVolume();
        brightnessAdjustments.postExposure.Override((Brightness - 50) / 50f + SceneExposure);
    }

    private static void EnsureBrightnessVolume()
    {
        if (brightnessVolume != null)
            return;
        GameObject go = new GameObject("Parlaklik_Volume");
        UnityEngine.Object.DontDestroyOnLoad(go);
        brightnessVolume = go.AddComponent<Volume>();
        brightnessVolume.isGlobal = true;
        brightnessVolume.priority = 100f;
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        brightnessAdjustments = profile.Add<ColorAdjustments>();
        brightnessVolume.sharedProfile = profile;
    }
}
