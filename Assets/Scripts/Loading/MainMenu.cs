using UnityEngine;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private string gameScene = "HouseScene";
    [SerializeField] private AudioClip clickSound;

    private void Start()
    {
        // Yeni oyun: önceki oyundan (final dahil) kalan gün, alışveriş ve kıyafet durumu sıfırlanır.
        DayRoutine.ResetAll();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void OnPlayClicked()
    {
        GameAudio.Play2D(clickSound);
        SceneLoader.Load(gameScene);
    }

    public void OnQuitClicked()
    {
        GameAudio.Play2D(clickSound);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
