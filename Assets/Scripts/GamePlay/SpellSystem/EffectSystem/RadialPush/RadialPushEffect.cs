using System.Collections.Generic;
using System.Diagnostics.Tracing;
using UnityEngine;

public class RadialPushEffect : BaseEffectClass, IEffectExecutor
{
    [Header("Setting")]
    [SerializeField] GameObject prefab;
    [SerializeField] List<Spawner> spawners = new();
    [SerializeField] VoidEventChannelSO handleSpawnEvent;

    public void Execute(GameObject owner)
    {
        base.owner = owner;

        Animator animator = animatorAnchor.Value;
        if (animator != null)
        {
            if (!string.IsNullOrEmpty(animationName))
            {
                animator.CrossFade(animationName, 0.2f, layerIndex);
            }
        }

        if (prefab == null)
        {
            Debug.LogWarning(
                "Radial Push requires a prefab.",
                owner);
            return;
        }

        VoidEventChannelSO eventChannel = animator.gameObject.GetComponent<HandleSpawn>().GetEventChannel();
        handleSpawnEvent = eventChannel;
        SetEnabled(true);
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

    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            if (handleSpawnEvent != null)
            {
                handleSpawnEvent.Raised += Shooting;
            }
        }
        else
        {
            if (handleSpawnEvent != null)
            {
                handleSpawnEvent.Raised -= Shooting;
            }
        }

    }

    private void Shooting()
    {
        foreach (Spawner spawn in spawners)
        {
            spawn.spawnObject();
        }
    }
}
