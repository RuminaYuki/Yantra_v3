using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// สั่ง DecalOpacityFader ตอนเข้า/ออก state โดยใช้ค่าเวลา/opacity ของ Action นี้ (ไม่ใช้ค่าบน component)
// Decal มักอยู่บนตัวลูก: ตั้ง Self Anchor เป็น AnchorID ของลูกตัวนั้น (และใส่ลูกไว้ใน Provider ของ Owner)
// ไม่ตั้ง Self Anchor = หาบนตัว Owner เอง ไม่ใส่ RequiresOwnerComponent เพราะมันจะเช็คที่ Owner ไม่ใช่ตัวลูก
[UseSelfAnchor]
[CreateAssetMenu(
    fileName = "DecalOpacityFade_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/VFX/Decal Opacity Fade")]
public class DecalOpacityFadeActionSO : StateActionSO
{
    public enum FadeCommand { None, FadeIn, FadeOut, Reset }

    [Tooltip("ทำอะไรตอนเข้า state")]
    [SerializeField] private FadeCommand _onEnter = FadeCommand.FadeIn;

    [Tooltip("ทำอะไรตอนออก state\nNone = ค้างไว้อย่างนั้น\nReset = คืน material เดิมทันที")]
    [SerializeField] private FadeCommand _onExit = FadeCommand.FadeOut;

    [Header("Time")]
    [Tooltip("ไม่ติ๊ก = ใช้ค่า Delay/Duration/Visible/Hidden บน DecalOpacityFader\nติ๊ก = ใช้ค่าข้างล่างนี้แทน")]
    [SerializeField] private bool _overrideSettings;
    [SerializeField, Min(0f)] private float _delay = 0f;
    [SerializeField, Min(0.01f)] private float _duration = 1f;
    [Tooltip("ค่า Opacity ตอนเห็นชัด")]
    [SerializeField] private float _visibleValue = 1f;
    [Tooltip("ค่า Opacity ตอนจาง")]
    [SerializeField] private float _hiddenValue = 0f;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new DecalOpacityFadeAction(_onEnter, _onExit, _overrideSettings, _delay, _duration, _visibleValue, _hiddenValue);
    }
}

public class DecalOpacityFadeAction : StateAction
{
    private readonly DecalOpacityFadeActionSO.FadeCommand _onEnter;
    private readonly DecalOpacityFadeActionSO.FadeCommand _onExit;
    private readonly bool _overrideSettings;
    private readonly float _delay;
    private readonly float _duration;
    private readonly float _visibleValue;
    private readonly float _hiddenValue;
    private AnchorReference<DecalOpacityFader> _fader;

    public DecalOpacityFadeAction(DecalOpacityFadeActionSO.FadeCommand onEnter, DecalOpacityFadeActionSO.FadeCommand onExit,
        bool overrideSettings, float delay, float duration, float visibleValue, float hiddenValue)
    {
        _onEnter = onEnter;
        _onExit = onExit;
        _overrideSettings = overrideSettings;
        _delay = delay;
        _duration = duration;
        _visibleValue = visibleValue;
        _hiddenValue = hiddenValue;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _fader = new AnchorReference<DecalOpacityFader>(stateMachine, this);
    }

    public override void OnStateEnter() => Run(_onEnter);

    public override void OnStateExit() => Run(_onExit);

    public override void OnUpdate() { }

    private void Run(DecalOpacityFadeActionSO.FadeCommand command)
    {
        if (command == DecalOpacityFadeActionSO.FadeCommand.None || !_fader.TryGet(out DecalOpacityFader fader))
            return;

        switch (command)
        {
            case DecalOpacityFadeActionSO.FadeCommand.FadeIn:
                if (_overrideSettings) fader.Fade(_hiddenValue, _visibleValue, _delay, _duration);
                else fader.FadeIn();
                break;
            case DecalOpacityFadeActionSO.FadeCommand.FadeOut:
                if (_overrideSettings) fader.Fade(_visibleValue, _hiddenValue, _delay, _duration);
                else fader.FadeOut();
                break;
            case DecalOpacityFadeActionSO.FadeCommand.Reset:
                fader.ResetFade();
                break;
        }
    }
}
