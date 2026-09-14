using UnityEngine;

public class SpellEffectSpawner : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private VoidEventChannelSO spellDrawFinished;

    [Header("Spawn")]
    [SerializeField] private GameObject effectPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private bool activateOnSpawn;
    [SerializeField] private bool destroySpawnerAfterSpawn;

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

        Transform origin = spawnPoint != null ? spawnPoint : transform;
        GameObject effectObject = Instantiate(
            effectPrefab,
            origin.position,
            origin.rotation);

        if (activateOnSpawn)
        {
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
                controller.SetOwner(gameObject);
                controller.ActivateFromSpawn();
            }
        }

        if (destroySpawnerAfterSpawn)
        {
            Destroy(gameObject);
        }
    }
}
