using SDFcl.GamePlay.Interactable;
using UnityEngine;

// วางบนขอบที่ปีนได้ — ต้องตั้ง Hold Interact = ปิด
// ห้ามเขียน Awake ในคลาสนี้ เพราะจะไปบัง Awake ของ BaseInteractor (ที่ซ่อน highlight/focus)
public class ClimbInteractor : BaseInteractor, IPairedInteractionSource
{
    [Header("Climb")]
    [SerializeField] private PairedAnimationId _animation;

    [Tooltip("ตำแหน่งเท้าก่อนปีน — แกน Z (ลูกศรน้ำเงิน) หันเข้าหากำแพง")]
    [SerializeField] private Transform _startPoint;

    [Tooltip("ตำแหน่งเท้าหลังปีนเสร็จ (บนขอบ)")]
    [SerializeField] private Transform _endPoint;

    [Tooltip("ต้องยืนห่างจุดเริ่มไม่เกินนี้ถึงจะปีนได้ — กันกดจากข้างบนแล้ววาร์ปลงมาปีนใหม่")]
    [SerializeField, Min(0f)] private float _maxDistanceFromStart = 1.5f;

    // กด E แล้วแค่ยื่นคำขอ — State Machine ของผู้เล่นเป็นคนตัดสินว่าจะปีนไหม
    public override bool Interact(GameObject rootplayer)
    {
        if (_animation == null || _startPoint == null || _endPoint == null)
        {
            Debug.LogWarning($"[ClimbInteractor] {name} ยังใส่ Animation / Start / End ไม่ครบ", this);
            return false;
        }

        // rootplayer คือ Rin(Player) ซึ่งไม่ขยับ ตัวที่ขยับจริงคือ PlayerController ที่มี actor อยู่
        PairedAnimationActor actor = rootplayer != null
            ? rootplayer.GetComponentInChildren<PairedAnimationActor>()
            : null;

        if (actor == null || actor.IsBusy)
            return false;

        if (Vector3.Distance(actor.transform.position, _startPoint.position) > _maxDistanceFromStart)
            return false;

        return actor.TryRequest(new PairedRequest(
            PairedActionType.Climb,
            _animation,
            _startPoint.position,
            _startPoint.rotation,
            _endPoint.position,
            _endPoint.rotation,
            this,
            rootplayer));
    }

    // State ของผู้เล่นเรียกตอนเริ่มปีนจริง — base.Interact เช็ค canInteract แล้วยิง OnInteract
    public void NotifyStarted(GameObject rootPlayer) => base.Interact(rootPlayer);

    private void OnDrawGizmosSelected()
    {
        if (_startPoint == null || _endPoint == null)
            return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(_startPoint.position, 0.15f);
        Gizmos.DrawRay(_startPoint.position, _startPoint.forward * 0.5f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(_endPoint.position, 0.15f);
        Gizmos.DrawLine(_startPoint.position, _endPoint.position);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(_startPoint.position, _maxDistanceFromStart);
    }
}