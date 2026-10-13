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
    // TODO(Anchor): GameObjectAnchor was replaced by AnchorIDSO (same asset, so the value is kept).
    // The animator now has to be looked up through the caster's Provider; see the Effect Execute methods.
    // [SerializeField] protected GameObjectAnchor animatorAnchor;
    [SerializeField] protected Yuki.Learning.StateMachine.AnchorIDSO animatorAnchor;
    [SerializeField] protected string animationName;
    [SerializeField] protected int layerIndex;
    [SerializeField] protected string eventKey = string.Empty;
    [SerializeField] protected AnimEventDispatcher animEvent;

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