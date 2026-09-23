using UnityEngine;

public class PlaySoundOnAnimatorState : StateMachineBehaviour
{
    [Header("เสียง")]
    [SerializeField] private SoundID _sound;

    [Header("จังหวะ")]
    [Tooltip("เล่นตอนคลิปเดินไปกี่ % (0 = เฟรมแรก, 0.3 = 30% ของคลิป)\n" +
        "ท่าต่อยควรอยู่ราว 0.25-0.4 เพราะต้องรอให้ง้างเสร็จก่อน\n" +
        "ท่าที่ออกทันทีอย่างบล็อก ใช้ 0 ได้เลย")]
    [Range(0f, 0.95f)]
    [SerializeField] private float _triggerAt = 0.3f;

    [Header("Options")]
    [Tooltip("ให้เสียงวิ่งตามตัวละคร\nเปิดไว้จะดีกว่าสำหรับท่าที่ตัวละครขยับระหว่างทำ")]
    [SerializeField] private bool _followTarget = true;

    [Tooltip("เล่นซ้ำได้ไหมถ้าคลิปวนรอบ (loop)\n" +
        "ท่าโจมตีไม่วน ติ๊กหรือไม่ติ๊กก็ได้ / ท่าเดินที่วนอยู่ควรปิด")]
    [SerializeField] private bool _retriggerOnLoop = false;

    [Tooltip("เว้นอย่างน้อยกี่วินาทีก่อนเล่นซ้ำ (0 = ไม่กัน)\n\n" +
        "⚠️ ท่าที่กดค้างได้อย่างบล็อก ต้องใส่อย่างน้อย 0.5\n" +
        "เพราะระบบสั่งเข้า state เดิมซ้ำทุกเฟรมตอนกดค้าง ทำให้ OnStateEnter ถูกเรียกรัวๆ\n" +
        "ถ้าไม่กัน เสียงกางโล่จะดังซ้ำ 60 ครั้งต่อวินาที\n\n" +
        "ท่าต่อยที่กดทีละครั้งใส่ 0 ได้ ไม่มีปัญหานี้")]
    [SerializeField] private float _minInterval = 0f;

    [Header("Debug")]
    [SerializeField] private bool _logPlay = false;

    private bool _hasPlayed;
    private int _lastLoopIndex = -1;

    // ⚠️ ห้าม reset ตัวนี้ใน OnStateEnter
    // มันต้องจำข้ามการเข้า state รอบก่อนๆ ถึงจะกันเสียงซ้ำตอน state เข้าซ้ำรัวๆ ได้
    private float _lastPlayTime = -999f;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // Unity สร้างสำเนาของ behaviour นี้ให้ Animator แต่ละตัว
        // ตัวแปรข้างในจึงเป็นของใครของมัน ผีหลายตัวไม่กวนกัน
        _hasPlayed = false;
        _lastLoopIndex = -1;
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (_sound == null) return;

        float progress = stateInfo.normalizedTime;
        int loopIndex = Mathf.FloorToInt(progress);
        float inClip = progress - loopIndex;   // 0..1 ภายในรอบปัจจุบัน

        // คลิปวนรอบใหม่ = ปลดล็อกให้เล่นได้อีกครั้ง (ถ้าเปิดไว้)
        if (_retriggerOnLoop && loopIndex != _lastLoopIndex)
        {
            _lastLoopIndex = loopIndex;
            _hasPlayed = false;
        }

        if (_hasPlayed) return;
        if (inClip < _triggerAt) return;

        _hasPlayed = true;
        Play(animator);
    }

    private void Play(Animator animator)
    {
        if (_minInterval > 0f && Time.time - _lastPlayTime < _minInterval) return;
        _lastPlayTime = Time.time;

        if (_logPlay)
            Debug.Log($"[AnimSound] {_sound.name} @ {_triggerAt:P0}", animator);

        if (SoundManager.Instance == null) return;

        if (_followTarget)
            SoundManager.Instance.PlaySFXAttached(_sound, animator.transform);
        else
            SoundManager.Instance.PlaySFX(_sound, animator.transform.position);
    }
}