using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// เดินเข้าโซน → ผู้เล่นหยุด กล้องหันไปมองเป้า (เช่น ผี) → เล่นบทพูด → พูดจบแล้วคืนการบังคับ
// ใช้ทำฉาก "รินตะโกนทักผี" — วางโซนนี้ไว้ก่อนถึงระยะที่ผีมองเห็นผู้เล่น
// พูดจบผู้เล่นเดินต่อเข้าไปเอง พอเข้าระยะ ผีจะหันมาสนใจ (Alert) แล้วเดินมาตีตามระบบของผีเอง ไม่ต้องแก้ State Machine
//
// สั่งผ่าน Trigger Zone ของทีม แบบเดียวกับ TutorialTrigger:
// Trigger Zone Events → On Enter → LookAndTalkTrigger.OnEnter (เลือกอันใต้หัวข้อ Dynamic GameObject)
public class LookAndTalkTrigger : MonoBehaviour
{
    [Header("บทพูด")]
    [Tooltip("ไฟล์บทพูด เช่น \"สวัสดีค่ะ ขอถามอะไรหน่อยได้ไหมคะ\" — เว้นว่าง = แค่หันกล้องมอง")]
    [SerializeField] private SubtitleDataSO _dialogue;

    [Header("กล้อง")]
    [Tooltip("สิ่งที่กล้องหันไปมอง — ลากผีใส่")]
    [SerializeField] private Transform _lookTarget;

    [Tooltip("มองสูงจากเป้าขึ้นมาเท่าไหร่ — 1.5 ≈ ระดับหัว (ถ้าลากกระดูกหัวใส่ ให้ตั้ง 0)")]
    [SerializeField] private Vector3 _lookOffset = new Vector3(0f, 1.5f, 0f);

    [Tooltip("ใช้เวลาหันกี่วินาที — มาก = หันช้า นุ่มขึ้น")]
    [SerializeField, Min(0.05f)] private float _turnTime = 0.5f;

    [Header("ผู้เล่น")]
    [Tooltip("ห้ามเดินระหว่างพูด")]
    [SerializeField] private bool _lockMovement = true;

    [Tooltip("พูดจบแล้วค้างไว้อีกกี่วินาทีก่อนคืนการบังคับ")]
    [SerializeField, Min(0f)] private float _holdAfter = 0.4f;

    [Header("Events")]
    [Tooltip("ยิงตอนจบ (คืนการบังคับแล้ว) — ไว้ต่อกับระบบอื่นเพิ่มได้")]
    public UnityEvent OnFinished;

    private PlayerLocomotion _locomotion;
    private PlayerCameraController _camera;
    private bool _movementLocked;
    private bool _looking;
    private bool _playing;

    /// <summary>ต่อกับ Trigger Zone Events → On Enter (Dynamic GameObject) — ส่งตัวผู้เล่นมา</summary>
    public void OnEnter(GameObject who)
    {
        if (_playing || who == null || !isActiveAndEnabled)
            return;

        StartCoroutine(Play(who.transform.root.gameObject));
    }

    private IEnumerator Play(GameObject root)
    {
        _playing = true;
        Hold(root);

        // ให้กล้องหันไปเกือบถึงก่อนค่อยเริ่มพูด
        yield return new WaitForSeconds(_turnTime);

        SubtitleSystem subtitles = SubtitleSystem.Instance;
        if (_dialogue != null && subtitles != null)
        {
            subtitles.PlaySubtitle(_dialogue);

            // Pause / เมนูเปิด ระบบซับหยุดรอเอง — รอแค่จนเล่นจบ (กันค้างไว้ 30 วิ)
            float waited = 0f;
            while (subtitles != null && subtitles.IsPlaying && waited < 30f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        else if (_dialogue != null)
        {
            Debug.LogWarning($"[LookAndTalkTrigger] {name} ไม่มี SubtitleSystem ในซีน — ข้ามบทพูด", this);
        }

        yield return new WaitForSeconds(_holdAfter);

        Release();
        _playing = false;
        OnFinished?.Invoke();
    }

    private void Hold(GameObject root)
    {
        if (_lockMovement)
        {
            _locomotion = root.GetComponentInChildren<PlayerLocomotion>(true);
            if (_locomotion != null)
            {
                _locomotion.LockLocomotion(this);
                _movementLocked = true;
            }
        }

        if (_lookTarget == null)
        {
            Debug.LogWarning($"[LookAndTalkTrigger] {name} ยังไม่ได้ใส่ Look Target — กล้องจะไม่หัน", this);
            return;
        }

        _camera = root.GetComponentInChildren<PlayerCameraController>(true);
        if (_camera == null)
            _camera = FindFirstObjectByType<PlayerCameraController>();

        // ระยะเวลาตั้งยาวไว้ แล้วสั่งหยุดเองตอนพูดจบ
        if (_camera != null)
            _looking = _camera.StartForcedLook(_lookTarget, _lookOffset, 999f, _turnTime * 0.5f);
    }

    private void Release()
    {
        if (_movementLocked && _locomotion != null)
            _locomotion.UnlockLocomotion(this);
        _movementLocked = false;

        if (_looking && _camera != null)
            _camera.StopForcedLook();
        _looking = false;
    }

    // ถูกปิดกลางคัน (เปลี่ยนซีน / ตาย) — คืนการบังคับ ไม่งั้นผู้เล่นค้างเดินไม่ได้
    private void OnDisable()
    {
        Release();
        _playing = false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (_lookTarget == null)
            return;
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.9f);
        Gizmos.DrawLine(transform.position, _lookTarget.position + _lookOffset);
        Gizmos.DrawWireSphere(_lookTarget.position + _lookOffset, 0.25f);
    }
#endif
}
