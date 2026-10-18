using System;
using UnityEngine;

/// <summary>
/// F ile açılıp kapanan her şey: kapı, dolap kapağı, çekmece. Her parça kapalı ve açık yerel pozunu bilir;
/// kapı/kapak için menteşe pivotu döndürülür (Euler), çekmece için konum kaydırılır.
/// Collider'lar parçaların altında olduğundan açık kapak da fiziksel olarak engel olur.
/// </summary>
public class Openable : Interactable
{
    [Serializable]
    public class Part
    {
        public Transform target;
        public Vector3 closedPosition;
        public Vector3 closedEuler;
        public Vector3 openPosition;
        public Vector3 openEuler;
    }

    [SerializeField] private string openPrompt = "Aç";
    [SerializeField] private string closePrompt = "Kapat";
    [SerializeField] private bool startsOpen;
    [SerializeField] private float duration = 0.6f;
    [SerializeField] private Part[] parts;

    private bool isOpen;
    private float amount;

    public bool IsOpen => isOpen;

    public override string Prompt => isOpen ? closePrompt : openPrompt;

    private void Awake()
    {
        isOpen = startsOpen;
        amount = isOpen ? 1f : 0f;
        Apply();
    }

    public override void Interact(PlayerInteractor interactor) => isOpen = !isOpen;

    private void Update()
    {
        float goal = isOpen ? 1f : 0f;
        if (Mathf.Approximately(amount, goal))
            return;

        amount = Mathf.MoveTowards(amount, goal, Time.deltaTime / duration);
        Apply();
    }

    private void Apply()
    {
        float t = Mathf.SmoothStep(0f, 1f, amount);
        foreach (Part part in parts)
        {
            part.target.localPosition = Vector3.Lerp(part.closedPosition, part.openPosition, t);
            part.target.localRotation = Quaternion.Slerp(Quaternion.Euler(part.closedEuler), Quaternion.Euler(part.openEuler), t);
        }
    }
}
