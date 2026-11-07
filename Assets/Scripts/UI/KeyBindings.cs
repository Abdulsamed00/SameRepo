using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Klavye tuş atamaları (07.11, kullanıcı: "klavyeden tuşları kafasına göre değiştirebilsin"). Ayarlar > Tuşlar'da
/// her satır <c>InputSystem_Actions</c> > Player'daki bir klavye bağlamasıdır; değişiklik binding override olarak
/// asset'in kendisine uygulanır (oyuncunun bütün script'leri aynı asset'i kullanır), PlayerPrefs "Ayar_Tuslar"
/// (JSON) olarak saklanır ve oyuncu ilk açıldığında (<see cref="Ensure"/>) yüklenir. Gamepad bağlamalarına
/// dokunulmaz. Hareket satırları WASD bileşiğinin parçalarıdır (ok tuşları yedek olarak çalışmaya devam eder).
/// Aynı tuş başka satırda kullanılıyorsa iki satırın tuşları yer değiştirir. ESC (duraklatma, iptal) atanamaz.
/// </summary>
public static class KeyBindings
{
    private const string PrefKey = "Ayar_Tuslar";
    private const string KeyboardGroup = "Keyboard&Mouse";

    /// <summary>Ayarlar'daki satır: <see cref="Label"/> Türkçe kaynak metin (<see cref="Loc"/> çevirir).</summary>
    public class Entry
    {
        public string Label;
        public string Action;
        public string Part; // bileşik parçası ("up"); boşsa düz düğme

        public Entry(string label, string action, string part = null)
        {
            Label = label;
            Action = action;
            Part = part;
        }
    }

    public static readonly Entry[] Entries =
    {
        new Entry("İleri", "Move", "up"),
        new Entry("Geri git", "Move", "down"),
        new Entry("Sola git", "Move", "left"),
        new Entry("Sağa git", "Move", "right"),
        new Entry("Koş", "Sprint"),
        new Entry("Etkileşim", "Interact"),
        new Entry("Otur / ürünü al", "Sit"),
        new Entry("Bırak", "Drop"),
    };

    private static InputActionAsset asset;
    private static InputActionRebindingExtensions.RebindingOperation operation;

    public static InputActionAsset Asset => asset;
    public static bool IsRebinding => operation != null;

    /// <summary>Kayıtlı atamaları bir kez yükler. Oyuncunun girdi kullanan script'leri Awake'te çağırır.</summary>
    public static void Ensure(InputActionAsset actions)
    {
        if (actions == null || asset == actions)
            return;
        asset = actions;
        string json = PlayerPrefs.GetString(PrefKey, "");
        if (string.IsNullOrEmpty(json))
            return;
        try
        {
            asset.LoadBindingOverridesFromJson(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning("KeyBindings: kayıtlı tuşlar okunamadı, varsayılanlar kullanılıyor. " + e.Message);
            asset.RemoveAllBindingOverrides();
        }
    }

    private static bool Find(Entry entry, out InputAction action, out int index)
    {
        action = asset != null ? asset.FindAction("Player/" + entry.Action) : null;
        index = -1;
        if (action == null)
            return false;
        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding b = action.bindings[i];
            if (b.isComposite || string.IsNullOrEmpty(b.groups) || !b.groups.Contains(KeyboardGroup))
                continue;
            if (string.IsNullOrEmpty(entry.Part) ? !b.isPartOfComposite : b.isPartOfComposite && b.name == entry.Part)
            {
                index = i;
                return true;
            }
        }
        return false;
    }

    /// <summary>Satırın şu anki tuşunun adı ("W", "Left Shift").</summary>
    public static string Display(Entry entry)
    {
        if (!Find(entry, out InputAction action, out int index))
            return "-";
        return action.GetBindingDisplayString(index);
    }

    private static string EffectivePath(Entry entry) =>
        Find(entry, out InputAction action, out int index) ? action.bindings[index].effectivePath : null;

    /// <summary>
    /// Satır için tuş bekler; bir tuşa basılınca atar (çakışan satırla yer değiştirir) ve kaydeder. ESC iptal eder.
    /// <paramref name="done"/> her iki durumda çağrılır.
    /// </summary>
    public static void Rebind(Entry entry, Action done)
    {
        if (operation != null || !Find(entry, out InputAction action, out int index))
        {
            done?.Invoke();
            return;
        }

        string oldPath = action.bindings[index].effectivePath;
        bool wasEnabled = action.enabled;
        action.Disable();

        void Finish(bool completed)
        {
            operation.Dispose();
            operation = null;
            if (completed)
            {
                string newPath = action.bindings[index].effectivePath;
                foreach (Entry other in Entries)
                {
                    if (other == entry || EffectivePath(other) != newPath)
                        continue;
                    Find(other, out InputAction otherAction, out int otherIndex);
                    otherAction.ApplyBindingOverride(otherIndex, oldPath);
                }
                Save();
            }
            if (wasEnabled)
                action.Enable();
            done?.Invoke();
        }

        operation = action.PerformInteractiveRebinding(index)
            .WithControlsHavingToMatchPath("<Keyboard>")
            .WithControlsExcluding("<Keyboard>/anyKey")
            .WithControlsExcluding("<Keyboard>/escape")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnComplete(_ => Finish(true))
            .OnCancel(_ => Finish(false))
            .Start();
    }

    /// <summary>Tüm klavye atamalarını varsayılana döndürür.</summary>
    public static void ResetAll()
    {
        if (asset == null)
            return;
        asset.RemoveAllBindingOverrides();
        Save();
    }

    private static void Save()
    {
        PlayerPrefs.SetString(PrefKey, asset.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
    }
}
