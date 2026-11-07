using UnityEngine;
using UnityEngine.InputSystem;

// ตัวช่วยทดสอบชั่วคราว — กด G ให้ผีจับผู้เล่นทันที ไม่ต้องรอ State ของผี
// แปะบน GameObject เดียวกับ GrabQTEInteractAddition ลบทิ้งได้หลังต่อ State ของผีเสร็จ
[RequireComponent(typeof(GrabQTEInteractAddition))]
public class GrabQTETester : MonoBehaviour
{
    [SerializeField] private Key _key = Key.G;
    [SerializeField] private string _playerTag = "Player";

    [Tooltip("โชว์หลอดทดสอบบนจอ ระหว่างที่ยังไม่ได้ทำ UI จริง")]
    [SerializeField] private bool _showDebugBar = true;

    private GrabQTEInteractAddition _qte;

    private void Awake()
    {
        _qte = GetComponent<GrabQTEInteractAddition>();
    }

    private void OnEnable()
    {
        _qte.OnEscaped.AddListener(LogEscaped);
        _qte.OnFailed.AddListener(LogFailed);
    }

    private void OnDisable()
    {
        _qte.OnEscaped.RemoveListener(LogEscaped);
        _qte.OnFailed.RemoveListener(LogFailed);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current[_key].wasPressedThisFrame)
            StartGrab();
    }

    [ContextMenu("ทดสอบ: ให้ผีจับเลย")]
    private void StartGrab()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[GrabQTETester] ต้องกด Play ก่อน", this);
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag(_playerTag);
        if (player == null)
        {
            Debug.LogWarning($"[GrabQTETester] หาผู้เล่นที่มี tag {_playerTag} ไม่เจอ", this);
            return;
        }

        bool started = _qte.BeginQTE(player.transform.root.gameObject);
        if (started)
            Debug.Log("[GrabQTETester] ผีจับแล้ว กด E รัวๆ", this);
        else
            Debug.LogWarning("[GrabQTETester] เริ่มไม่ได้ — ดูข้อความสีเหลืองอันอื่นใน Console / ผู้เล่นต้องยืนเฉยๆ ไม่เล็งปืน ไม่กัน", this);
    }

    private void LogEscaped() => Debug.Log("[GrabQTETester] หลุดแล้ว", this);
    private void LogFailed() => Debug.Log("[GrabQTETester] แพ้", this);

    private void OnGUI()
    {
        if (!_showDebugBar || _qte == null || !_qte.IsRunning)
            return;

        var back = new Rect(Screen.width * 0.5f - 160f, Screen.height * 0.72f, 320f, 22f);
        var fill = new Rect(back.x, back.y, back.width * _qte.Progress01, back.height);

        Color prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(back, Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.8f, 0.3f, 0.95f);
        GUI.DrawTexture(fill, Texture2D.whiteTexture);
        GUI.color = prev;

        GUI.Label(new Rect(back.x, back.y - 24f, back.width, 22f), "MASH  E   " + Mathf.RoundToInt(_qte.Progress01 * 100f) + "%");
    }
}
