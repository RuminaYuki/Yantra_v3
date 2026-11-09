using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

public class HomingMissileEffect : BaseEffectClass, IEffectExecutor
{
    public void Execute(GameObject owner)
    {
        if (owner != null) base.owner = owner;
        if (base.owner == null)
        {
            Debug.LogError("HomingMissileEffect requires an owner.", this);
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
        base.OnShot += SetBullet;
    }

    private void OnDisable()
    {
        SetEnabled(false);
        base.OnShot -= SetBullet;
    }
    private void OnDestroy()
    {
        SetEnabled(false);
    }

    private void SetBullet(List<GameObject> bullets)
    {
        foreach (GameObject bullet in bullets)
        {
            if (bullet == null) continue;
            HomingMissile homingMissile = bullet.GetComponentInChildren<HomingMissile>();
            if (homingMissile == null)
            {
                Debug.LogWarning("Bullet does not have a HomingMissile component.", bullet);
                continue;
            }
            Debug.Log($"[HomingMissileEffect] Setting owner for bullet {bullet.name} to {base.owner.name}.", this);
            homingMissile.SetOwner(base.owner);
        }
    }
}
