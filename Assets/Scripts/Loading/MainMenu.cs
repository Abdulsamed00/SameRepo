using UnityEngine;

/// <summary>
/// MainMenu sahnesi sadece açılış sahnesidir: ana menü evin içinde, oyuncu kanepede otururken açılır
/// (<see cref="IntroMenu"/>, HouseScene). Burada oyun açılınca doğrudan oraya geçilir.
/// </summary>
public class MainMenu : MonoBehaviour
{
    private void Start() => SceneLoader.Load(SceneLoader.MenuSceneName);
}
