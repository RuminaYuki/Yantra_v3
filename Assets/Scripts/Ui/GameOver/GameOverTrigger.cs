using System.Collections;
using UnityEngine;
using Yantra.UI;

/// <summary>
/// ฟัง event ตายจาก Health แล้วสั่งเปิดหน้า Game Over
///
/// ไม่แตะไฟล์ Health เลย — subscribe ผ่าน VoidEventChannelSO
/// แปะบน GameObject เดียวกับ Health (Rin/Player)
///
/// แยกจาก GameOverScreen เพราะหน้าจอไม่ควรรู้จักระบบ Health
/// วันหน้าถ้าอยากให้ตายจากเหตุอื่น (ตกเหว, หมดเวลา) ก็แค่เพิ่มตัวเรียกใหม่
/// </summary>
public class GameOverTrigger : MonoBehaviour
{
    [Tooltip("ลาก channel ตัวเดียวกับช่อง On Dead ของ Health\nถ้าเว้นว่างจะดึงจาก Health ให้เอง")]
    [SerializeField] private VoidEventChannelSO _onDead;

    private Health _health;
    private bool _triggered;

    private void Awake()
    {
        _health = GetComponent<Health>();
        if (_onDead == null && _health != null) _onDead = _health.OnDeadEventChannel;
    }

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
    }
}
