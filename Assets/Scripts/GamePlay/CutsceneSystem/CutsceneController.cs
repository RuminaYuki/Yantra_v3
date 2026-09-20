using UnityEngine;
using UnityEngine.Playables;
using System; // จำเป็นต้องใช้สำหรับเรียกใช้งานคลาส Action ในการทำ Global Event

[RequireComponent(typeof(PlayableDirector))]
public class CutsceneController : MonoBehaviour
{
    // หอกระจายข่าว Global Event ให้สคริปต์อื่น (เช่น AI ผี) สามารถสมัครรับฟังได้จากทุกที่ในระบบ โดยไม่ต้องลากมาใส่ Array
    public static event Action<bool> OnGlobalCutsceneStateChanged;

    [Header("Dependencies")]
    [Tooltip("ลากกล้องตัวแม่ที่มีสคริปต์ PlayerCameraController มาใส่")]
    [SerializeField] private PlayerCameraController _cameraController;

    [Tooltip("ลาก GameObject ของผู้เล่นที่มีสคริปต์ Locomotion (และ IMoveLock) มาใส่")]
    [SerializeField] private GameObject _playerObject;

    // ปรับมาใช้ Array ตามที่คุณต้องการ ใส่ UI หรือ System ได้ไม่จำกัด
    [Header("Cutscene Listeners (UI & Systems)")]
    [Tooltip("ลาก GameObject ที่มีระบบรับคำสั่งคัทซีน (เช่น Stamina System หรือ UI) มาใส่ในนี้ได้เลย")]
    [SerializeField] private GameObject[] _cutsceneListeners;

    // สวิตช์สำหรับเปิด-ปิดการใช้ Global Event ผ่าน Inspector (เพื่อความสะดวกในการเลือกใช้งานเป็นรายคัทซีน)
    [Header("Global Settings")]
    [Tooltip("ติ๊กถูกถ้าอยากให้ส่งสัญญาณบอกวัตถุทั้งหมดในฉาก (เช่น ผีทุกตัว) โดยไม่ต้องลากใส่ Array")]
    [SerializeField] private bool _useGlobalEvent = true;

    private PlayableDirector _director;
    private ILocomotionLock _playerMoveLock; // Interface สำหรับล็อกขาผู้เล่น

    private void Awake()
    {
        _director = GetComponent<PlayableDirector>();

        // ค้นหา Interface IMovementLock จากตัวละครผู้เล่น
        if (_playerObject != null)
        {
            _playerMoveLock = _playerObject.GetComponent<ILocomotionLock>();

            if (_playerMoveLock == null)
            {
                Debug.LogWarning("CutsceneController: ไม่พบ IMovementLock บนตัวละครผู้เล่น");
            }
        }
    }

    private void OnEnable()
    {
        // สมัครรับ Event จาก Timeline
        if (_director != null)
        {
            _director.played += OnCutsceneStarted;
            _director.stopped += OnCutsceneEnded;
        }
    }

    private void OnDisable()
    {
        // ยกเลิกรับ Event เพื่อป้องกัน Memory Leak
        if (_director != null)
        {
            _director.played -= OnCutsceneStarted;
            _director.stopped -= OnCutsceneEnded;
        }
    }

    // ฟังก์ชันนี้จะถูกเรียกอัตโนมัติเมื่อ Timeline เริ่มเล่น
    private void OnCutsceneStarted(PlayableDirector director)
    {
        Debug.Log("🎬 Cutscene เริ่ม: ล็อกผู้เล่นและกล้อง");

        // 1. สั่งเปิดโหมดคัทซีนที่กล้อง (กล้อง Gameplay จะหยุดหมุนตามเมาส์)
        if (_cameraController != null) _cameraController.IsCutsceneMode = true;

        // 2. สั่งล็อกขาผู้เล่นผ่าน Interface
        if (_playerMoveLock != null) _playerMoveLock.LockLocomotion(this);

        // 3. ตะโกนบอกทุกคนใน Array ว่าคัทซีนเริ่มแล้ว (ซ่อน UI, หยุดระบบ)
        NotifyListeners(true);

        // ตรวจสอบสวิตช์ Global ถ้าเปิดอยู่ จะกระจายสัญญาณแจ้งเตือนทุกคนที่สมัครรับ Event (ว่าคัทซีนได้เริ่มขึ้นแล้ว)
        if (_useGlobalEvent)
        {
            OnGlobalCutsceneStateChanged?.Invoke(true);
        }
    }

    // ฟังก์ชันนี้จะถูกเรียกอัตโนมัติเมื่อ Timeline เล่นจบ
    private void OnCutsceneEnded(PlayableDirector director)
    {
        Debug.Log("🎬 Cutscene จบ: คืนการควบคุมให้ผู้เล่น");

        // 1. คืนการควบคุมให้กล้อง Gameplay
        if (_cameraController != null) _cameraController.IsCutsceneMode = false;

        // 2. ปลดล็อกขาผู้เล่นให้เดินต่อได้
        if (_playerMoveLock != null) _playerMoveLock.UnlockLocomotion(this);

        // 3. ตะโกนบอกทุกคนใน Array ว่าคัทซีนจบแล้ว (โชว์ UI, ระบบเดินหน้าต่อ)
        NotifyListeners(false);

        // ตรวจสอบสวิตช์ Global ถ้าเปิดอยู่ จะกระจายสัญญาณแจ้งเตือนทุกคนที่สมัครรับ Event (ว่าคัทซีนจบลงแล้ว ให้ทำงานต่อได้)
        if (_useGlobalEvent)
        {
            OnGlobalCutsceneStateChanged?.Invoke(false);
        }
    }

    // ระบบตะโกนสั่งงาน
    private void NotifyListeners(bool isPlaying)
    {
        if (_cutsceneListeners == null) return;

        foreach (GameObject obj in _cutsceneListeners)
        {
            if (obj == null) continue;

            // ค้นหาทุกคนในออบเจกต์ที่มีปลั๊ก ICutsceneListener เสียบอยู่
            ICutsceneListener[] listeners = obj.GetComponents<ICutsceneListener>();
            foreach (var listener in listeners)
            {
                listener.OnCutsceneStateChanged(isPlaying);
            }
        }
    }

    /// <summary>
    /// API สำหรับให้ Trigger (เช่น กล่องชนตอนเดินผ่าน) สั่งเริ่มคัทซีน
    /// </summary>
    public void PlayCutscene()
    {
        if (_director != null) _director.Play();
    }
}