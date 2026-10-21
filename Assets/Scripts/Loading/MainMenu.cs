using UnityEngine;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private string gameScene = "HouseScene";

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void OnPlayClicked()
    {
        SceneLoader.Load(gameScene);
    }

    public void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
