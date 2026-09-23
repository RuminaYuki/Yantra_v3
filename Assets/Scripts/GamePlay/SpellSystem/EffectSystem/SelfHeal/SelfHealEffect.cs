using UnityEngine;

public class SelfHealEffect : BaseEffectClass, IEffectExecutor
{
    [Header("Heal")]
    [SerializeField] float healAmount;

    public void Execute(GameObject owner)
    {
        base.owner = owner;

        if (useAnimation)
        {
            Animator animator = animatorAnchor.Value;
            if (animator == null) return;

            animEvent = animator.gameObject.GetComponent<AnimEventDispatcher>();
            if (animEvent == null) return;

            if (!string.IsNullOrEmpty(eventKey))
                animEvent.GetEvent(eventKey).AddListener(Heal);

            SetEnabled(true);

            if (!string.IsNullOrEmpty(animationName))
            {
                animator.CrossFade(animationName, 0.2f, layerIndex);
            }
        }
    }

    public override void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            if (animEvent != null)
            {
                animEvent.GetEvent(eventKey).AddListener(Heal);
            }
        }
        else
        {
            if (animEvent != null)
            {
                animEvent.GetEvent(eventKey).RemoveListener(Heal);
            }
        }
    }

    private void Heal()
    {
        IHeal heal = owner.GetComponent<IHeal>();
        if (heal == null) return;

        heal.Heal(healAmount);
    }
}
