using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Sokakta hızlı gidiş (sunum/test kolaylığı): tuşa basınca ekran kararır, oyuncu iki noktadan
/// kendisine uzak olanına ışınlanır, ekran aydınlanır. Eve giden sokaktayken mağazanın önüne,
/// mağazanın önündeyken eve giden sokaktaki doğma noktasına gider.
/// </summary>
public class FastTravel : MonoBehaviour
{
    [SerializeField] private Key key = Key.T;
    [Tooltip("Mağazanın önü (oyuncu mağazaya bakar).")]
    [SerializeField] private Transform shopPoint;
    [Tooltip("Eve giden sokaktaki doğma noktası.")]
    [SerializeField] private Transform homePoint;
    [SerializeField] private float fadeDuration = 0.35f;

    private bool busy;

    private void Update()
    {
        if (busy || PauseMenu.IsPaused || Keyboard.current == null || !Keyboard.current[key].wasPressedThisFrame)
            return;

        FirstPersonController player = FindFirstObjectByType<FirstPersonController>();
        // Otururken, poz verirken, incelerken ya da sahne geçişinde çalışmaz.
        if (player == null || !player.enabled || player.Frozen || !player.InputEnabled)
            return;

        Vector3 position = player.transform.position;
        Transform target = Vector3.Distance(position, homePoint.position) < Vector3.Distance(position, shopPoint.position)
            ? shopPoint
            : homePoint;
        StartCoroutine(Travel(player, target));
    }

    private IEnumerator Travel(FirstPersonController player, Transform target)
    {
        busy = true;
        player.InputEnabled = false;
        yield return ScreenFader.FadeOut(fadeDuration);

        CharacterController controller = player.GetComponent<CharacterController>();
        controller.enabled = false;
        player.transform.SetPositionAndRotation(target.position, Quaternion.Euler(0f, target.eulerAngles.y, 0f));
        controller.enabled = true;
        player.SyncLookToTransforms();

        player.InputEnabled = true;
        yield return ScreenFader.FadeIn(fadeDuration);
        busy = false;
    }
}
