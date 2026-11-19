using UnityEngine;

// tutorial กระเป๋ากด Tab ปิด → ขอให้ State Machine เปิดกระเป๋าให้ (ไม่เปิดเอง กระเป๋าต้องเปิดผ่าน Inventory_State เท่านั้น)
// ตั้ง flag ไว้ให้ทางเข้า Inventory_State เช็ค / กระเป๋าเปิดแล้วหรือเกินเวลา = ล้าง flag
public class OpenInventoryRequest : MonoBehaviour
{
    [Tooltip("ช่องเดียวกับ On Closed ของ Tutorial กระเป๋า")]
    [SerializeField] private VoidEventChannelSO _requestChannel;

    [Tooltip("flag ที่ทางเข้า Inventory_State เช็ค")]
    [SerializeField] private FlagSO _requestFlag;

    [Tooltip("กระเป๋ายังไม่เปิดภายในเวลานี้ = ยกเลิกคำขอ กันกระเป๋าเด้งเปิดเองทีหลัง")]
    [SerializeField, Min(0.1f)] private float _timeout = 1f;

    [SerializeField] private string _playerTag = "Player";

    private StateFlagsAccess _flags;
    private InventoryUI _ui;
    private float _timer = -1f;

    private void OnEnable()
    {
        if (_requestChannel != null)
            _requestChannel.Raised += HandleRequest;
    }

    private void OnDisable()
    {
        if (_requestChannel != null)
            _requestChannel.Raised -= HandleRequest;
        Clear();
    }

    private void HandleRequest()
    {
        if (_requestFlag == null || !FindReferences())
        {
            Debug.LogWarning("[OpenInventoryRequest] ไม่มี Flag หรือหา StateFlags ของผู้เล่นไม่เจอ — เปิดกระเป๋าไม่ได้", this);
            return;
        }

        _flags.Set(_requestFlag, true);
        _timer = _timeout;
    }

    private void Update()
    {
        if (_timer < 0f)
            return;

        _timer -= Time.unscaledDeltaTime;
        if ((_ui != null && _ui.IsOpen) || _timer <= 0f)
            Clear();
    }

    private void Clear()
    {
        if (_timer < 0f)
            return;

        _timer = -1f;
        if (_flags != null && _requestFlag != null)
            _flags.Set(_requestFlag, false);
    }

    private bool FindReferences()
    {
        if (_ui == null)
            _ui = FindAnyObjectByType<InventoryUI>();

        if (_flags != null)
            return true;

        // ผู้เล่นติด tag Player หลายตัวลูก ไล่หาจาก root ของแต่ละตัว
        foreach (GameObject obj in GameObject.FindGameObjectsWithTag(_playerTag))
        {
            _flags = obj.transform.root.GetComponentInChildren<StateFlagsAccess>(true);
            if (_flags != null)
                return true;
        }
        return false;
    }
}
