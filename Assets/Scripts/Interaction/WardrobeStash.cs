using System;
using UnityEngine;

/// <summary>
/// Dolaptaki dekor kıyafetler. Bir ürün giyilince onun yerini aldığı eski eşya dolapta birikir
/// (<see cref="after"/> giyilince görünür); <see cref="after"/> boşsa (<c>useTrigger</c> kapalı) baştan durur,
/// dolap boş görünmesin diye. Etkileşimsizdir, collider'ı olmaz.
/// </summary>
public class WardrobeStash : MonoBehaviour
{
    [Serializable]
    private struct Piece
    {
        public Transform model;
        [Tooltip("Kapalıysa eşya baştan dolapta durur.")]
        public bool useTrigger;
        [Tooltip("Bu ürün giyilince eski eşya dolapta belirir.")]
        public ProductType after;
    }

    [SerializeField] private Piece[] pieces;

    private void OnEnable()
    {
        PlayerOutfit.Changed += OnChanged;
        Refresh();
    }

    private void OnDisable() => PlayerOutfit.Changed -= OnChanged;

    private void OnChanged(ProductType type) => Refresh();

    private void Refresh()
    {
        foreach (Piece piece in pieces)
        {
            if (piece.model == null)
                continue;
            bool show = !piece.useTrigger || PlayerOutfit.IsWorn(piece.after);
            if (piece.model.gameObject.activeSelf != show)
                piece.model.gameObject.SetActive(show);
        }
    }
}
