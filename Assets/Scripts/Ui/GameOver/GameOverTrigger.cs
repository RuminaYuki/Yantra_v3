using System.Collections;
using UnityEngine;
using Yantra.UI;

// ฟัง event ตายของผู้เล่นแล้วเปิดหน้า Game Over
// ไม่ผูกกับ Health — วางบน GameObject ไหนก็ได้ (เช่น Manager) แค่ใส่ช่อง On Dead ให้ถูก
public class GameOverTrigger : MonoBehaviour
{
    [Tooltip("PlayerOnDead_VoidEventChannel — ตัวเดียวกับช่อง On Dead ของ Health ผู้เล่น")]
    [SerializeField] private VoidEventChannelSO _onDead;

    private bool _triggered;

    private void OnEnable()
    {
        _triggered = false;

        if (_onDead != null)
            _onDead.Raised += HandleDead;
        else
            Debug.LogWarning("[GameOverTrigger] ไม่มี OnDead channel — หน้า Game Over จะไม่ขึ้น", this);
    }

    private void OnDisable()
    {
        if (_onDead != null) _onDead.Raised -= HandleDead;
    }

    private void HandleDead()
    {
        // กันยิงซ้ำ เผื่อมีอะไรเรียก Kill() ซ้อนกัน
        if (_triggered) return;
        _triggered = true;

        StartCoroutine(ShowAfterDelay());
    }

    private IEnumerator ShowAfterDelay()
    {
        float delay = 1.2f;

        if (UIManager.TryGet<GameOverScreen>(ScreenId.GameOver, out var screen))
            delay = screen.DelayBeforeShow;

        // WaitForSecondsRealtime เพราะอาจมี hitstop ทำให้ timeScale ต่ำอยู่
        yield return new WaitForSecondsRealtime(delay);

        UIManager.Instance?.Open(ScreenId.GameOver);

        // รีเซ็ตหลังเปิดหน้าแล้ว — ถ้า Manager อยู่ข้ามฉาก (DontDestroyOnLoad) OnEnable จะไม่ถูกเรียกอีก
        // ไม่รีเซ็ตตรงนี้ เล่นใหม่แล้วตายรอบสอง หน้า Game Over จะไม่ขึ้น
        _triggered = false;
    }
}