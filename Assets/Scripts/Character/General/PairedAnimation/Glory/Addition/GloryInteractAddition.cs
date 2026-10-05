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

    private void Awake()
    {
        _interactor = GetComponent<Iinteractor>();
        _baseInteractor = GetComponent<IbaseInteractor>();
    }

    private void OnEnable()
    {
        // รับคำสั่งตอนผู้เล่นกด E
        _interactor.OnInteract += HandleInteract;
    }

    private void OnDisable()
    {
        _interactor.OnInteract -= HandleInteract;
    }

    private void HandleInteract(GameObject rootplayer)
    {
        PairedAnimationActor player = rootplayer.GetComponentInChildren<PairedAnimationActor>();
        if (player == null || _victim == null) return;

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

        // ส่งคำสั่งไปบอก State Machine ให้เล่นแอนิเมชัน
        PairedRequest request = PairedRequest
            .InPlace(PairedActionType.Glory, _playerAnimation, playerPosition, playerRotation, _baseInteractor, rootplayer)
            .WithPartner(_victim, _victimAnimation, victimPosition, victimRotation);

        if (!player.TryRequest(request))
        {
            _interactor.CancelInteraction(rootplayer);
        }
    }

    public void NotifyStarted(GameObject rootPlayer) { }
}