using System.Collections.Generic;
using System.Diagnostics.Tracing;
using UnityEngine;

public class RadialPushEffect : BaseEffectClass, IEffectExecutor
{
    public void Execute(GameObject owner)
    {
        if (owner != null) base.owner = owner;
        if (base.owner == null)
        {
            Debug.LogError("RadialPushEffect requires an owner.", this);
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
                animEvent.GetEvent(eventKey).AddListener(Shooting);

            SetEnabled(true);

            if (!string.IsNullOrEmpty(animationName))
            {
                animator.CrossFade(animationName, 0.2f, layerIndex);
            }
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
