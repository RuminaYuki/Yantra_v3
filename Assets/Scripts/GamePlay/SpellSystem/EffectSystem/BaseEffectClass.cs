using System.Collections.Generic;
using UnityEngine;

public class BaseEffectClass : MonoBehaviour
{
    [Tooltip("Player root")]
    [SerializeField] protected GameObject owner;

    [Header("Spawner")]
    [SerializeField] List<Spawner> spawners = new();

    [Header("Animator")]
    [SerializeField] protected bool useAnimation;
    [SerializeField] protected AnimatorAnchor animatorAnchor;
    [SerializeField] protected string animationName;
    [SerializeField] protected int layerIndex;
    [SerializeField] protected string eventKey = string.Empty;
    [SerializeField] protected AnimEventDispatcher animEvent = new AnimEventDispatcher();

    public virtual void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            if (animEvent != null)
            {
                animEvent.GetEvent(eventKey).AddListener(Shooting);
            }
        }
        else
        {
            if (animEvent != null)
            {
                animEvent.GetEvent(eventKey).RemoveListener(Shooting);
            }
        }
    }

    protected void Shooting()
    {
        foreach (Spawner spawn in spawners)
        {
            spawn.spawnObject();
        }
    }
}