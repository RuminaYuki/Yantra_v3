using System.Collections;
using UnityEngine;

// ขึ้น Tutorial กระเป๋าตอนได้ของเข้ากระเป๋าครั้งแรก — ไม่แตะโค้ดกระเป๋า แค่ฟังสัญญาณจาก Inventory
// วางบน Object ไหนในฉากก็ได้ จะหา PlayerInventory เอง / ขึ้นครั้งเดียว (TutorialScreen จำให้ รวมถึงในเซฟ)
public class InventoryTutorialTrigger : MonoBehaviour
{
    [SerializeField] private TutorialDataSO _tutorial;

    [Tooltip("ได้ของแล้วรอกี่วินาทีค่อยขึ้น — ให้ผู้เล่นเห็นของหายเข้ากระเป๋า และ log เก็บของขึ้นก่อน")]
    [SerializeField, Min(0f)] private float _delay = 0.6f;

    [Tooltip("ไม่นับของที่เข้ากระเป๋าช่วงนี้หลังเริ่มฉาก (วินาที)\nตอนโหลดเซฟ / ได้ของติดตัวตอนเริ่ม จะได้ไม่ขึ้น")]
    [SerializeField, Min(0f)] private float _ignoreAtStart = 1f;

    private Inventory _inventory;
    private float _readyTime;
    private float _retryTimer = 1f;
    private int _lastTotal;
    private bool _done;

    private void Start()
    {
        if (_tutorial == null)
        {
            Debug.LogWarning($"[InventoryTutorial] {name} ยังไม่ได้ใส่ Tutorial", this);
            enabled = false;
            return;
        }

        _readyTime = Time.time + _ignoreAtStart;
    }

    // ผู้เล่นอาจเกิดทีหลัง — หาไปเรื่อยๆ จนเจอ
    private void Update()
    {
        if (_inventory != null || _done)
            return;

        _retryTimer += Time.unscaledDeltaTime;
        if (_retryTimer < 0.5f)
            return;
        _retryTimer = 0f;

        PlayerInventory player = GetComponentInParent<PlayerInventory>();
        if (player == null)
            player = FindAnyObjectByType<PlayerInventory>();
        if (player == null || player.Inventory == null)
            return;

        _inventory = player.Inventory;
        _lastTotal = Total();
        _inventory.ItemAdded += HandleInventoryChanged;
        _inventory.ItemChanged += HandleInventoryChanged;
        _inventory.ItemRemoved += HandleInventoryChanged;
    }

    private void OnDestroy() => Unsubscribe();

    // ย้าย / หมุน / รวมกอง จำนวนรวมไม่เปลี่ยน — นับเฉพาะตอนของเพิ่มจริง
    private void HandleInventoryChanged(PlacedItem _)
    {
        int total = Total();
        bool gained = total > _lastTotal;
        _lastTotal = total;

        if (!gained || _done || IsIgnoring())
            return;

        _done = true;
        Unsubscribe();

        if (!TutorialScreen.HasSeen(_tutorial))
            StartCoroutine(ShowAfterDelay());
    }

    private IEnumerator ShowAfterDelay()
    {
        yield return new WaitForSeconds(_delay);
        TutorialScreen.TryShow(_tutorial);
    }

    private bool IsIgnoring()
    {
        if (Time.time < _readyTime)
            return true;
        return SceneLoader.Instance != null && SceneLoader.Instance.IsLoading;
    }

    private int Total()
    {
        int total = 0;
        foreach (PlacedItem p in _inventory.Items)
            total += p.Quantity;
        return total;
    }

    private void Unsubscribe()
    {
        if (_inventory == null)
            return;

        _inventory.ItemAdded -= HandleInventoryChanged;
        _inventory.ItemChanged -= HandleInventoryChanged;
        _inventory.ItemRemoved -= HandleInventoryChanged;
    }
}
