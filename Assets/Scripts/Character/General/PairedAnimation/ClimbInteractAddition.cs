using SDFcl.GamePlay.Interactable;
using UnityEngine;

// วางคู่กับ Interactor อะไรก็ได้ (Hold Interact ต้องปิด) — ปีนผ่าน PairedAnimationPlayer ไม่ต้องมี Climb State ใน Transition Table
// ผู้เล่นต้องมี PairedAnimationActor + PairedAnimationPlayer บน PlayerController
[RequireComponent(typeof(Iinteractor), typeof(IbaseInteractor))]
public class ClimbInteractAddition : MonoBehaviour, IPairedInteractionSource
{
    [SerializeField] private PairedAnimationId _animation;

    [Tooltip("ตำแหน่งเท้าก่อนปีน — แกน Z (ลูกศรน้ำเงิน) หันเข้าหากำแพง")]
    [SerializeField] private Transform _startPoint;

    [Tooltip("ตำแหน่งเท้าหลังปีนเสร็จ (บนขอบ)")]
    [SerializeField] private Transform _endPoint;

    [Tooltip("ต้องยืนห่างจุดเริ่มไม่เกินนี้ถึงจะปีนได้ — กันกดจากข้างบนแล้ววาร์ปลงมาปีนใหม่")]
    [SerializeField, Min(0f)] private float _maxDistanceFromStart = 1.5f;

    private Iinteractor _interactor;
    private IbaseInteractor _baseInteractor;
    private PairedAnimationPlayer _activePlayer;   // ผู้เล่นที่กำลังปีนจาก Interactor ตัวนี้

    private void Awake()
    {
        _interactor = GetComponent<Iinteractor>();
        _baseInteractor = GetComponent<IbaseInteractor>();
    }

    private void OnEnable()
    {
        _interactor.OnInteract += HandleInteract;
        _interactor.AddCanInteractModifier(CanClimb);
        _interactor.AddCanCancelInteractModifier(IsNotPlaying);
    }

    private void OnDisable()
    {
        _interactor.OnInteract -= HandleInteract;
        _interactor.RemoveCanInteractModifier(CanClimb);
        _interactor.RemoveCanCancelInteractModifier(IsNotPlaying);
    }

    // เช็คระยะตรงนี้แทนใน HandleInteract — ถ้าไกลไป Interact จะไม่เริ่มเลย ไม่ต้องตามยกเลิกทีหลัง
    private bool CanClimb(GameObject rootPlayer)
    {
        if (!IsNotPlaying(rootPlayer) || _startPoint == null || rootPlayer == null)
            return false;

        PairedAnimationPlayer player = rootPlayer.GetComponentInChildren<PairedAnimationPlayer>();
        return player != null &&
            Vector3.Distance(player.transform.position, _startPoint.position) <= _maxDistanceFromStart;
    }

    // ท่าจบแล้ว PairedAnimationPlayer ยกเลิกด้วย force จึงไม่ติดตัวนี้
    private bool IsNotPlaying(GameObject rootPlayer)
    {
        return _activePlayer == null || !_activePlayer.IsPlaying;
    }

    private void HandleInteract(GameObject rootplayer)
    {
        PairedAnimationPlayer player = rootplayer.GetComponentInChildren<PairedAnimationPlayer>();
        if (player == null || _animation == null || _startPoint == null || _endPoint == null)
        {
            Debug.LogWarning($"[ClimbInteractAddition] {name} หา PairedAnimationPlayer ไม่เจอ หรือใส่ Animation / Start / End ไม่ครบ", this);
            return;
        }

        // ถ้าเริ่มไม่ได้ PairedAnimationPlayer จะยกเลิก Interact ให้เองในเฟรมถัดไป
        _activePlayer = player;
        player.Play(new PairedRequest(
            PairedActionType.Climb,
            _animation,
            _startPoint.position,
            _startPoint.rotation,
            _endPoint.position,
            _endPoint.rotation,
            _baseInteractor,
            rootplayer));
    }

    // OnInteract ยิงไปแล้วตอนกด ไม่ต้องยิงซ้ำ
    public void NotifyStarted(GameObject rootPlayer) { }

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
