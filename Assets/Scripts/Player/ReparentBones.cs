using System;
using UnityEngine;

/// <summary>
/// Modeldeki yanlış yere bağlı kemikleri oyun başında doğru üst kemiğe taşır (dünya pozu korunur, skinning bozulmaz).
/// AnaKarakter.fbx'te parmak kemikleri (Bone_025/Bone_020) el yerine ön kola bağlı: bilek bükülünce parmaklar
/// geride kalıp el ezilmiş görünüyordu. Model Blender'da düzeltilince bu bileşen kaldırılabilir.
/// </summary>
[DefaultExecutionOrder(-100)]
public class ReparentBones : MonoBehaviour
{
    [Serializable]
    private struct Move
    {
        public string bone;
        public string newParent;
    }

    [SerializeField] private Move[] moves =
    {
        new Move { bone = "Bone_025", newParent = "smartrig:LeftHand" },
        new Move { bone = "Bone_020", newParent = "smartrig:RightHand" },
    };

    private void Awake()
    {
        foreach (Move move in moves)
        {
            Transform bone = FindDeep(transform, move.bone);
            Transform parent = FindDeep(transform, move.newParent);
            if (bone == null || parent == null)
            {
                Debug.LogWarning($"Kemik taşınamadı: {move.bone} -> {move.newParent}", this);
                continue;
            }
            bone.SetParent(parent, true);
        }

        // Animator hiyerarşiyi önbelleğe almış olabilir.
        if (TryGetComponent(out Animator animator))
            animator.Rebind();
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name)
            return root;
        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, name);
            if (found != null)
                return found;
        }
        return null;
    }
}
