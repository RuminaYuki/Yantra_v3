using System;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Health))]
public class LootDropper : MonoBehaviour
{
    [Tooltip("How many times to roll the loot table on death")]
    [SerializeField] int maxLootDrops = 1;

    [Tooltip("Weight for dropping nothing. Higher = more often nothing drops")]
    [SerializeField, Min(0f)] float noDropWeight = 0f;

    [SerializeField] LootItem[] lootTable;

    private Health health;

    private void Awake()
    {
        health = GetComponent<Health>();
        health.OnDead += DropLoot;
    }

    private void OnDestroy()
    {
        if (health != null) health.OnDead -= DropLoot;
    }

    private void DropLoot()
    {
        if (lootTable == null || lootTable.Length == 0) return;

        float totalWeight = noDropWeight;
        foreach (LootItem item in lootTable)
            totalWeight += Mathf.Max(0f, item.weight);

        if (totalWeight <= 0f) return;

        for (int roll = 0; roll < maxLootDrops; roll++)
        {
            LootItem picked = PickWeighted(totalWeight);
            if (picked != null && picked.itemPrefab != null)
                Instantiate(picked.itemPrefab, transform.position, Quaternion.identity);
        }
    }

    // Returns null when the "no drop" slot is picked
    private LootItem PickWeighted(float totalWeight)
    {
        float r = UnityEngine.Random.value * totalWeight;

        foreach (LootItem item in lootTable)
        {
            float w = Mathf.Max(0f, item.weight);
            if (r < w) return item;
            r -= w;
        }

        return null; // landed in noDropWeight
    }
}

[Serializable]
public class LootItem
{
    public GameObject itemPrefab;

    [Tooltip("Relative weight. Chance = weight / (sum of all weights + noDropWeight)")]
    [FormerlySerializedAs("dropChance"), Min(0f)]
    public float weight = 1f;
}
