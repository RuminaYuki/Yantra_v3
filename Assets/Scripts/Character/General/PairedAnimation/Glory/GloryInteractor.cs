using SDFcl.GamePlay.Interactable;
using UnityEngine;

// วางบนลูกของผี สูงระดับอก (ระบบเล็งวัดจาก pivot ของตัวนี้) — Hold Interact ต้องปิด, Hide Interact ติ๊ก
// ห้ามเขียน Awake ในคลาสนี้ เพราะจะไปบัง Awake ของ BaseInteractor
public class GloryInteractor : BaseInteractor, IPairedInteractionSource
{
    [Header("Glory")]
    [Tooltip("PairedAnimationActor ของผี — ตัวเดียวกับที่มี StateMachineController")]
    [SerializeField] private PairedAnimationActor _victim;

    [SerializeField] private PairedAnimationId _playerAnimation;
    [SerializeField] private PairedAnimationId _victimAnimation;

    [Tooltip("ผู้เล่นยืนห่างผีเท่านี้ตอนเล่นท่า (เมตร)")]
    [SerializeField, Min(0f)] private float _distance = 1.2f;

    private Health _victimHealth;

    private void Start()
    {
        if (_victim == null)
            return;

        // Health ของผีอาจอยู่ตัวแม่หรือตัวลูกของ actor
        _victimHealth = _victim.GetComponentInParent<Health>();
        if (_victimHealth == null)
            _victimHealth = _victim.GetComponentInChildren<Health>();

        if (_victimHealth != null)
            _victimHealth.OnDead += HandleVictimDead;
    }

    private void OnDestroy()
    {
        if (_victimHealth != null)
            _victimHealth.OnDead -= HandleVictimDead;
    }

    // กด E แล้วแค่ยื่นคำขอให้ผู้เล่น (พร้อมข้อมูลของผี) — State ของผู้เล่นเป็นคนสั่งผีต่อเอง
    public override bool Interact(GameObject rootplayer, bool force = false)
    {
        if (_victim == null || _playerAnimation == null || _victimAnimation == null)
        {
            Debug.LogWarning($"[GloryInteractor] {name} ยังใส่ Victim / Player Animation / Victim Animation ไม่ครบ", this);
            return false;
        }

        if (_victimHealth != null && _victimHealth.IsDead)
            return false;

        PairedAnimationActor player = rootplayer != null
            ? rootplayer.GetComponentInChildren<PairedAnimationActor>()
            : null;

        if (player == null || player.IsBusy || _victim.IsBusy)
            return false;

        // ผีหันหาผู้เล่น ผู้เล่นยืนตรงหน้าผีแล้วหันเข้าหา — ท่าชุดเดียวเลยใช้ได้ไม่ว่าเข้าทางไหน
        Vector3 victimPosition = _victim.transform.position;
        Vector3 toPlayer = player.transform.position - victimPosition;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f)
            toPlayer = Vector3.ProjectOnPlane(_victim.transform.forward, Vector3.up);
        toPlayer.Normalize();

        Quaternion victimRotation = Quaternion.LookRotation(toPlayer);
        Vector3 playerPosition = victimPosition + toPlayer * _distance;
        playerPosition.y = player.transform.position.y;
        Quaternion playerRotation = Quaternion.LookRotation(-toPlayer);

        PairedRequest request = PairedRequest
            .InPlace(PairedActionType.Glory, _playerAnimation, playerPosition, playerRotation, this, rootplayer)
            .WithPartner(_victim, _victimAnimation, victimPosition, victimRotation);

        return player.TryRequest(request);
    }

    // State ของผู้เล่นเรียกตอนเริ่มท่าจริง — base.Interact เช็ค canInteract แล้วยิง OnInteract
    public void NotifyStarted(GameObject rootPlayer) => base.Interact(rootPlayer);

    // ต้องติ๊ก Hide Interact ไว้ ไฟถึงจะดับตอนผีตาย
    private void HandleVictimDead() => SetCanInteract(false);
}