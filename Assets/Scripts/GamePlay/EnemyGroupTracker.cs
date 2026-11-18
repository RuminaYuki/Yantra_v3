using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(BoxCollider))]
public class EnemyGroupTracker : MonoBehaviour
{
    [SerializeField] Health[] enemies;
    [Tooltip("จำนวนศัตรูที่ต้องตายถึงจะถือว่าชนะ ถ้าเป็น 0 = ต้องตายทั้งหมด")]
    [SerializeField] int minCount;

    [Header("Events")]
    public UnityEvent OnPlayerEnter;
    public UnityEvent OnAllEnemiesDefeated;

    // เก็บตัวที่ตายแล้ว ใช้ HashSet กันนับซ้ำ
    private readonly HashSet<Health> deadEnemies = new HashSet<Health>();
    // เก็บ handler ของแต่ละตัวไว้ เพื่อ unsubscribe ได้ถูกตัว
    private readonly Dictionary<Health, Action> handlers = new Dictionary<Health, Action>();

    private bool defeated;
    private bool playerEntered;

    private int TotalCount => enemies != null ? enemies.Length : 0;

    private int RequiredCount
    {
        get
        {
            int total = TotalCount;
            return minCount <= 0 ? total : Mathf.Min(minCount, total);
        }
    }

    private void Awake()
    {
        if (enemies == null) return;

        foreach (var enemy in enemies)
        {
            if (enemy == null) continue;

            var target = enemy; // capture ให้ถูกตัว
            Action handler = () => RegisterDead(target);
            handlers[target] = handler;
            target.OnDead += handler;
        }
    }

    // เช็คใน Start เพื่อให้ script อื่นที่ Awake เสร็จแล้ว (เช่น spawner) ตั้งค่า active เรียบร้อยก่อน
    private void Start()
    {
        CheckEnemyAlive();
    }

    private void OnDestroy()
    {
        foreach (var pair in handlers)
        {
            if (pair.Key != null)
                pair.Key.OnDead -= pair.Value;
        }
        handlers.Clear();
    }

    // เช็คว่า Enemy โดน SetActive(false) ตั้งแต่เริ่มหรือไม่ ถ้าใช่ให้ถือว่าตายแล้ว
    private void CheckEnemyAlive()
    {
        if (enemies == null) return;

        foreach (var enemy in enemies)
        {
            if (enemy == null) continue;

            if (!enemy.gameObject.activeInHierarchy)
                deadEnemies.Add(enemy);
        }
        CheckAllEnemiesDefeated();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (playerEntered) return; // ลบบรรทัดนี้ถ้าอยากให้ยิงทุกครั้งที่เดินเข้า

        if (other.CompareTag("Player"))
        {
            playerEntered = true;
            OnPlayerEnter?.Invoke();
        }
    }

    private void RegisterDead(Health enemy)
    {
        if (!deadEnemies.Add(enemy)) return; // เคยนับแล้ว ข้าม

        CheckAllEnemiesDefeated();
    }

    public void CheckAllEnemiesDefeated()
    {
        if (defeated) return;
        if (TotalCount == 0) return; // ไม่มีศัตรูในกลุ่ม ไม่ต้องยิง event

        if (deadEnemies.Count >= RequiredCount)
        {
            defeated = true;
            OnAllEnemiesDefeated?.Invoke();
        }
    }

    [ContextMenu("Set Up List Enemy")]
    public void SetUpListEnemy()
    {
        // หา enemies ทั้งหมดใน BoxCollider ของ EnemyGroupTracker (รวมตัวที่ปิดอยู่ด้วย)
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        var all = FindObjectsByType<Health>(FindObjectsInactive.Include, FindObjectsSortMode.None);

#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, "Set Up List Enemy");
#endif
        enemies = Array.FindAll(all, enemy => boxCollider.bounds.Contains(enemy.transform.position));
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }
}