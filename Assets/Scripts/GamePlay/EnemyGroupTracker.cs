using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(BoxCollider))]
public class EnemyGroupTracker : MonoBehaviour
{
    [SerializeField] Health[] enemies;
    [Tooltip("จำนวนศัตรูที่ต้องตายถึงจะถือว่าชนะ ถ้าเป็น 0 = ต้องตายทั้งหมด")]
    [SerializeField] int minCount;

    [Header("Required Loot")]
    [Tooltip("หน่วงเวลาก่อนเริ่มตรวจหาไอเทมเงื่อนไขในห้องหลังศัตรูตายครบ")]
    [SerializeField, Min(0f)] float requiredLootCheckDelay = 0.5f;

    [Header("Barriers")]
    [Tooltip("ล็อคตอนผู้เล่นเดินเข้า ปลดล็อคหลังศัตรูตายครบและไม่มีไอเทมเงื่อนไขเหลือในห้อง\n" +
             "ล็อค = Play ParticleSystem + collider เป็นของแข็ง, ปลดล็อค = Stop + collider เป็น trigger (เดินผ่านได้)\n" +
             "ใช้ ParticleSystem/Collider ทั้งหมดใน object นั้นและลูกของมัน")]
    [SerializeField] GameObject[] barriers;

    [Header("Events")]
    public UnityEvent OnPlayerEnter;
    public UnityEvent OnAllEnemiesDefeated;

    // เก็บตัวที่ตายแล้ว ใช้ HashSet กันนับซ้ำ
    private readonly HashSet<Health> deadEnemies = new HashSet<Health>();
    // เก็บ handler ของแต่ละตัวไว้ เพื่อ unsubscribe ได้ถูกตัว
    private readonly Dictionary<Health, Action> handlers = new Dictionary<Health, Action>();

    private bool defeated;
    private bool roomCompleted;
    private bool playerEntered;

    // อ่านอย่างเดียว ให้ script อื่น (เช่น Debug) เห็นรายชื่อศัตรูกับขอบเขตห้อง
    public IReadOnlyList<Health> Enemies => enemies ?? Array.Empty<Health>();
    public Bounds RoomBounds => GetComponent<BoxCollider>().bounds;

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
        SetBarriersLocked(false); // เริ่มแบบเปิดทางไว้เสมอ ไม่ต้องตั้งค่า barrier ใน scene เอง
        CheckEnemyAlive();
    }

    private void SetBarriersLocked(bool locked)
    {
        if (barriers == null) return;

        foreach (var barrier in barriers)
        {
            if (barrier == null) continue;

            foreach (var particle in barrier.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (locked) particle.Play();
                else particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            foreach (var col in barrier.GetComponentsInChildren<Collider>(true))
                col.isTrigger = !locked;
        }
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

    // นับตัวที่ตายอยู่แล้ว (เช่นตายตอนผู้เล่นยังไม่เข้าห้อง)
    // ข้ามตัวที่ปิดอยู่: ตัวที่ยังไม่เคยเปิด (เช่นโดน EnemyZoneActivator ปิดไว้) Health.Awake ยังไม่ทำงาน
    // HP เลยเป็นค่าที่ save ไว้ใน prefab (0) — ถ้านับ จะกลายเป็นตายทั้งกลุ่มตั้งแต่เริ่ม
    // ตัวที่ตายจริงไม่หลุด: ถูกนับผ่าน OnDead ที่ subscribe ไว้ใน Awake แล้ว (รวมตัวที่ถูกปิดหลังตาย / ตายจาก save)
    private void CheckEnemyAlive()
    {
        if (enemies == null) return;

        foreach (var enemy in enemies)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;

            if (enemy.IsDead)
            {
                Debug.Log($"Enemy {enemy.name} is already dead at start.");
                deadEnemies.Add(enemy);
            }
        }
        CheckAllEnemiesDefeated();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (playerEntered) return; // ลบบรรทัดนี้ถ้าอยากให้ยิงทุกครั้งที่เดินเข้า

        if (other.CompareTag("Player"))
        {
            CheckEnemyAlive();
            if (TotalCount == 0) return; // ไม่มีศัตรูในกลุ่ม ไม่ต้องยิง event

            playerEntered = true;
            if (!roomCompleted)
            {
                SetBarriersLocked(true);
                OnPlayerEnter?.Invoke();
            }
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
            StartCoroutine(WaitForRequiredLootToClear());
        }
    }

    private IEnumerator WaitForRequiredLootToClear()
    {
        Debug.Log("Start");
        yield return new WaitForSeconds(requiredLootCheckDelay);
        Debug.Log("WaitForRequiredLootToClear");

        float checkInterval = Mathf.Max(requiredLootCheckDelay, 0.1f);
        while (HasRequiredLootInRoom())
            yield return new WaitForSeconds(checkInterval);

        roomCompleted = true;
        SetBarriersLocked(false);
        if (playerEntered)
            OnAllEnemiesDefeated?.Invoke();
    }

    private bool HasRequiredLootInRoom()
    {
        Bounds roomBounds = RoomBounds;
        RoomLootRequirement[] lootItems = FindObjectsByType<RoomLootRequirement>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (RoomLootRequirement lootItem in lootItems)
        {
            if (lootItem.gameObject.scene == gameObject.scene &&
                roomBounds.Contains(lootItem.transform.position))
                return true;
        }

        return false;
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