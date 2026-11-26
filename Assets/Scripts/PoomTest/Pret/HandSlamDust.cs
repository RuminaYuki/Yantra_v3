using UnityEngine;

/// <summary>
/// ฝุ่นกระจายตอนมือผีตบพื้น — แปะบนตัวผี (เช่น EnemeyController ของเปรต)
///
/// ไม่ต้องตั้งเวลาตามท่า ไม่ต้องแก้ไฟล์อนิเมชั่น / table YSM:
/// เช็คทุกเฟรมว่ามือกำลังฟาดลงเร็ว ๆ แล้วถึงพื้นหรือยัง — ถึงเมื่อไหร่ เกิดฝุ่นตรงจุดที่มือแตะพื้น
/// ท่าเร็วช้า / มือโดน IK ดึงไปที่ไหน ฝุ่นก็ออกตรงจังหวะเอง
/// </summary>
[DisallowMultipleComponent]
public class HandSlamDust : MonoBehaviour
{
    [Header("โหมด")]
    [Tooltip("ติ๊ก = ไม่จับมือเอง รอ Animation Event \"PretSlam\" ในท่าตบ (ผ่าน PretAnimEvents)\n" +
             "ไม่ติ๊ก = จับเองจากความเร็วมือแบบเดิม")]
    [SerializeField] private bool _useAnimationEvent;

    [Header("มือ")]
    [Tooltip("กระดูกมือที่ตบ — ว่าง = หา hand.l / hand.r ในตัวลูกให้เอง")]
    [SerializeField] private Transform[] _hands;

    [Header("ฝุ่น")]
    [Tooltip("Prefab ฝุ่น (Particle System หรือ VFX Graph) — ด้านบนของ prefab (แกน Y เขียว) จะหันตามพื้น")]
    [SerializeField] private GameObject _dustPrefab;

    [Tooltip("ลบฝุ่นทิ้งหลังกี่วินาที")]
    [SerializeField, Min(0.1f)] private float _dustLifetime = 4f;

    [Tooltip("ขนาดฝุ่น คูณกับขนาดใน prefab")]
    [SerializeField, Min(0.01f)] private float _dustScale = 1f;

    [Header("จับจังหวะตบ")]
    [Tooltip("มือต่ำกว่าพื้นไม่เกินเท่านี้ = นับว่าแตะพื้น (เมตร)\nฝุ่นไม่ออก เพิ่มค่า / ออกตอนมือยังลอย ลดค่า")]
    [SerializeField, Min(0.01f)] private float _touchHeight = 0.35f;

    [Tooltip("มือต้องฟาดลงเร็วอย่างน้อยเท่านี้ (เมตร/วินาที) ถึงนับเป็นตบ\nกันมือที่วางพื้นเฉย ๆ / เดินแล้วมือแกว่งโดนพื้น")]
    [SerializeField, Min(0f)] private float _minSlamSpeed = 2f;

    [Tooltip("ตบซ้ำมือเดิมได้อีกครั้งหลังกี่วินาที")]
    [SerializeField, Min(0f)] private float _cooldown = 0.5f;

    [Tooltip("layer ที่นับเป็นพื้น — อย่าติ๊ก layer ของผี / ผู้เล่น")]
    [SerializeField] private LayerMask _groundLayers = ~0;

    [Tooltip("ใส่ชื่อ state ท่าตบใน Animator (เช่น Attack) = ออกฝุ่นเฉพาะตอนเล่นท่านี้\nว่าง = ออกทุกครั้งที่มือฟาดพื้น")]
    [SerializeField] private string _onlyInState = "";

    [Header("Debug")]
    [SerializeField] private bool _logSlam;

    /// <summary>มือตบพื้น — ส่งจุดที่ตบ (GhostAudio ใช้เล่นเสียงตบ)</summary>
    public event System.Action<Vector3> Slammed;

    private const float RayUp = 1f;          // ยิงหาพื้นจากเหนือมือนิดหน่อย เผื่อมือจมดิน
    private const float RayDown = 3f;

    private Vector3[] _lastPos;
    private float[] _lastSlam;
    private bool[] _hasLast;
    private Animator _animator;
    private int _stateHash;

    private void Awake()
    {
        if (_hands == null || _hands.Length == 0 || System.Array.TrueForAll(_hands, h => h == null))
            _hands = FindHands();

        if (_hands.Length == 0)
            Debug.LogWarning($"[HandSlamDust] {name} หากระดูกมือไม่เจอ — ลาก hand.l / hand.r ใส่ช่อง Hands เอง", this);
        if (_dustPrefab == null)
            Debug.LogWarning($"[HandSlamDust] {name} ยังไม่ได้ใส่ Dust Prefab", this);

        _lastPos = new Vector3[_hands.Length];
        _lastSlam = new float[_hands.Length];
        _hasLast = new bool[_hands.Length];
        for (int i = 0; i < _lastSlam.Length; i++) _lastSlam[i] = -999f;

        if (_useAnimationEvent && GetComponentInChildren<PretAnimEvents>(true) == null)
            Debug.LogWarning($"[HandSlamDust] {name} ติ๊ก Use Animation Event แต่ยังไม่มี PretAnimEvents — แปะที่ตัวที่มี Animator", this);

        _animator = GetComponentInChildren<Animator>();
        _stateHash = string.IsNullOrWhiteSpace(_onlyInState) ? 0 : Animator.StringToHash(_onlyInState.Trim());
    }

    private void OnEnable()
    {
        if (_hasLast != null)
            for (int i = 0; i < _hasLast.Length; i++) _hasLast[i] = false;
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (_useAnimationEvent || dt <= 0f) return;

        for (int i = 0; i < _hands.Length; i++)
        {
            Transform hand = _hands[i];
            if (hand == null) continue;

            Vector3 pos = hand.position;
            if (!_hasLast[i])
            {
                _lastPos[i] = pos;
                _hasLast[i] = true;
                continue;
            }

            float downSpeed = (_lastPos[i].y - pos.y) / dt;   // บวก = กำลังลง
            _lastPos[i] = pos;

            if (downSpeed < _minSlamSpeed) continue;
            if (Time.time - _lastSlam[i] < _cooldown) continue;
            if (!InSlamState()) continue;
            if (!FindGround(pos, out RaycastHit ground)) continue;
            if (pos.y - ground.point.y > _touchHeight) continue;

            _lastSlam[i] = Time.time;
            SpawnDust(ground);
            Slammed?.Invoke(ground.point);

            if (_logSlam)
                Debug.Log($"[HandSlamDust] {hand.name} ตบพื้น ความเร็ว {downSpeed:0.0} ม./วิ ที่ {ground.collider.name}", this);
        }
    }

    /// <summary>
    /// เรียกจาก PretAnimEvents ตอนท่าตบถึงเฟรมมือกระแทกพื้น
    /// ฝุ่นออกทุกมือที่แตะพื้นอยู่ (ตบสองมือ = ฝุ่น 2 กอง) — ไม่มีมือไหนแตะ = ออกใต้มือที่ต่ำที่สุด
    /// </summary>
    public void SlamFromEvent()
    {
        bool any = false;
        Transform lowest = null;

        foreach (Transform hand in _hands)
        {
            if (hand == null) continue;
            if (lowest == null || hand.position.y < lowest.position.y) lowest = hand;

            if (!FindGround(hand.position, out RaycastHit ground)) continue;
            if (hand.position.y - ground.point.y > _touchHeight) continue;

            Hit(hand, ground, !any);
            any = true;
        }

        if (any) return;

        Vector3 from = lowest != null ? lowest.position : transform.position;
        if (FindGround(from, out RaycastHit fallback))
            Hit(lowest, fallback, true);
    }

    private void Hit(Transform hand, RaycastHit ground, bool sendSignal)
    {
        SpawnDust(ground);
        if (sendSignal) Slammed?.Invoke(ground.point);   // เสียงตบออกครั้งเดียวต่อ event

        if (_logSlam)
            Debug.Log($"[HandSlamDust] {(hand != null ? hand.name : name)} ตบพื้น (Animation Event) ที่ {ground.collider.name}", this);
    }

    private bool InSlamState()
    {
        if (_stateHash == 0 || _animator == null) return true;

        for (int layer = 0; layer < _animator.layerCount; layer++)
        {
            if (IsState(_animator.GetCurrentAnimatorStateInfo(layer))) return true;
            if (_animator.IsInTransition(layer) && IsState(_animator.GetNextAnimatorStateInfo(layer))) return true;
        }
        return false;
    }

    private bool IsState(AnimatorStateInfo info) =>
        info.shortNameHash == _stateHash || info.fullPathHash == _stateHash;

    // พื้นใต้มือ — ไม่นับตัวผีเอง
    private bool FindGround(Vector3 handPos, out RaycastHit ground)
    {
        ground = default;
        RaycastHit[] hits = Physics.RaycastAll(handPos + Vector3.up * RayUp, Vector3.down, RayUp + RayDown,
                                               _groundLayers, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        bool found = false;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.normal.y < 0.5f) continue;   // ผนัง / ของชัน ไม่นับเป็นพื้น
            if (hit.distance >= best) continue;
            best = hit.distance;
            ground = hit;
            found = true;
        }
        return found;
    }

    private void SpawnDust(RaycastHit ground)
    {
        if (_dustPrefab == null) return;

        Quaternion rot = Quaternion.FromToRotation(Vector3.up, ground.normal) *
                         Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        GameObject dust = Instantiate(_dustPrefab, ground.point + ground.normal * 0.02f, rot);
        dust.transform.localScale = _dustPrefab.transform.localScale * _dustScale;
        dust.SetActive(true);

        // Particle ที่ไม่ได้ติ๊ก Play On Awake จะไม่เล่นเอง — สั่งเฉพาะตัวบนสุด ตัวลูกเล่นตาม
        foreach (ParticleSystem ps in dust.GetComponentsInChildren<ParticleSystem>(true))
        {
            Transform p = ps.transform.parent;
            bool top = true;
            while (p != null && p != dust.transform.parent)
            {
                if (p.GetComponent<ParticleSystem>() != null) { top = false; break; }
                p = p.parent;
            }
            if (top && !ps.isPlaying) ps.Play(true);
        }

        Destroy(dust, _dustLifetime);
    }

    private Transform[] FindHands()
    {
        var found = new System.Collections.Generic.List<Transform>();
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLowerInvariant();
            if (n == "hand.l" || n == "hand.r" || n == "hand_l" || n == "hand_r" ||
                n.EndsWith(":lefthand") || n.EndsWith(":righthand") || n == "lefthand" || n == "righthand")
                found.Add(t);
        }
        return found.ToArray();
    }

    private void OnDrawGizmosSelected()
    {
        if (_hands == null) return;
        Gizmos.color = new Color(0.8f, 0.6f, 0.3f, 0.8f);
        foreach (Transform h in _hands)
            if (h != null) Gizmos.DrawWireSphere(h.position, 0.08f);
    }
}
