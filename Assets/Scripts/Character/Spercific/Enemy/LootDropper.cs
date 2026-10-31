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

    [Header("Ground Snap")]
    [Tooltip("Layers treated as ground when placing dropped items")]
    [SerializeField] LayerMask groundMask = ~0;

    [Tooltip("Start the ground raycast this far above the enemy (handles enemies sunk into the ground)")]
    [SerializeField, Min(0f)] float rayStartHeight = 2f;

    [Tooltip("Max distance the ground raycast travels downward")]
    [SerializeField, Min(0f)] float rayDistance = 20f;

    [Tooltip("Extra gap between the item's bottom and the ground")]
    [SerializeField] float groundOffset = 0.02f;

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
            if (picked == null || picked.itemPrefab == null) continue;

            GameObject drop = Instantiate(picked.itemPrefab, transform.position, Quaternion.identity);
            SnapToGround(drop, picked.heightOffset);
        }
    }

    // Places the item so its bottom rests on the ground surface (never below it)
    private void SnapToGround(GameObject drop, float extraHeight)
    {
        if (!TryFindGround(transform.position, out float groundY)) return;

        float bottomY = GetBottomY(drop);
        float shift = groundY + groundOffset + extraHeight - bottomY;
        drop.transform.position += Vector3.up * shift;

        Rigidbody rb = drop.GetComponent<Rigidbody>();
        if (rb != null) rb.position = drop.transform.position;
    }

    private bool TryFindGround(Vector3 origin, out float groundY)
    {
        groundY = origin.y;
        Vector3 start = origin + Vector3.up * rayStartHeight;

        RaycastHit[] hits = Physics.RaycastAll(start, Vector3.down, rayStartHeight + rayDistance,
            groundMask, QueryTriggerInteraction.Ignore);
        if (hits.Length == 0) return false;

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            // Skip the dying enemy's own colliders
            if (hit.collider.transform.IsChildOf(transform)) continue;

            groundY = hit.point.y;
            return true;
        }
        return false;
    }

    // Lowest world Y of the item, using visuals first, then solid colliders, then pivot
    private static float GetBottomY(GameObject drop)
    {
        bool found = false;
        Bounds bounds = default;

        foreach (Renderer r in drop.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (!found) { bounds = r.bounds; found = true; }
            else bounds.Encapsulate(r.bounds);
        }

        if (!found)
        {
            foreach (Collider c in drop.GetComponentsInChildren<Collider>())
            {
                if (c.isTrigger) continue;
                if (!found) { bounds = c.bounds; found = true; }
                else bounds.Encapsulate(c.bounds);
            }
        }

        return found ? bounds.min.y : drop.transform.position.y;
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

    [Tooltip("Extra height above the ground for this item only (added on top of groundOffset)")]
    public float heightOffset = 0f;
}
