using UnityEngine;

[System.Serializable]
public struct PairedAnimatorState
{
    [Tooltip("ชื่อ state ใน Animator Controller")]
    public string StateName;

    public int Layer;

    [Tooltip("state ที่จะกลับไปตอนออก — ว่างได้ ถ้าใน Animator มีลูกศรออกจากท่านี้อยู่แล้ว")]
    public string ExitStateName;
}

// ข้อมูลของ "ท่า" หนึ่งท่า (ปีน / glory) — เปลี่ยนท่าหรือจูนการเคลื่อนที่ได้ที่ asset นี้ ไม่ต้องแก้โค้ด
[CreateAssetMenu(
    fileName = "NewPaired_Animation",
    menuName = "YUKI Learning State Machine/Paired Animation/Paired Animation Id")]
public class PairedAnimationId : ScriptableObject
{
    [Header("Animator")]
    [Tooltip("ท่าของตัว (RinAnimController) — การเลื่อนตัวจะเดินตามเวลาของท่านี้")]
    [SerializeField] private PairedAnimatorState _body;

    [Tooltip("ท่าของแขน FPS — ว่างได้")]
    [SerializeField] private PairedAnimatorState _arm;

    [SerializeField, Min(0f)] private float _transitionDuration = 0.15f;

    [Header("Movement จากจุดเริ่ม → จุดจบ (แกน X = เวลาท่า 0-1)")]
    [Tooltip("เวลาดูดตัวเข้าจุดเริ่ม (วินาที) — ทำไปพร้อมกับท่าเลย")]
    [SerializeField, Min(0f)] private float _snapDuration = 0.2f;

    [Tooltip("ขึ้นไปแล้วกี่ส่วนของความสูงทั้งหมด")]
    [SerializeField] private AnimationCurve _upCurve = AnimationCurve.EaseInOut(0f, 0f, 0.6f, 1f);

    [Tooltip("ไปข้างหน้าแล้วกี่ส่วนของระยะทั้งหมด")]
    [SerializeField] private AnimationCurve _forwardCurve = AnimationCurve.EaseInOut(0.4f, 0f, 1f, 1f);

    public PairedAnimatorState Body => _body;
    public PairedAnimatorState Arm => _arm;
    public float TransitionDuration => _transitionDuration;
    public float SnapDuration => _snapDuration;
    public AnimationCurve UpCurve => _upCurve;
    public AnimationCurve ForwardCurve => _forwardCurve;
}