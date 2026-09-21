using System.Collections;
using UnityEngine;

public class RandomAmbientSequencer : MonoBehaviour
{
    [Header("เสียงที่จะสุ่ม")]
    [Tooltip("ใส่ได้หลายตัว ระบบจะสุ่มหยิบทีละตัว\nควรมีอย่างน้อย 3-4 ตัว ไม่งั้นจะจำได้เร็ว")]
    [SerializeField] private SoundID[] _sounds;

    [Header("จังหวะ (วินาที)")]
    [Tooltip("เว้นช่วงสั้นสุดก่อนเสียงถัดไป")]
    [SerializeField] private float _minInterval = 12f;

    [Tooltip("เว้นช่วงยาวสุด\nช่วงห่างกันมาก ๆ จะเดาจังหวะไม่ได้ = น่ากลัวกว่า")]
    [SerializeField] private float _maxInterval = 35f;

    [Tooltip("รอกี่วินาทีหลังเข้าฉากก่อนเริ่มเล่นตัวแรก\nกันเสียงดังใส่หน้าทันทีที่โหลดเสร็จ ซึ่งดูจงใจเกินไป")]
    [SerializeField] private float _startDelay = 8f;

    [Header("ตำแหน่ง")]
    [Tooltip("สุ่มตำแหน่งรอบตัวผู้เล่น — ปิดไว้ถ้าอยากให้ออกจากจุดที่วาง object นี้เสมอ")]
    [SerializeField] private bool _positionAroundListener = true;

    [Tooltip("ใกล้สุดกี่เมตรจากผู้เล่น\nอย่าตั้งใกล้เกิน ไม่งั้นจะรู้สึกเหมือนเสียงดังในหัว")]
    [SerializeField] private float _minDistance = 6f;

    [Tooltip("ไกลสุดกี่เมตร\nอย่าเกิน Max Distance ของ SoundData ไม่งั้นจะเงียบจนไม่ได้ยิน")]
    [SerializeField] private float _maxDistance = 18f;

    [Tooltip("สุ่มความสูงขึ้น-ลงได้กี่เมตร\nใส่นิดหน่อยจะรู้สึกมีมิติ ไม่ใช่ทุกเสียงอยู่ระดับหูหมด")]
    [SerializeField] private float _heightVariation = 2f;

    [Header("เงื่อนไข")]
    [Tooltip("หยุดเล่นระหว่างคัตซีน — ควรเปิดไว้ ไม่งั้นเสียงจะไปแย่งบทพูด")]
    [SerializeField] private bool _skipDuringCutscene = true;

    [Tooltip("ห้ามสุ่มได้เสียงเดิมติดกัน 2 ครั้ง\nซ้ำติดกันทำลายความรู้สึกว่ามันเกิดขึ้นเอง")]
    [SerializeField] private bool _avoidRepeat = true;

    [Header("Debug")]
    [Tooltip("ขึ้น log ทุกครั้งที่เล่น บอกว่าเสียงอะไร ห่างจากผู้เล่นเท่าไหร่")]
    [SerializeField] private bool _logPlays = false;

    private Coroutine _routine;
    private int _lastIndex = -1;

    /// <summary>กำลังทำงานอยู่ไหม</summary>
    public bool IsSequencing => _routine != null;

    private void OnEnable()
    {
        if (_sounds == null || _sounds.Length == 0)
        {
#if UNITY_EDITOR
            Debug.LogWarning("[RandomAmbient] ยังไม่ได้ใส่เสียงสักตัว — ตัวนี้จะไม่ทำอะไรเลย", this);
#endif
            return;
        }

        _routine = StartCoroutine(SequenceRoutine());
    }

    private void OnDisable()
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
    }

    /// <summary>หยุดชั่วคราว — เผื่ออยากให้เงียบตอนอยู่ในบ้าน หรือตอนบอสมา</summary>
    public void PauseSequencing()
    {
        if (_routine == null) return;
        StopCoroutine(_routine);
        _routine = null;
    }

    /// <summary>กลับมาทำงานต่อ</summary>
    public void ResumeSequencing()
    {
        if (_routine != null) return;
        if (!isActiveAndEnabled) return;
        _routine = StartCoroutine(SequenceRoutine());
    }

    // ==========================================

    private IEnumerator SequenceRoutine()
    {
        // WaitForSeconds ผูกกับ timeScale — พอเกม pause เสียงบรรยากาศจะหยุดนับตามไปด้วย
        // ถูกแล้ว เพราะเวลาในโลกของเกมหยุดเดินอยู่
        // (ต่างจาก UI ที่ต้องใช้ unscaled เพราะ UI ต้องขยับตอน pause)
        yield return new WaitForSeconds(_startDelay);

        while (true)
        {
            float wait = Random.Range(_minInterval, _maxInterval);
            yield return new WaitForSeconds(wait);

            // ถึงคิวแล้วแต่ติดคัตซีน — รอจนจบ ไม่ทิ้งคิวนี้
            // ทิ้งไปเลยจะทำให้หลังคัตซีนจบเงียบยาวผิดปกติ
            while (_skipDuringCutscene && LevelAudioManager.IsCutsceneActive)
                yield return null;

            PlayOne();
        }
    }

    [ContextMenu("เล่นเสียงเดี๋ยวนี้ (ทดสอบ)")]
    private void PlayOne()
    {
        if (SoundManager.Instance == null) return;

        SoundID sound = PickSound();
        if (sound == null) return;

        Vector3 position = PickPosition();
        SoundManager.Instance.PlaySFX(sound, position);

        if (_logPlays)
        {
            float distance = Vector3.Distance(position, GetListenerPosition());
            Debug.Log($"[RandomAmbient] {sound.name} — ห่าง {distance:F1} ม.", this);
        }
    }

    private SoundID PickSound()
    {
        if (_sounds == null || _sounds.Length == 0) return null;
        if (_sounds.Length == 1) return _sounds[0];

        int index;
        int guard = 0;

        // guard กันลูปไม่รู้จบ เผื่อช่องในลิสต์เป็น null หลายตัว
        do
        {
            index = Random.Range(0, _sounds.Length);
            guard++;
        }
        while (_avoidRepeat && index == _lastIndex && guard < 10);

        _lastIndex = index;
        return _sounds[index];
    }

    private Vector3 PickPosition()
    {
        if (!_positionAroundListener) return transform.position;

        Vector3 center = GetListenerPosition();

        // สุ่มมุมรอบตัว 360 องศา แล้วสุ่มระยะห่าง
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float distance = Random.Range(_minDistance, _maxDistance);

        Vector3 offset = new Vector3(
            Mathf.Cos(angle) * distance,
            Random.Range(-_heightVariation, _heightVariation),
            Mathf.Sin(angle) * distance);

        return center + offset;
    }

    private Vector3 GetListenerPosition()
    {
        Transform listener = AudioListenerCache.Transform;
        if (listener != null) return listener.position;

        if (Camera.main != null) return Camera.main.transform.position;

        return transform.position;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_minInterval < 0.5f) _minInterval = 0.5f;
        if (_maxInterval < _minInterval) _maxInterval = _minInterval;

        if (_minDistance < 0f) _minDistance = 0f;
        if (_maxDistance < _minDistance) _maxDistance = _minDistance;

        if (_startDelay < 0f) _startDelay = 0f;
        if (_heightVariation < 0f) _heightVariation = 0f;
    }

    private void OnDrawGizmosSelected()
    {
        if (!_positionAroundListener) return;

        // วาดวงในวงนอกให้เห็นว่าเสียงจะโผล่มาจากบริเวณไหน
        Vector3 center = Application.isPlaying ? GetListenerPosition() : transform.position;

        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.5f);
        Gizmos.DrawWireSphere(center, _minDistance);

        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.2f);
        Gizmos.DrawWireSphere(center, _maxDistance);
    }
#endif
}