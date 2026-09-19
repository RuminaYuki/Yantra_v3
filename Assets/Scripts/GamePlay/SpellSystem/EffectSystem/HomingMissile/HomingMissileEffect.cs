using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class HomingMissileEffect : BaseEffectClass, IEffectExecutor
{
    [Header("Setting")]
    //[SerializeField] private GameObject gameObjectPrefab;
    [SerializeField] List<Spawner> spawners = new();
    [SerializeField] VoidEventChannelSO handleSpawnEvent;

    public void Execute(GameObject owner)
    {
        base.owner = owner;

        //if (gameObjectPrefab == null)
        //{
        //    Debug.LogWarning(
        //        "Homing Missile requires a projectile prefab.",
        //        owner);
        //    return;
        //}

        Animator animator = animatorAnchor.Value;
        if (animator == null) return;

        VoidEventChannelSO eventChannel = animator.gameObject.GetComponent<HandleSpawn>().GetEventChannel();
        handleSpawnEvent = eventChannel;
        SetEnabled(true);

        if (!string.IsNullOrEmpty(animationName))
        {
            animator.CrossFade(animationName, 0.2f, layerIndex);
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
