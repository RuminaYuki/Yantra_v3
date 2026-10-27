using System;
using UnityEngine;

[RequireComponent(typeof(Health))]
public class LootDropper : MonoBehaviour
{
    [SerializeField] int maxLootDrops = 1;
    [SerializeField] LootItem[] lootTable;

    private Health health;

    private void Awake()
    {
        health = GetComponent<Health>();
        health.OnDead += DropLoot;
    }

    private void DropLoot()
    {
        int drops = 0;
        for (int i = 0; i < lootTable.Length && drops < maxLootDrops; i++)
        {
            if (UnityEngine.Random.value <= lootTable[i].dropChance)
            {
                Instantiate(lootTable[i].itemPrefab, transform.position, Quaternion.identity);
                drops++;
            }
        }
    }
}

[Serializable]
public class LootItem
{
    public GameObject itemPrefab;
    public float dropChance; // Chance to drop this item (0 to 1)
}