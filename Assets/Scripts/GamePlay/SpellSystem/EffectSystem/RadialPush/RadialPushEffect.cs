using System.Collections.Generic;
using System.Diagnostics.Tracing;
using UnityEngine;

public class RadialPushEffect : BaseEffectClass, IEffectExecutor
{
    public void Execute(GameObject owner)
    {
        base.owner = owner;

        if (useAnimation)
        {
            GameObject animatorObject = animatorAnchor.Value;
            if (animatorObject == null || !animatorObject.TryGetComponent(out Animator animator)) return;

            animEvent = animatorObject.GetComponent<AnimEventDispatcher>();
            if (animEvent == null) return;

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
