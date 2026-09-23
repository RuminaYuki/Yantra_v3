using UnityEngine;

public class PlayLoopSoundOnAnimatorState : StateMachineBehaviour
{
    [Header("เสียงเปิด (เว้นว่างได้)")]
    [Tooltip("ดังครั้งเดียวตอนเข้า state — เสียงที่บอกคนเล่นว่า 'กดติดแล้ว'\n" +
        "ดังทุกครั้งแม้แตะสั้นๆ เพราะนี่คือเสียงที่ยืนยันว่าอินพุตติด")]
    [SerializeField] private SoundID _enterSound;

    [Tooltip("เว้นอย่างน้อยกี่วินาทีก่อนเล่นเสียงเปิดซ้ำ\n" +
        "กันเสียงรัวตอนผู้เล่นแตะปุ่มถี่ๆ เพื่อจับจังหวะ parry")]
    [SerializeField] private float _enterMinInterval = 0.25f;

    [Header("เสียงลูป (เว้นว่างได้)")]
    [Tooltip("ดังค้างตลอดเวลาที่ยังอยู่ใน state — ฮัมต่ำๆ ของพลังงานที่ทำงานอยู่\n" +
        "ควรเบากว่าเสียงเปิดมาก เพราะมันดังตลอด")]
    [SerializeField] private SoundID _loopSound;

    [Tooltip("ต้องกดค้างอย่างน้อยกี่วินาทีถึงจะเริ่มเสียงลูป\n\n" +
        "แตะสั้นกว่านี้ (เช่นตอนจับจังหวะ parry) จะได้ยินแค่เสียงเปิด\n" +
        "ไม่มีลูป ไม่มีเสียงปิด = ไม่รำคาญ\n" +
        "0 = เริ่มลูปทันที")]
    [SerializeField] private float _minHoldForLoop = 0.3f;

    [Tooltip("หรี่เสียงลูปลงกี่วินาทีตอนหยุด (0 = ตัดทันที)")]
    [SerializeField] private float _loopFadeOut = 0.2f;

    [Header("เสียงปิด (เว้นว่างได้)")]
    [Tooltip("ดังครั้งเดียวตอนออกจาก state\n" +
        "ดังเฉพาะตอนที่เสียงลูปได้เริ่มไปแล้วเท่านั้น — แตะสั้นๆ จะไม่ได้ยิน")]
    [SerializeField] private SoundID _exitSound;

    [Header("จุดกำเนิดเสียง")]
    [Tooltip("ใส่แค่ชื่อกระดูกพอ เช่น Hand_R — ไม่ต้องใส่พาธเต็ม\n" +
        "เว้นว่าง = ใช้ตัวที่มี Animator")]
    [SerializeField] private string _originName = "";

    [Header("Options")]
    [Tooltip("ห่างจาก OnStateUpdate ครั้งก่อนเกินกี่วินาที ถึงนับว่าออกจาก state จริง\n" +
        "สั้นเกิน = เสียงขาดเป็นช่วงๆ / ยาวเกิน = เสียงค้างหลังปล่อยปุ่ม")]
    [SerializeField] private float _graceSeconds = 0.15f;

    [Header("Debug")]
    [SerializeField] private bool _logPlay = false;

    // Unity สร้างสำเนาของ behaviour นี้ให้ Animator แต่ละตัว
    // ตัวแปรข้างในจึงเป็นของใครของมัน ผีหลายตัวไม่กวนกัน
    private LoopSoundKeepAlive _keepAlive;
    private float _lastUpdateTime = -999f;
    private float _inStateSince = -999f;
    private float _lastEnterSoundTime = -999f;

    private Transform _cachedOrigin;
    private Transform _cachedRoot;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // ตั้งใจไม่ทำอะไรตรงนี้ — state ถูกเข้าซ้ำรัวๆ เชื่อไม่ได้
        // ดูคำอธิบายข้อ 1 ด้านบน
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // ตั้งใจไม่ทำอะไรตรงนี้เช่นกัน
        // LoopSoundKeepAlive จะหยุดเสียงเองเมื่อไม่มีใครต่ออายุให้
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        float now = Time.time;

        // ห่างจากครั้งก่อนมาก = เพิ่งเข้า state จริงๆ ไม่ใช่การเข้าซ้ำระหว่างกดค้าง
        bool freshEntry = now - _lastUpdateTime > _graceSeconds;
        _lastUpdateTime = now;

        if (freshEntry)
        {
            _inStateSince = now;
            PlayEnterSound(animator);
        }

        // เสียงลูปทำงานอยู่แล้ว = แค่ต่ออายุ
        if (_keepAlive != null)
        {
            _keepAlive.Ping();
            return;
        }

        if (_loopSound == null && _exitSound == null) return;

        // ยังกดไม่นานพอ — แตะสั้นๆ แบบนี้ไม่ต้องมีลูปและไม่ต้องมีเสียงปิด
        if (now - _inStateSince < _minHoldForLoop) return;

        StartLoop(animator);
    }

    private void PlayEnterSound(Animator animator)
    {
        if (_enterSound == null) return;
        if (SoundManager.Instance == null) return;

        if (_enterMinInterval > 0f && Time.time - _lastEnterSoundTime < _enterMinInterval) return;
        _lastEnterSoundTime = Time.time;

        Transform origin = ResolveOrigin(animator);

        if (_logPlay)
            Debug.Log($"[LoopSound] เปิด — {_enterSound.name} จาก '{origin.name}'", animator);

        SoundManager.Instance.PlaySFXAttached(_enterSound, origin);
    }

    private void StartLoop(Animator animator)
    {
        if (SoundManager.Instance == null) return;

        Transform origin = ResolveOrigin(animator);

        SFXHandle loopHandle = _loopSound != null
            ? SoundManager.Instance.PlayLoopSFXForeverAttached(_loopSound, origin)
            : SFXHandle.None;

        if (!loopHandle.IsValid && _exitSound == null) return;

        if (_logPlay)
        {
            Debug.Log(
                $"[LoopSound] เริ่มลูป — {Name(_loopSound)} (ปิดด้วย {Name(_exitSound)})",
                animator);
        }

        _keepAlive = animator.gameObject.AddComponent<LoopSoundKeepAlive>();
        _keepAlive.Begin(loopHandle, _exitSound, origin, _graceSeconds, _loopFadeOut);
    }

    private static string Name(SoundID id) => id != null ? id.name : "-";

    /// <summary>
    /// StateMachineBehaviour อยู่บน Animator Controller ซึ่งเป็น asset
    /// จึงลากอ้างอิง GameObject ในซีนมาใส่ไม่ได้ ต้องหาเอาตอนรัน
    /// </summary>
    private Transform ResolveOrigin(Animator animator)
    {
        Transform root = animator.transform;

        if (string.IsNullOrEmpty(_originName)) return root;

        if (_cachedOrigin != null && _cachedRoot == root)
            return _cachedOrigin;

        Transform found = root.Find(_originName) ?? FindDeep(root, _originName);

        if (found == null)
        {
            if (_logPlay)
            {
                Debug.LogWarning(
                    $"[LoopSound] หา '{_originName}' ไม่เจอใต้ {animator.name} — ใช้ตัวหลักแทน",
                    animator);
            }
            return root;
        }

        _cachedOrigin = found;
        _cachedRoot = root;
        return found;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == name) return child;

            Transform deeper = FindDeep(child, name);
            if (deeper != null) return deeper;
        }

        return null;
    }
}