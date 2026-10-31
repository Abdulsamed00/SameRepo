using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ESC ile duraklatma (Player prefab'ının kökünde). Zaman ve sesler durur, oyuncunun girdisi kapanır; ekranın
/// bulanık görüntüsü üstünde "DURAKLATILDI", Devam et / Ayarlar / Ana menüye dön ve satış fişi.
/// Sahne geçişinde, uykuda ya da finalde (etkileşim kapalıyken) açılmaz.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [SerializeField] private string menuScene = "MainMenu";
    [SerializeField] private AudioClip clickSound;

    public static bool IsPaused { get; private set; }

    private FirstPersonController controller;
    private PlayerInteractor interactor;
    private readonly List<InputAction> disabledActions = new List<InputAction>();
    private Canvas canvas;
    private CanvasGroup front;
    private MenuList menu;
    private RenderTexture blurred;
    private CursorLockMode cursorLock;
    private bool cursorVisible;
    private bool opening;
    private int openedFrame;

    private void Awake()
    {
        controller = GetComponent<FirstPersonController>();
        interactor = GetComponent<PlayerInteractor>();
    }

    private void OnDestroy()
    {
        if (IsPaused)
            Restore();
    }

    private bool CanPause => !SceneLoader.IsTransitioning && Time.timeScale > 0f
        && interactor != null && interactor.enabled && controller != null && controller.enabled;

    private void Update()
    {
        if (IsPaused || opening)
            return;
        bool pressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
        if (pressed && CanPause)
            StartCoroutine(Pause());
    }

    private IEnumerator Pause()
    {
        opening = true;
        IsPaused = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        cursorLock = Cursor.lockState;
        cursorVisible = Cursor.visible;

        // Oyuncu girdisi: açık olan action'lar kapatılır, devam edince sadece onlar açılır.
        disabledActions.Clear();
        if (controller.InputActions != null)
            foreach (InputAction action in controller.InputActions)
                if (action.enabled)
                {
                    action.Disable();
                    disabledActions.Add(action);
                }

        yield return UiBlur.CaptureScreen(rt => blurred = rt);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        UiKit.EnsureEventSystem();
        Build();
        openedFrame = Time.frameCount;
        opening = false;
    }

    private void Build()
    {
        canvas = UiKit.CreateCanvas("Duraklatma_UI", 100);
        Transform root = canvas.transform;

        UnityEngine.UI.RawImage blur = UiKit.CreateRect("Bulanik", root).gameObject.AddComponent<UnityEngine.UI.RawImage>();
        blur.texture = blurred;
        blur.raycastTarget = false;
        UiKit.Stretch(blur.rectTransform);
        UiKit.CreateFullscreen("Karartma", root, UiKit.WithAlpha(UiKit.Dim, 0.5f)).raycastTarget = true;

        RectTransform frontRect = UiKit.CreateRect("On", root);
        UiKit.Stretch(frontRect);
        front = frontRect.gameObject.AddComponent<CanvasGroup>();

        RectTransform left = UiKit.CreateRect("Sol", frontRect);
        UiKit.Place(left, new Vector2(0f, 0.5f), new Vector2(144f, 0f), new Vector2(700f, 0f));

        TextMeshProUGUI title = UiKit.CreateText("Baslik", left, "DURAKLATILDI", UiKit.FontKind.Light, 33f, 45f, UiKit.White,
            TextAlignmentOptions.MidlineLeft);
        UiKit.Place(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 132f), new Vector2(800f, 44f));

        menu = MenuList.Create(left);
        menu.ClickSound = clickSound;
        menu.Add("Devam et", Resume);
        menu.Add("Ayarlar", OpenSettings);
        menu.Add("Ana menüye dön", ReturnToMenu);
        RectTransform menuRect = (RectTransform)menu.transform;
        menuRect.anchorMin = menuRect.anchorMax = new Vector2(0f, 0.5f);
        menuRect.pivot = new Vector2(0f, 1f);
        menuRect.anchoredPosition = new Vector2(0f, 52f);

        RectTransform receipt = ReceiptView.Create(frontRect);
        if (receipt != null)
        {
            receipt.anchorMin = receipt.anchorMax = new Vector2(1f, 1f);
            receipt.pivot = new Vector2(1f, 1f);
            receipt.anchoredPosition = new Vector2(-225f, -144f);
        }
    }

    private void SetFrontVisible(bool visible)
    {
        front.alpha = visible ? 1f : 0f;
        front.blocksRaycasts = visible;
        menu.Interactive = visible;
        openedFrame = Time.frameCount;
    }

    private void OpenSettings()
    {
        SetFrontVisible(false);
        SettingsView.Open(canvas.transform, blurred, clickSound, () => SetFrontVisible(true));
    }

    private void LateUpdate()
    {
        // ESC duraklatmayı kapatır; açan basış ve ayarları kapatan basış sayılmaz.
        if (IsPaused && !opening && menu != null && menu.Interactive && Time.frameCount != openedFrame && UiInput.Back())
        {
            GameAudio.PlayUI(clickSound);
            Resume();
        }
    }

    private void Resume()
    {
        Restore();
        Cursor.lockState = cursorLock;
        Cursor.visible = cursorVisible;
    }

    private void ReturnToMenu()
    {
        Restore();
        SceneLoader.Load(menuScene);
    }

    private void Restore()
    {
        if (canvas != null)
            Destroy(canvas.gameObject);
        if (blurred != null)
        {
            blurred.Release();
            blurred = null;
        }
        foreach (InputAction action in disabledActions)
            action.Enable();
        disabledActions.Clear();
        Time.timeScale = 1f;
        AudioListener.pause = false;
        IsPaused = false;
    }
}
