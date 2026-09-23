using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class HomingMissileEffect : BaseEffectClass, IEffectExecutor
{

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
