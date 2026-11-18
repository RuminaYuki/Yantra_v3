using UnityEngine;

// ซ่อน/โชว์ HUD ตาม flag ของผู้เล่น เช่น แถบ SP โชว์หลังได้ปืนแล้วเท่านั้น
// ใช้ CanvasGroup จางเอา ไม่ปิด GameObject — สคริปต์บนตัวเดียวกัน (เช่น SkillPointBarUI) ยังทำงานต่อ
// StateFlags ไม่มี event ให้ฟัง เลยอ่านเป็นช่วง ๆ แทนการอ่านทุกเฟรม
[RequireComponent(typeof(CanvasGroup))]
public class HUDFlagGate : MonoBehaviour
{
    [Tooltip("flag ที่ใช้ตัดสินว่าจะโชว์ไหม เช่น PlayerEnableGun_Flag")]
    [SerializeField] private FlagSO _flag;

    [Tooltip("โชว์เมื่อ flag เป็นค่านี้")]
    [SerializeField] private bool _showWhen = true;

    [Tooltip("เว้นว่างได้ จะหา StateFlags บนตัวผู้เล่นให้เอง")]
    [SerializeField] private StateFlagsAccess _stateFlags;
    [SerializeField] private string _playerTag = "Player";

    [Tooltip("อ่าน flag ทุกกี่วินาที — 0.25 = วินาทีละ 4 ครั้ง ช้ากว่านี้แถบจะโผล่ช้ากว่าตอนเก็บปืนนิดนึง")]
    [SerializeField, Min(0.05f)] private float _checkInterval = 0.25f;

    [SerializeField] private float _fadeSpeed = 4f;

    private CanvasGroup _group;
    private float _checkTimer;
    private float _target;
    private int _failedSearches;
    private bool _initialized;

    private void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        _group.alpha = 0f;   // ซ่อนไว้ก่อน กันแวบขึ้นมาก่อนอ่าน flag ได้
        _group.interactable = false;
        _group.blocksRaycasts = false;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        _checkTimer -= dt;
        if (_checkTimer <= 0f)
        {
            _checkTimer = _checkInterval;
            ReadFlag();
        }

        if (_group.alpha == _target)
            return;

        _group.alpha = Mathf.MoveTowards(_group.alpha, _target, _fadeSpeed * dt);
    }

    private void ReadFlag()
    {
        if (_flag == null)
        {
            _target = 1f;
            _group.alpha = 1f;
            return;
        }

        if (!TryGetFlags())
            return;

        _target = _stateFlags.Get(_flag) == _showWhen ? 1f : 0f;

        // ครั้งแรกที่อ่านได้ ตั้งค่าทันที ไม่ต้องเฟดตอนเข้าฉาก
        if (!_initialized)
        {
            _initialized = true;
            _group.alpha = _target;
        }
    }

    private bool TryGetFlags()
    {
        if (_stateFlags != null)
            return true;

        // ผู้เล่นติด tag Player หลายตัวลูก ไล่หาจาก root ของแต่ละตัว
        foreach (GameObject obj in GameObject.FindGameObjectsWithTag(_playerTag))
        {
            _stateFlags = obj.transform.root.GetComponentInChildren<StateFlagsAccess>(true);
            if (_stateFlags != null)
                return true;
        }

        _failedSearches++;
        if (_failedSearches * _checkInterval >= 3f && _failedSearches * _checkInterval - _checkInterval < 3f)
            Debug.LogWarning($"[HUDFlagGate] หา StateFlags บนตัวผู้เล่นไม่เจอ — {name} จะซ่อนไว้ก่อน", this);

        return false;
    }
}
