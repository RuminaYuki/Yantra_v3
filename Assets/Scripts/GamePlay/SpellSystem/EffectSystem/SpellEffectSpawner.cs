using UnityEngine;

public class SpellEffectSpawner : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private VoidEventChannelSO spellDrawFinished;

    [Header("Spawn")]
    [SerializeField] private GameObject effectPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] bool spawnInMainCamara = false;
    [SerializeField] private bool activateOnSpawn;
    [SerializeField] private bool destroySpawnerAfterSpawn;

    [Header("Runtime Value")]
    [SerializeField] Transform runtimeSpawnPoint;
    [SerializeField] GameObject runtimeOwner;

    private void OnEnable()
    {
        if (spellDrawFinished == null)
        {
            Debug.LogError(
                "SpellEffectSpawner requires a spell draw finished event.",
                this);
            return;
        }

        spellDrawFinished.Raised += SpawnEffect;
    }

    private void OnDisable()
    {
        if (spellDrawFinished != null)
        {
            spellDrawFinished.Raised -= SpawnEffect;
        }
    }

    public void SpawnEffect()
    {
        if (effectPrefab == null)
        {
            Debug.LogError(
                "SpellEffectSpawner requires an effect prefab.",
                this);
            return;
        }

        Transform origin = spawnInMainCamara == true 
            ? Camera.main.transform 
            : runtimeSpawnPoint != null
                ? runtimeSpawnPoint
                : spawnPoint != null
                    ? spawnPoint
                    : transform;
        GameObject effectObject = Instantiate(
            effectPrefab,
            origin.position,
            origin.rotation,
            origin.transform);

        EffectController controller =
            effectObject.GetComponent<EffectController>();

        if (controller == null)
        {
            Debug.LogError(
                "The spawned effect prefab requires an EffectController.",
                effectObject);
        }
        else
        {
            // Owner is the caster (player root), not the spawn origin (may be the camera).
            GameObject owner = runtimeOwner != null
                ? runtimeOwner
                : runtimeSpawnPoint != null
                    ? runtimeSpawnPoint.gameObject
                    : transform.root.gameObject;
            controller.SetOwner(owner);

            if (activateOnSpawn)
            {
                controller.ActivateFromSpawn();
            }
        }

        if (destroySpawnerAfterSpawn)
        {
            Destroy(gameObject);
        }
    }

    public void SetSpawnPoint(Transform point)
    {
        runtimeSpawnPoint = point;
    }

    public void SetOwner(GameObject owner)
    {
        runtimeOwner = owner;
    }
}
