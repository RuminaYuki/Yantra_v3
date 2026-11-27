using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PickUpLogUI : Singleton<PickUpLogUI>
{
    private readonly struct PickUpLogEntry
    {
        public readonly Sprite Icon;
        public readonly string ItemName;
        public readonly int Amount;
        public readonly bool IsMessage;

        public PickUpLogEntry(Sprite icon, string itemName, int amount, bool isMessage = false)
        {
            Icon = icon;
            ItemName = itemName;
            Amount = amount;
            IsMessage = isMessage;
        }
    }

    [Header("PickUp Log UI Settings")]
    [SerializeField] SlotPickUpLog SlotTemplat;
    [Tooltip("Parent object that has the Layout Group. Falls back to this transform if empty.")]
    [SerializeField] Transform logContainer;
    [Tooltip("True: newest log at the top of the layout. False: newest at the bottom.")]
    [SerializeField] bool newestOnTop = true;
    [SerializeField] float displayDuration = 3f;
    [SerializeField] float fadeDuration = 1f;

    [SerializeField, Min(1)] int maxLogCount = 5;

    private readonly Queue<PickUpLogEntry> pendingLogs = new Queue<PickUpLogEntry>();
    private readonly Stack<SlotPickUpLog> pool = new Stack<SlotPickUpLog>();
    private int activeLogCount;

    private Transform Container => logContainer != null ? logContainer : transform;

    public void ShowPickUpLog(Sprite itemIcon, string itemName, int amount)
    {
        EnqueueEntry(new PickUpLogEntry(itemIcon, itemName, amount));
    }

    public void ShowInventoryFullLog()
    {
        EnqueueEntry(new PickUpLogEntry(null, "กระเป๋าเต็ม", 0, true));
    }

    private void EnqueueEntry(PickUpLogEntry entry)
    {
        if (SlotTemplat == null)
        {
            Debug.LogError("Slot prefab is not assigned in PickUpLogUI.");
            return;
        }

        if (maxLogCount < 1)
        {
            Debug.LogError("Max log count must be at least 1.");
            return;
        }

        if (activeLogCount >= maxLogCount)
        {
            pendingLogs.Enqueue(entry);
            return;
        }

        ShowEntry(entry);
    }

    private SlotPickUpLog GetSlot()
    {
        // Reuse a pooled slot if available, skipping any that were destroyed externally
        while (pool.Count > 0)
        {
            SlotPickUpLog pooled = pool.Pop();
            if (pooled != null) return pooled;
        }

        return Instantiate(SlotTemplat, Container);
    }

    private void ReleaseSlot(SlotPickUpLog slot)
    {
        if (slot == null) return;

        // Inactive objects are ignored by the Layout Group, so no gap is left
        slot.gameObject.SetActive(false);
        pool.Push(slot);
    }

    private void ShowEntry(PickUpLogEntry entry)
    {
        SlotPickUpLog slot = GetSlot();
        if (entry.IsMessage) slot.SetMessage(entry.ItemName);
        else slot.SetSlot(entry.Icon, entry.ItemName, entry.Amount);
        slot.SetCanvasGroupAlpha(1f); // Reset alpha left over from the previous fade

        // Layout Group arranges children by sibling order
        if (newestOnTop) slot.transform.SetAsFirstSibling();
        else slot.transform.SetAsLastSibling();

        slot.gameObject.SetActive(true);
        activeLogCount++;
        StartCoroutine(FadeOutAndRelease(slot, displayDuration, fadeDuration));
    }

    private IEnumerator FadeOutAndRelease(SlotPickUpLog slot, float duration, float fadeDuration)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, duration));

        float elapsedTime = 0f;
        float safeFadeDuration = Mathf.Max(0f, fadeDuration);
        while (elapsedTime < safeFadeDuration && slot != null)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsedTime / safeFadeDuration);
            slot.SetCanvasGroupAlpha(alpha);
            yield return null;
        }

        ReleaseSlot(slot);

        activeLogCount--;
        if (pendingLogs.Count > 0)
        {
            ShowEntry(pendingLogs.Dequeue());
        }
    }
}