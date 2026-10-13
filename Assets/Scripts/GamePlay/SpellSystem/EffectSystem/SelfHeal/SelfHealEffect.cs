using System.Collections;
using UnityEngine;

public class SelfHealEffect : BaseEffectClass, IEffectExecutor
{
    [Header("Heal")]
    [SerializeField] float healAmount;
    [SerializeField] float OverTime = 0f;

    [SerializeField] private HealOverTime healOverTimePrefab;
    private HealOverTime overTime;

    public void Execute(GameObject owner)
    {
        if (owner != null) base.owner = owner;
        if (base.owner == null)
        {
            Debug.LogError("SelfHealEffect requires an owner.", this);
            return;
        }

        if (useAnimation)
        {
            animEvent = base.owner.GetComponentInChildren<AnimEventDispatcher>();
            if (animEvent == null)
            {
                Debug.LogError("Owner has no AnimEventDispatcher.", base.owner);
                return;
            }
            Animator animator = animEvent.GetComponent<Animator>();
            if (animator == null) return;

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
        IHeal heal = owner.GetComponentInChildren<IHeal>();
        if (heal == null) return;

        if (OverTime > 0f)
        {
            GameObject healOverTimeObject = Instantiate(healOverTimePrefab.gameObject, owner.transform);
            overTime = healOverTimeObject.GetComponent<HealOverTime>();

            overTime.StartHealing(healAmount, OverTime, heal);
        }
        else
        {
            heal.Heal(healAmount);
        }
    }

    private void OnEnable()
    {
        SetEnabled(true);
    }

    private void OnDisable()
    {
        SetEnabled(false);
    }
    private void OnDestroy()
    {
        SetEnabled(false);
    }
}
