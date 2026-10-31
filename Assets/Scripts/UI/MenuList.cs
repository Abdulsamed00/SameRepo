using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Ana menü ve duraklatma menüsünün dikey listesi: satır 66 px, seçili satır Inter Medium beyaz ve solunda
/// <c>Secim_Cizgisi</c>, diğerleri Inter Regular %78. Fareyle üstüne gelince seçilir, tıklayınca çalışır;
/// klavyede yukarı/aşağı (W/S) ve Enter/Boşluk, gamepad'de d-pad ve A.
/// </summary>
public class MenuList : MonoBehaviour
{
    public const float RowHeight = 66f;
    private const float LineWidth = 33f;
    private const float LineGap = 24f;

    private class Item
    {
        public TextMeshProUGUI Label;
        public Image Line;
        public Action Action;
    }

    private readonly List<Item> items = new List<Item>();
    private int selected;

    /// <summary>Kapalıyken klavye girdisi okunmaz (üstte başka bir ekran açıkken).</summary>
    public bool Interactive
    {
        get => interactive;
        set
        {
            interactive = value;
            // Üstteki ekranı kapatan tuş (Enter/ESC) aynı karede bu listede de işlenmesin.
            enabledFrame = Time.frameCount;
        }
    }

    private bool interactive = true;
    private int enabledFrame = -1;

    public AudioClip ClickSound { get; set; }

    public float Height => items.Count * RowHeight;

    public static MenuList Create(Transform parent)
    {
        RectTransform rect = UiKit.CreateRect("Menu", parent);
        return rect.gameObject.AddComponent<MenuList>();
    }

    public void Add(string label, Action action)
    {
        int index = items.Count;
        RectTransform row = UiKit.CreateRect("Satir_" + label, transform);
        row.anchorMin = row.anchorMax = row.pivot = new Vector2(0f, 1f);
        row.anchoredPosition = new Vector2(0f, -index * RowHeight);
        row.sizeDelta = new Vector2(560f, RowHeight);

        // Tıklama alanı: görünmez, satırın tamamı.
        Image hit = row.gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        MenuRowEvents events = row.gameObject.AddComponent<MenuRowEvents>();
        events.Hover = () => Select(index);
        events.Click = () => Activate(index);

        Image line = UiKit.CreateImage("Cizgi", row, UiKit.White, UiKit.Sprite("Secim_Cizgisi"));
        UiKit.Place(line.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(LineWidth, 3f));

        TextMeshProUGUI text = UiKit.CreateText("Yazi", row, label, UiKit.FontKind.Regular, 26f, 6f, UiKit.Unselected,
            TextAlignmentOptions.MidlineLeft);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(LineWidth + LineGap, 0f);
        textRect.offsetMax = Vector2.zero;

        items.Add(new Item { Label = text, Line = line, Action = action });
        RectTransform self = (RectTransform)transform;
        self.sizeDelta = new Vector2(560f, Height);
        Refresh();
    }

    public void Select(int index)
    {
        if (items.Count == 0)
            return;
        selected = (index % items.Count + items.Count) % items.Count;
        Refresh();
    }

    private void Activate(int index)
    {
        if (!Interactive)
            return;
        Select(index);
        GameAudio.PlayUI(ClickSound);
        items[index].Action?.Invoke();
    }

    private void Refresh()
    {
        for (int i = 0; i < items.Count; i++)
        {
            bool on = i == selected;
            Item item = items[i];
            item.Line.enabled = on;
            UiKit.FontKind kind = on ? UiKit.FontKind.Medium : UiKit.FontKind.Regular;
            UiKit.SetFont(item.Label, kind);
            item.Label.color = on ? UiKit.White : UiKit.Unselected;
        }
    }

    private void Update()
    {
        if (!Interactive || items.Count == 0 || Time.frameCount == enabledFrame)
            return;

        if (UiInput.Up())
            Select(selected - 1);
        else if (UiInput.Down())
            Select(selected + 1);
        else if (UiInput.Submit())
            Activate(selected);
    }
}

/// <summary>Menü satırının fare olayları.</summary>
public class MenuRowEvents : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    public Action Hover;
    public Action Click;

    public void OnPointerEnter(PointerEventData eventData) => Hover?.Invoke();

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            Click?.Invoke();
    }
}

/// <summary>Menülerde klavye/gamepad gezinmesi (input action asset'inde UI map'i kullanılmıyor, cihazlar doğrudan okunur).</summary>
public static class UiInput
{
    private static bool Key(Key a, Key b = UnityEngine.InputSystem.Key.None)
    {
        Keyboard k = Keyboard.current;
        if (k == null)
            return false;
        return k[a].wasPressedThisFrame || (b != UnityEngine.InputSystem.Key.None && k[b].wasPressedThisFrame);
    }

    private static bool Pad(Func<Gamepad, bool> check) => Gamepad.current != null && check(Gamepad.current);

    public static bool Up() => Key(UnityEngine.InputSystem.Key.UpArrow, UnityEngine.InputSystem.Key.W) || Pad(g => g.dpad.up.wasPressedThisFrame);
    public static bool Down() => Key(UnityEngine.InputSystem.Key.DownArrow, UnityEngine.InputSystem.Key.S) || Pad(g => g.dpad.down.wasPressedThisFrame);
    public static bool Left() => Key(UnityEngine.InputSystem.Key.LeftArrow, UnityEngine.InputSystem.Key.A) || Pad(g => g.dpad.left.wasPressedThisFrame);
    public static bool Right() => Key(UnityEngine.InputSystem.Key.RightArrow, UnityEngine.InputSystem.Key.D) || Pad(g => g.dpad.right.wasPressedThisFrame);
    public static bool Submit() => Key(UnityEngine.InputSystem.Key.Enter, UnityEngine.InputSystem.Key.Space) || Pad(g => g.buttonSouth.wasPressedThisFrame);
    public static bool Back() => Key(UnityEngine.InputSystem.Key.Escape) || Pad(g => g.buttonEast.wasPressedThisFrame || g.startButton.wasPressedThisFrame);
    public static bool Reset() => Key(UnityEngine.InputSystem.Key.R) || Pad(g => g.buttonNorth.wasPressedThisFrame);
    public static bool NextTab() => Key(UnityEngine.InputSystem.Key.Tab, UnityEngine.InputSystem.Key.E) || Pad(g => g.rightShoulder.wasPressedThisFrame);
    public static bool PreviousTab() => Key(UnityEngine.InputSystem.Key.Q) || Pad(g => g.leftShoulder.wasPressedThisFrame);
}
