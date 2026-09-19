using UnityEngine;

public class BaseEffectClass : MonoBehaviour
{
    [Tooltip("Player root")]
    [SerializeField] protected GameObject owner; 

    [Header("Animator")]
    [SerializeField] protected AnimatorAnchor animatorAnchor;
    [SerializeField] protected string animationName;
    [SerializeField] protected int layerIndex;
}