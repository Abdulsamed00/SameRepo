using UnityEngine;

/// <summary>
/// Sahne geçişi bu kimliği istediyse (<see cref="SceneLoader.SpawnId"/>) oyuncuyu bu objenin konumuna ve yönüne taşır.
/// Örn. mağazadan çıkınca sokakta mağaza önünde doğmak için.
/// </summary>
public class SpawnOverride : MonoBehaviour
{
    [SerializeField] private string spawnId;

    private void Start()
    {
        if (SceneLoader.SpawnId != spawnId)
            return;

        SceneLoader.ConsumeSpawnId();
        FirstPersonController player = FindFirstObjectByType<FirstPersonController>();
        if (player == null)
            return;

        CharacterController controller = player.GetComponent<CharacterController>();
        controller.enabled = false;
        player.transform.SetPositionAndRotation(transform.position, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
        controller.enabled = true;
    }
}
