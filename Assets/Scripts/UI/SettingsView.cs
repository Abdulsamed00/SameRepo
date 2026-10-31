using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ayarlar ekranı (Ses / Kontroller / Görüntü sekmeleri), ana menüden ve duraklatmadan açılır. Değerler
/// <see cref="GameSettings"/>'e anında yazılır. Fare: sekmeye/oka tıkla, slider'ı sürükle. Klavye: yukarı/aşağı
/// satır, sol/sağ değer, Tab/Q/E sekme, R sekmeyi varsayılana döndürür, ESC geri.
/// Yerleşim paketteki gibi: sol üst (144, 126), genişlik 960, satır 87, slider 360.
/// </summary>
public class SettingsView : MonoBehaviour
{
    private const float Width = 960f;
    private const float RowHeight = 87f;
    private const float FirstRowTop = 212f;   // başlık üstünden ilk satırın üstüne (ekranda y = 338)
    private const float TabCenter = 126f;     // ekranda y = 252
    private const float TabLine = 158f;       // ekranda y = 284
    private const float SliderLeft = 525f;    // ekranda x = 669
    private const float SliderWidth = 360f;
    private const float ChoiceLeft = 636f;    // ekranda x = 780 (sol okun ortası)
    private const float ChoiceRight = 932f;   // ekranda x = 1076

    private static readonly string[] OffOn = { "Kapalı", "Açık" };

    private abstract class Row
    {
        public RectTransform Rect;
        public TextMeshProUGUI Label;
        public abstract void Step(int direction);
        public abstract void Refresh();
    }

    private class SliderRow : Row
    {
        public Slider Slider;
        public TextMeshProUGUI Value;
        public Func<int> Get;
        public Action<int> Set;
        public bool Updating;

        public override void Step(int direction) => Set(Mathf.Clamp(Get() + direction * 5, 0, 100));

        public override void Refresh()
        {
            Updating = true;
            Slider.SetValueWithoutNotify(Get());
            Updating = false;
            Value.text = Get().ToString();
        }
    }

    private class ChoiceRow : Row
    {
        public TextMeshProUGUI Value;
        public Func<int> Count;
        public Func<int, string> Name;
        public Func<int> Get;
        public Action<int> Set;

        public override void Step(int direction)
        {
            int count = Count();
            if (count > 0)
                Set(((Get() + direction) % count + count) % count);
        }

        public override void Refresh() => Value.text = Name(Get());
    }

    private class Tab
    {
        public string Title;
        public TextMeshProUGUI Text;
        public Action<SettingsView> Build;
        public Action Reset;
    }

    private readonly List<Tab> tabs = new List<Tab>();
    private readonly List<Row> rows = new List<Row>();
    private RectTransform content;
    private RectTransform rowsRoot;
    private Image tabUnderline;
    private int currentTab;
    private int selectedRow;
    private Action onClose;
    private AudioClip clickSound;

    /// <param name="backdrop">Bulanık arka plan (yoksa sadece karartma). Ekran kapanınca serbest bırakılmaz, sahibi bırakır.</param>
    public static SettingsView Open(Transform parent, Texture backdrop, AudioClip clickSound, Action onClose)
    {
        RectTransform root = UiKit.CreateRect("Ayarlar", parent);
        UiKit.Stretch(root);
        SettingsView view = root.gameObject.AddComponent<SettingsView>();
        view.onClose = onClose;
        view.clickSound = clickSound;
        view.Build(backdrop);
        return view;
    }

    private void OnEnable() => GameSettings.Changed += RefreshRows;

    private void OnDisable() => GameSettings.Changed -= RefreshRows;

    private void Build(Texture backdrop)
    {
        if (backdrop != null)
        {
            RawImage blur = UiKit.CreateRect("Bulanik", transform).gameObject.AddComponent<RawImage>();
            blur.texture = backdrop;
            blur.raycastTarget = false;
            UiKit.Stretch(blur.rectTransform);
        }
        Image dim = UiKit.CreateFullscreen("Karartma", transform, UiKit.WithAlpha(UiKit.Dim, 0.55f));
        dim.raycastTarget = true; // arkadaki menüye tıklanmasın

        content = UiKit.CreateRect("Icerik", transform);
        UiKit.Place(content, new Vector2(0f, 1f), new Vector2(144f, -126f), new Vector2(Width, 700f));

        TextMeshProUGUI title = UiKit.CreateText("Baslik", content, "AYARLAR", UiKit.FontKind.Light, 33f, 45f);
        UiKit.Place(title.rectTransform, new Vector2(0f, 1f), Vector2.zero, new Vector2(Width, 40f));
        title.alignment = TextAlignmentOptions.TopLeft;

        Image divider = UiKit.CreateImage("Sekme_Cizgisi", content, UiKit.TabDivider);
        UiKit.Place(divider.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -TabLine), new Vector2(Width, 1f));
        tabUnderline = UiKit.CreateImage("Secili_Sekme", content, UiKit.White);

        AddTab("Ses", v => v.BuildAudio(), GameSettings.ResetAudio);
        AddTab("Kontroller", v => v.BuildControls(), GameSettings.ResetControls);
        AddTab("Görüntü", v => v.BuildDisplay(), GameSettings.ResetDisplay);

        rowsRoot = UiKit.CreateRect("Satirlar", content);
        UiKit.Place(rowsRoot, new Vector2(0f, 1f), new Vector2(0f, -FirstRowTop), new Vector2(Width, 500f));

        RectTransform back = UiKit.CreateFooterHint(transform, "ESC", "Geri", Vector2.zero, new Vector2(144f, 81f));
        MakeClickable(back, Close);
        RectTransform reset = UiKit.CreateFooterHint(transform, "R", "Varsayılanlara dön", new Vector2(1f, 0f), new Vector2(-144f, 81f));
        MakeClickable(reset, ResetTab);

        ShowTab(0);
    }

    private void AddTab(string title, Action<SettingsView> build, Action reset)
    {
        int index = tabs.Count;
        float x = 0f;
        foreach (Tab t in tabs)
            x += t.Text.rectTransform.sizeDelta.x + 48f;

        TextMeshProUGUI text = UiKit.CreateText("Sekme_" + title, content, title, UiKit.FontKind.Medium, 22.5f, 4f, UiKit.Secondary,
            TextAlignmentOptions.Center);
        float width = text.GetPreferredValues(title).x;
        UiKit.Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(x, -(TabCenter - 20f)), new Vector2(width, 40f));
        text.raycastTarget = true;
        MakeClickable(text.rectTransform, () => ShowTab(index));

        tabs.Add(new Tab { Title = title, Text = text, Build = build, Reset = reset });
    }

    private void ShowTab(int index)
    {
        currentTab = (index % tabs.Count + tabs.Count) % tabs.Count;
        for (int i = 0; i < tabs.Count; i++)
            tabs[i].Text.color = i == currentTab ? UiKit.White : UiKit.Secondary;

        RectTransform tabRect = tabs[currentTab].Text.rectTransform;
        UiKit.Place(tabUnderline.rectTransform, new Vector2(0f, 1f), new Vector2(tabRect.anchoredPosition.x, -(TabLine - 1f)),
            new Vector2(tabRect.sizeDelta.x, 2f));

        foreach (Transform child in rowsRoot)
            Destroy(child.gameObject);
        rows.Clear();
        tabs[currentTab].Build(this);
        selectedRow = 0;
        RefreshRows();
    }

    private void BuildAudio()
    {
        AddSlider("Genel ses", () => GameSettings.MasterVolume, GameSettings.SetMasterVolume);
        AddSlider("Müzik", () => GameSettings.MusicVolume, GameSettings.SetMusicVolume);
        AddSlider("Efektler", () => GameSettings.EffectsVolume, GameSettings.SetEffectsVolume);
        AddSlider("Ortam sesleri", () => GameSettings.AmbientVolume, GameSettings.SetAmbientVolume);
    }

    private void BuildControls()
    {
        AddSlider("Fare hassasiyeti", () => GameSettings.Sensitivity, GameSettings.SetSensitivity);
        AddChoice("Y eksenini ters çevir", () => 2, i => OffOn[i], () => GameSettings.InvertY ? 1 : 0, i => GameSettings.SetInvertY(i == 1));
    }

    private void BuildDisplay()
    {
        AddChoice("Ekran modu", () => 2, i => i == 0 ? "Tam ekran" : "Pencere", () => GameSettings.Fullscreen ? 0 : 1,
            i => GameSettings.SetFullscreen(i == 0));
        AddChoice("Çözünürlük", () => GameSettings.Resolutions.Count,
            i => $"{GameSettings.Resolutions[i].x} × {GameSettings.Resolutions[i].y}",
            () => GameSettings.ResolutionIndex, GameSettings.SetResolutionIndex);
        AddSlider("Parlaklık", () => GameSettings.Brightness, GameSettings.SetBrightness);
        AddChoice("Dikey senkronizasyon", () => 2, i => OffOn[i], () => GameSettings.VSync ? 1 : 0, i => GameSettings.SetVSync(i == 1));
    }

    private RectTransform CreateRowBase(string label, out TextMeshProUGUI labelText)
    {
        int index = rows.Count;
        RectTransform rect = UiKit.CreateRect("Satir_" + label, rowsRoot);
        UiKit.Place(rect, new Vector2(0f, 1f), new Vector2(0f, -index * RowHeight), new Vector2(Width, RowHeight));

        Image hit = rect.gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        MenuRowEvents events = rect.gameObject.AddComponent<MenuRowEvents>();
        events.Hover = () => SelectRow(index);

        Image line = UiKit.CreateImage("Ayirici", rect, UiKit.Divider);
        UiKit.Place(line.rectTransform, Vector2.zero, Vector2.zero, new Vector2(Width, 1f));

        labelText = UiKit.CreateText("Etiket", rect, label, UiKit.FontKind.Regular, 22.5f, 0f, UiKit.Unselected,
            TextAlignmentOptions.MidlineLeft);
        UiKit.Place(labelText.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(500f, RowHeight));
        return rect;
    }

    private void AddSlider(string label, Func<int> get, Action<int> set)
    {
        RectTransform rect = CreateRowBase(label, out TextMeshProUGUI labelText);
        SliderRow row = new SliderRow { Rect = rect, Label = labelText, Get = get, Set = set };

        RectTransform sliderRect = UiKit.CreateRect("Slider", rect);
        UiKit.Place(sliderRect, new Vector2(0f, 0.5f), new Vector2(SliderLeft, 0f), new Vector2(SliderWidth, 24f));
        Image track = UiKit.CreateImage("Yol", sliderRect, UiKit.White, UiKit.Sprite("Slider_Yol"));
        track.raycastTarget = true; // yola tıklayınca da değer değişsin
        UiKit.Place(track.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(SliderWidth, 3f));

        // Slider dolgu ve tutamacın anchor'larını alanın tam yüksekliğine gerer: dolgu alanı 3 px,
        // tutamaç alanı 0 px yükseklikte (tutamaç kendi 21 px'iyle).
        RectTransform fillArea = UiKit.CreateRect("Dolgu_Alani", sliderRect);
        UiKit.Place(fillArea, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(SliderWidth, 3f));
        Image fill = UiKit.CreateImage("Dolgu", fillArea, UiKit.White, UiKit.Sprite("Slider_Dolgu"));
        RectTransform fillRect = fill.rectTransform;
        fillRect.sizeDelta = Vector2.zero;

        RectTransform handleArea = UiKit.CreateRect("Tutamac_Alani", sliderRect);
        UiKit.Place(handleArea, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(SliderWidth, 0f));
        Image handle = UiKit.CreateImage("Tutamac", handleArea, UiKit.White, UiKit.Sprite("Slider_Tutamac"));
        handle.raycastTarget = true;
        handle.rectTransform.sizeDelta = new Vector2(21f, 21f);

        Slider slider = sliderRect.gameObject.AddComponent<Slider>();
        slider.transition = Selectable.Transition.None;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        slider.fillRect = fillRect;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.minValue = 0f;
        slider.maxValue = 100f;
        slider.wholeNumbers = true;
        slider.onValueChanged.AddListener(v =>
        {
            if (!row.Updating)
                set(Mathf.RoundToInt(v));
        });
        row.Slider = slider;

        row.Value = UiKit.CreateText("Deger", rect, "", UiKit.FontKind.Regular, 22.5f, 0f, UiKit.Unselected,
            TextAlignmentOptions.MidlineRight);
        UiKit.Place(row.Value.rectTransform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(70f, RowHeight));
        rows.Add(row);
    }

    private void AddChoice(string label, Func<int> count, Func<int, string> name, Func<int> get, Action<int> set)
    {
        RectTransform rect = CreateRowBase(label, out TextMeshProUGUI labelText);
        ChoiceRow row = new ChoiceRow { Rect = rect, Label = labelText, Count = count, Name = name, Get = get, Set = set };

        Image left = UiKit.CreateImage("Ok_Sol", rect, UiKit.Unselected, UiKit.Sprite("Ok_Sol"));
        UiKit.Place(left.rectTransform, new Vector2(0f, 0.5f), new Vector2(ChoiceLeft - 12f, 0f), new Vector2(24f, 24f));
        left.raycastTarget = true;
        MakeClickable(left.rectTransform, () => { row.Step(-1); Click(); });

        Image right = UiKit.CreateImage("Ok_Sag", rect, UiKit.Unselected, UiKit.Sprite("Ok_Sag"));
        UiKit.Place(right.rectTransform, new Vector2(0f, 0.5f), new Vector2(ChoiceRight - 12f, 0f), new Vector2(24f, 24f));
        right.raycastTarget = true;
        MakeClickable(right.rectTransform, () => { row.Step(1); Click(); });

        row.Value = UiKit.CreateText("Deger", rect, "", UiKit.FontKind.Medium, 22.5f, 0f, UiKit.White, TextAlignmentOptions.Center);
        float center = (ChoiceLeft + ChoiceRight) * 0.5f;
        UiKit.Place(row.Value.rectTransform, new Vector2(0f, 0.5f), new Vector2(center - 120f, 0f), new Vector2(240f, RowHeight));
        rows.Add(row);
    }

    private void SelectRow(int index)
    {
        if (rows.Count == 0)
            return;
        selectedRow = Mathf.Clamp(index, 0, rows.Count - 1);
        for (int i = 0; i < rows.Count; i++)
            rows[i].Label.color = i == selectedRow ? UiKit.White : UiKit.Unselected;
    }

    private void RefreshRows()
    {
        foreach (Row row in rows)
            row.Refresh();
        SelectRow(selectedRow);
    }

    private void ResetTab()
    {
        Click();
        tabs[currentTab].Reset();
    }

    private void Close()
    {
        Click();
        Action callback = onClose;
        Destroy(gameObject);
        callback?.Invoke();
    }

    private void Click() => GameAudio.PlayUI(clickSound);

    private static void MakeClickable(RectTransform rect, Action action)
    {
        if (rect.GetComponent<Graphic>() == null)
        {
            Image hit = rect.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
        }
        rect.GetComponent<Graphic>().raycastTarget = true;
        MenuRowEvents events = rect.gameObject.AddComponent<MenuRowEvents>();
        events.Click = action;
    }

    private void Update()
    {
        if (UiInput.Back())
        {
            Close();
            return;
        }
        if (UiInput.Reset())
            ResetTab();
        else if (UiInput.NextTab())
        {
            Click();
            ShowTab(currentTab + 1);
        }
        else if (UiInput.PreviousTab())
        {
            Click();
            ShowTab(currentTab - 1);
        }
        else if (UiInput.Up())
            SelectRow(selectedRow - 1);
        else if (UiInput.Down())
            SelectRow(selectedRow + 1);
        else if (UiInput.Left() && rows.Count > 0)
            rows[selectedRow].Step(-1);
        else if (UiInput.Right() && rows.Count > 0)
            rows[selectedRow].Step(1);
    }
}
