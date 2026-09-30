using SDFcl.GamePlay.Interactable;
using UnityEngine;

public class TestAddition : MonoBehaviour
{
    private Iinteractor _interactor;

    private void Awake()
    {
        _interactor = GetComponent<Iinteractor>();
    }

    private void OnEnable()
    {
        _interactor.OnInteract += HandleInteract; 
        _interactor.OnEndInteract += HandleEndInteract; 
    }

    private void OnDisable()
    {
        _interactor.OnInteract -= HandleInteract;
        _interactor.OnEndInteract -= HandleEndInteract;
    }

    private void HandleInteract(GameObject rootplayer)
    {
        Debug.Log("Interact");
    }    
    private void HandleEndInteract(GameObject rootplayer)
    {
        Debug.Log("End Interact");
    }
}
