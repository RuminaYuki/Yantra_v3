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
            // TODO(Anchor): disabled so the project builds. Look the animator up through the caster's
            // Provider instead (the owner passed in must be the caster, not the camera/spawn point).
            // GameObject animatorObject = animatorAnchor.Value;
            GameObject animatorObject = null;
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
