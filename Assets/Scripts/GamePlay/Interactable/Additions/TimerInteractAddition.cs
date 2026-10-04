using SDFcl.GamePlay.Interactable;
using UnityEngine;

public class TimerInteractAddition : MonoBehaviour
{
    [SerializeField] float duration = 2f;   // ล็อกนานกี่วินาที

    Iinteractor _interactor;
    GameObject _player;
    float _timeLeft;                        // เวลาที่เหลือ ถ้ามากกว่า 0 แปลว่ากำลังนับอยู่

    private void Awake()
    {
        _interactor = GetComponent<Iinteractor>();
    }

    private void OnEnable()
    {
        _interactor.OnInteract += StartTimer;
        _interactor.AddCanInteractModifier(IsTimerDone);
        _interactor.AddCanCancelInteractModifier(IsTimerDone);
    }

    private void OnDisable()
    {
        _interactor.OnInteract -= StartTimer;
        _interactor.RemoveCanInteractModifier(IsTimerDone);
        _interactor.RemoveCanCancelInteractModifier(IsTimerDone);
    }

    // BaseInteractor มาถาม: กดได้ไหม / ยกเลิกได้ไหม
    private bool IsTimerDone()
    {
        return _timeLeft <= 0;
    }

    // ผู้เล่นกด interact
    private void StartTimer(GameObject player)
    {
        _player = player;
        _timeLeft = duration;
    }

    private void Update()
    {
        if (_timeLeft <= 0) return;         // ไม่ได้นับอยู่

        _timeLeft -= Time.deltaTime;

        if (_timeLeft <= 0)                 // เพิ่งนับครบในเฟรมนี้
        {
            _interactor.CancelInteraction(_player);
        }
    }
}
