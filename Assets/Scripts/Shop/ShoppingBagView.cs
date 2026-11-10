using UnityEngine;

/// <summary>
/// Poşetin görünümü: kamerada sağ altta, sapından tutulur gibi asılı durur, yürürken sallanır. Kraft kâğıt poşet koddan
/// kurulur (modeli yok). <see cref="ShoppingBag"/> ekler ve açar/kapatır; bileşen kapanınca poşet kalkar.
/// </summary>
public class ShoppingBagView : MonoBehaviour
{
    // Kameraya göre sapın tutulduğu nokta: sağ altta, biraz önde.
    private static readonly Vector3 HandlePosition = new Vector3(0.34f, -0.30f, 0.55f);

    private GameObject bag;
    private Transform bagRoot;
    private FirstPersonController controller;
    private float swayPhase;

    private void OnEnable()
    {
        controller = GetComponentInParent<FirstPersonController>();
        Camera cam = controller != null ? controller.GetComponentInChildren<Camera>() : null;
        if (cam == null)
            return;
        bag = Build(FindLitShader());
        bagRoot = bag.transform;
        bagRoot.SetParent(cam.transform, false);
        bagRoot.localPosition = HandlePosition;
        bagRoot.localRotation = Quaternion.Euler(0f, -12f, 0f);
    }

    private void OnDisable()
    {
        if (bag != null)
            Destroy(bag);
        bag = null;
    }

    private void LateUpdate()
    {
        if (bagRoot == null)
            return;
        float speed = controller != null ? controller.HorizontalSpeed : 0f;
        swayPhase += Time.deltaTime * (2.2f + speed * 1.3f);
        float amount = Mathf.Clamp01(speed / 2f);
        // Sapından asılı sarkaç: yürürken öne arkaya ve yana sallanır, dururken hafifçe.
        float roll = Mathf.Sin(swayPhase) * (2f + 7f * amount);
        float pitch = Mathf.Sin(swayPhase * 0.5f + 1f) * (1.5f + 6f * amount);
        bagRoot.localRotation = Quaternion.Euler(pitch, -12f, roll);
    }

    private Shader FindLitShader()
    {
        // Oyuncunun kendi malzemesinin shader'ı (build'e girdiği kesin).
        SkinnedMeshRenderer r = GetComponentInChildren<SkinnedMeshRenderer>(true);
        return r != null && r.sharedMaterial != null ? r.sharedMaterial.shader : Shader.Find("Universal Render Pipeline/Lit");
    }

    // Origin: sapın tutulduğu nokta. Gövde altına sarkar.
    private static GameObject Build(Shader shader)
    {
        GameObject root = new GameObject("Alisveris_Poseti");
        int layer = LayerMask.NameToLayer(SceneFade.ExemptLayerName);
        Material kraft = MakeMaterial(shader, new Color(0.74f, 0.55f, 0.34f));
        Material band = MakeMaterial(shader, new Color(0.93f, 0.9f, 0.84f));
        Material handle = MakeMaterial(shader, new Color(0.62f, 0.44f, 0.26f));

        // Gövde: sap 0.14 m üstte, gövde -0.04'ten -0.34'e.
        Part(root, "Govde", new Vector3(0f, -0.19f, 0f), new Vector3(0.24f, 0.30f, 0.13f), kraft, layer);
        Part(root, "Bant", new Vector3(0f, -0.075f, 0f), new Vector3(0.245f, 0.05f, 0.135f), band, layer);
        // Sap: iki dikey şerit + üst çubuk.
        Part(root, "Sap_Sol", new Vector3(-0.06f, -0.015f, 0f), new Vector3(0.012f, 0.05f, 0.012f), handle, layer);
        Part(root, "Sap_Sag", new Vector3(0.06f, -0.015f, 0f), new Vector3(0.012f, 0.05f, 0.012f), handle, layer);
        Part(root, "Sap_Ust", new Vector3(0f, 0.012f, 0f), new Vector3(0.132f, 0.012f, 0.012f), handle, layer);
        return root;
    }

    private static Material MakeMaterial(Shader shader, Color color)
    {
        Material m = new Material(shader);
        m.SetColor("_BaseColor", color);
        m.SetTexture("_BaseMap", null);
        m.SetFloat("_Smoothness", 0.1f);
        m.SetFloat("_Metallic", 0f);
        return m;
    }

    private static void Part(GameObject root, string name, Vector3 position, Vector3 scale, Material material, int layer)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        Destroy(part.GetComponent<Collider>());
        part.transform.SetParent(root.transform, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        if (layer >= 0)
            part.layer = layer;
        MeshRenderer r = part.GetComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }
}
