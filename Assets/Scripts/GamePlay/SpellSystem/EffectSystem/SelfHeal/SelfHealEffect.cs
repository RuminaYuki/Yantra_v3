using UnityEngine;

public class SelfHealEffect : BaseEffectClass, IEffectExecutor
{
    [SerializeField] float healAmount;

    public void Execute(GameObject owner)
    {
        base.owner = owner;

        if (owner == null)
        {
            return;
        }

        Health health = owner.GetComponentInChildren<Health>();
        if (health != null)
        {
            health.Heal(healAmount);
        }

        Animator animator = animatorAnchor.Value;
        if (animator == null) return;

        if (!string.IsNullOrEmpty(animationName))
        {
            Debug.Log("here");
            animator.CrossFade(animationName, 0.2f, layerIndex);
        }
    }
}
