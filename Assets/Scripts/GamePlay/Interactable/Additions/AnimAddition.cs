using SDFcl.GamePlay.Interactable;
using UnityEngine;

[System.Serializable]
struct AnimationData
{
    public string StateID;
    public int LayerIndex;
    public float CrossFadeDuration;
}

public class AnimAddition : MonoBehaviour
{
    [SerializeField] Animator animator;

    [Header("Animation Data")]
    [SerializeField] bool IsPlayOnInteract = true;
    [SerializeField] AnimationData animationDataOnInteract;
    [SerializeField] bool IsPlayOnCancelInteract = false;
    [SerializeField] AnimationData animationDataOnCancelInteract;

    Iinteractor interactor;

    private void Awake()
    {
        interactor = GetComponent<Iinteractor>();
    }

    private void OnEnable()
    {
        interactor.OnInteract += HandleInteract;
        interactor.OnEndInteract += HandleCancelInteract;
    }

    private void OnDisable()
    {
        interactor.OnInteract -= HandleInteract;
        interactor.OnEndInteract -= HandleCancelInteract;
    }

    private void HandleInteract(GameObject rootplayer)
    {
        if (IsPlayOnInteract)
        {
            animator.CrossFade(animationDataOnInteract.StateID, animationDataOnInteract.CrossFadeDuration, animationDataOnInteract.LayerIndex);
        }
    }
    private void HandleCancelInteract(GameObject rootplayer)
    {
        if (IsPlayOnCancelInteract)
        {
            animator.CrossFade(animationDataOnCancelInteract.StateID, animationDataOnCancelInteract.CrossFadeDuration, animationDataOnCancelInteract.LayerIndex);
        }
    }
}
