using SDFcl.GamePlay.Interactable;
using UnityEngine;

[RequireComponent(typeof(Iinteractor), typeof(IbaseInteractor))]
public class GloryInteractAddition : MonoBehaviour, IPairedInteractionSource
{
    [SerializeField] private PairedAnimationActor _victim;
    [SerializeField] private PairedAnimationId _playerAnimation;
    [SerializeField] private PairedAnimationId _victimAnimation;
    [SerializeField, Min(0f)] private float _distance = 1.2f;

    private Iinteractor _interactor;
    private IbaseInteractor _baseInteractor;
    private PairedAnimationPlayer _activePlayer;   // ผู้เล่นที่กำลังเล่นท่าจาก Interactor ตัวนี้

    private void Awake()
    {
        _interactor = GetComponent<Iinteractor>();
        _baseInteractor = GetComponent<IbaseInteractor>();
    }

    private void OnEnable()
    {
        // รับคำสั่งตอนผู้เล่นกด E
        _interactor.OnInteract += HandleInteract;
        _interactor.AddCanInteractModifier(IsNotPlaying);
        _interactor.AddCanCancelInteractModifier(IsNotPlaying);
    }

    private void OnDisable()
    {
        _interactor.OnInteract -= HandleInteract;
        _interactor.RemoveCanInteractModifier(IsNotPlaying);
        _interactor.RemoveCanCancelInteractModifier(IsNotPlaying);
    }

    // BaseInteractor มาถาม: กดได้ไหม / ยกเลิกได้ไหม
    // ระหว่างเล่นท่า Interactor ขอยกเลิกเองไม่ได้ (เช่น ButtonInteractor ที่ยกเลิกทันทีหลังกด)
    // ท่าจบแล้ว PairedAnimationPlayer ยกเลิกด้วย force จึงไม่ติดตัวนี้
    private bool IsNotPlaying()
    {
        return _activePlayer == null || !_activePlayer.IsPlaying;
    }

    private void HandleInteract(GameObject rootplayer)
    {
        PairedAnimationPlayer player = rootplayer.GetComponentInChildren<PairedAnimationPlayer>();
        if (player == null || _victim == null)
        {
            Debug.LogWarning($"[GloryInteractAddition] {name} หา PairedAnimationPlayer บนผู้เล่น หรือ Victim ไม่เจอ", this);
            return;
        }

        // คำนวณจุดยืนให้ตรงกัน
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

        // ผู้เล่นเล่นท่าเองผ่าน Component (อยู่ใน PlayerInteract_State) แล้วสั่งผีให้เล่นตาม
        // ถ้าเริ่มไม่ได้ PairedAnimationPlayer จะยกเลิก Interact ให้เองในเฟรมถัดไป
        PairedRequest request = PairedRequest
            .InPlace(PairedActionType.Glory, _playerAnimation, playerPosition, playerRotation, _baseInteractor, rootplayer)
            .WithPartner(_victim, _victimAnimation, victimPosition, victimRotation);

        _activePlayer = player;
        player.Play(request);
    }

    public void NotifyStarted(GameObject rootPlayer) { }
}