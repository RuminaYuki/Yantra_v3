using System;
using UnityEngine;
using UnityEngine.InputSystem;

public abstract class BaseInteractor : MonoBehaviour, Iinteractor
{
    [Header("Highlight and Focus Objects")]
    [SerializeField] private GameObject highlightObject;
    [SerializeField] private GameObject focusObject;

    [Header("Interaction Settings")]
    [Tooltip("If true, the player can interact with this object.")]
    [SerializeField]private bool canInteract = true;
    [Tooltip("If true, the highlight will be hidden when canInteract is false.")]
    [SerializeField] private bool hideInteract = false;
    [Tooltip("If true, ")]
    [SerializeField] private bool holdInteract = false;

    //if hideInteract is true, CanInteract will always return the value of canInteract, otherwise it will return the value of false
    public bool CanInteract => hideInteract ? canInteract : true;
    public bool HoldInteract => holdInteract;

    public Action<GameObject> OnInteract { get; set; }
    public Action<GameObject> OnEndInteract { get; set; }

    private void Awake()
    {
        //Disable this script if there is no highlight or focus object assigned
        if (highlightObject == null && focusObject == null)
        {
            this.enabled = false;
            return;
        }

        highlightObject.SetActive(false);
        focusObject.SetActive(false);
    }

    public virtual bool Interact(GameObject rootplayer)
    {
        if (!canInteract)
        {
            Debug.LogWarning($"Interactable {this.gameObject.name} is not interactable.");
            return false;
        }

        Debug.Log("Start Interact");

        OnInteract?.Invoke(rootplayer);
        return true;
        //Debug.Log($"Interact input detected{this.gameObject.name}");
    }

    public virtual void OnFocus()
    {
        if (!canInteract) return;
        focusObject.SetActive(true);
        highlightObject.SetActive(false);
    }

    public virtual void OnLoseFocus()
    {
        focusObject.SetActive(false);
        highlightObject.SetActive(true);
    }

    public virtual void ShowHighlight()
    {
        highlightObject.SetActive(true);
        focusObject.SetActive(false);
    }

    public virtual void HideHighlight()
    {
        highlightObject.SetActive(false);
        focusObject.SetActive(false);
    }

    public virtual bool CancelInteraction(GameObject rootplayer)
    {
        Debug.Log("Cancel Interact");
        OnEndInteract?.Invoke(rootplayer);
        return false;
    }

    //API set CanInteract
    public void SetCanInteract(bool value)
    {
        canInteract = value;
    }
}

public interface Iinteractor
{
    bool CanInteract { get; }
    bool HoldInteract {  get; }

    public Action<GameObject> OnInteract { get; set; }
    public Action<GameObject> OnEndInteract { get; set; }

    //Command the object to perform its interaction logic
    bool Interact(GameObject rootplayer);

    //Command the object to show Focus when in Camera forward
    void OnFocus();
    void OnLoseFocus();

    //Command the object to show Highlight when in Highlight range
    void ShowHighlight();
    void HideHighlight();

    bool CancelInteraction(GameObject rootplayer);
}