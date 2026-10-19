using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ใส่ที่ตัวรถแท็กซี่ที่จอดอยู่ในฉาก — พอจอสว่างขึ้นหลังโหลดฉาก รถจะออกตัวไปตามจุดที่วางไว้แล้วหายไป
public class TaxiDriveAway : MonoBehaviour
{
    [Header("เส้นทาง")]
    [Tooltip("GameObject เปล่าที่วางเรียงตามถนน — รถเริ่มจากจุดที่จอดอยู่ แล้ววิ่งผ่านจุดพวกนี้ตามลำดับ\n" +
             "จุดสุดท้ายควรอยู่ในหมอกหรือหลังต้นไม้ ผู้เล่นจะได้ไม่เห็นตอนรถหายไป")]
    [SerializeField] private Transform[] _waypoints = new Transform[0];

    [Tooltip("ติ๊ก = เลี้ยวโค้งนุ่มๆ ผ่านทุกจุด / ไม่ติ๊ก = วิ่งเป็นเส้นตรงหักมุม")]
    [SerializeField] private bool _smoothPath = true;

    [Header("จังหวะ")]
    [Tooltip("รอกี่วินาทีหลังจอสว่างเต็มที่ ก่อนรถออก")]
    [SerializeField, Min(0f)] private float _startDelay = 2f;

    [Tooltip("ความเร็วสูงสุด เมตร/วินาที (12 ≈ 43 กม./ชม.)")]
    [SerializeField, Min(0.1f)] private float _maxSpeed = 12f;

    [Tooltip("เร่งเร็วแค่ไหน — ค่าน้อย = ค่อยๆ ออกตัว")]
    [SerializeField, Min(0.1f)] private float _acceleration = 3f;

    [Tooltip("ความไวในการหักพวงมาลัย — ค่าน้อย = เลี้ยวนุ่มขึ้น")]
    [SerializeField, Min(0.1f)] private float _steering = 5f;

    [Tooltip("ถ้าโมเดลหันหัวไม่ตรงลูกศรสีฟ้า (แกน Z) ให้หมุนชดเชยตรงนี้ เช่น 90 หรือ 180")]
    [SerializeField] private float _yawOffset = 0f;

    [Tooltip("ติ๊ก = ซ่อนรถเมื่อถึงจุดสุดท้าย")]
    [SerializeField] private bool _hideAtEnd = true;

    [Header("ล้อ (ไม่ใส่ก็ได้)")]
    [SerializeField] private Transform[] _wheels = new Transform[0];
    [SerializeField, Min(0.05f)] private float _wheelRadius = 0.35f;
    [Tooltip("แกนที่ล้อหมุน (ของตัวล้อเอง) ปกติคือแกน X")]
    [SerializeField] private Vector3 _wheelAxis = Vector3.right;

    [Header("เสียง (ไม่ใส่ก็ได้)")]
    [Tooltip("AudioSource ที่ใส่เสียงเครื่องยนต์แบบ Loop ไว้")]
    [SerializeField] private AudioSource _engine;
    [Tooltip("เสียงปิดประตูรถ เล่นก่อนออกตัว")]
    [SerializeField] private AudioClip _doorClose;
    [SerializeField, Min(0f)] private float _doorToDriveDelay = 0.8f;
    [SerializeField] private Vector2 _enginePitch = new Vector2(0.8f, 1.5f);
    [Tooltip("เสียงเครื่องยนต์ค่อยๆ เงียบกี่วินาทีก่อนถึงจุดสุดท้าย")]
    [SerializeField, Min(0.1f)] private float _engineFadeTime = 2f;

    private const int SamplesPerSegment = 16;

    private readonly List<Vector3> _points = new List<Vector3>();
    private readonly List<float> _distances = new List<float>();
    private float _length;

    private void Start()
    {
        BuildPath(transform.position, _points, _distances);
        _length = _distances.Count > 0 ? _distances[_distances.Count - 1] : 0f;

        if (_length <= 0.01f)
        {
            Debug.LogWarning("[Taxi] ยังไม่ได้วางจุด Waypoints — รถจะไม่ขยับ", this);
            return;
        }

        if (TryGetComponent(out Rigidbody body))
            body.isKinematic = true;

        StartCoroutine(DriveRoutine());
    }

    private IEnumerator DriveRoutine()
    {
        // รอให้หน้าโหลดกับจอดำหายไปก่อน ผู้เล่นจะได้เห็นรถออกตัว
        while (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading)
            yield return null;

        yield return new WaitForSeconds(_startDelay);

        if (_doorClose != null)
        {
            if (_engine != null)
                _engine.PlayOneShot(_doorClose);
            else
                AudioSource.PlayClipAtPoint(_doorClose, transform.position);
            yield return new WaitForSeconds(_doorToDriveDelay);
        }

        float baseVolume = 1f;
        if (_engine != null)
        {
            baseVolume = _engine.volume;
            if (!_engine.isPlaying)
                _engine.Play();
        }

        float speed = 0f;
        float travelled = 0f;
        int positionCursor = 0;
        int aheadCursor = 0;

        while (travelled < _length)
        {
            float dt = Time.deltaTime;
            speed = Mathf.Min(_maxSpeed, speed + _acceleration * dt);
            travelled = Mathf.Min(_length, travelled + speed * dt);

            Vector3 position = Sample(travelled, ref positionCursor);
            Vector3 ahead = Sample(Mathf.Min(_length, travelled + Mathf.Max(1.5f, speed * 0.4f)), ref aheadCursor);
            transform.position = position;

            Vector3 direction = ahead - position;
            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion target = Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(0f, _yawOffset, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-_steering * dt));
            }

            SpinWheels(speed * dt);

            if (_engine != null)
            {
                _engine.pitch = Mathf.Lerp(_enginePitch.x, _enginePitch.y, speed / _maxSpeed);
                float timeLeft = speed > 0.01f ? (_length - travelled) / speed : _engineFadeTime;
                _engine.volume = baseVolume * Mathf.Clamp01(timeLeft / _engineFadeTime);
            }

            yield return null;
        }

        if (_engine != null)
            _engine.Stop();
        if (_hideAtEnd)
            gameObject.SetActive(false);
    }

    private void SpinWheels(float distance)
    {
        if (_wheels.Length == 0)
            return;
        float degrees = distance / _wheelRadius * Mathf.Rad2Deg;
        foreach (Transform wheel in _wheels)
            if (wheel != null)
                wheel.Rotate(_wheelAxis, degrees, Space.Self);
    }

    // ระยะที่ขอเพิ่มขึ้นเรื่อยๆ เลยเดินหน้า cursor ต่อจากเฟรมก่อนได้ ไม่ต้องค้นใหม่ทุกเฟรม
    private Vector3 Sample(float distance, ref int cursor)
    {
        if (cursor >= _distances.Count - 1 || distance < _distances[cursor])
            cursor = 0;
        while (cursor < _distances.Count - 2 && _distances[cursor + 1] < distance)
            cursor++;

        float segment = _distances[cursor + 1] - _distances[cursor];
        float t = segment > 0.0001f ? (distance - _distances[cursor]) / segment : 0f;
        return Vector3.Lerp(_points[cursor], _points[cursor + 1], t);
    }

    private void BuildPath(Vector3 start, List<Vector3> points, List<float> distances)
    {
        points.Clear();
        distances.Clear();

        var keys = new List<Vector3> { start };
        foreach (Transform waypoint in _waypoints)
            if (waypoint != null)
                keys.Add(waypoint.position);
        if (keys.Count < 2)
            return;

        points.Add(keys[0]);
        for (int i = 0; i < keys.Count - 1; i++)
        {
            if (!_smoothPath || keys.Count < 3)
            {
                points.Add(keys[i + 1]);
                continue;
            }

            Vector3 p0 = keys[Mathf.Max(0, i - 1)];
            Vector3 p1 = keys[i];
            Vector3 p2 = keys[i + 1];
            Vector3 p3 = keys[Mathf.Min(keys.Count - 1, i + 2)];
            for (int s = 1; s <= SamplesPerSegment; s++)
                points.Add(CatmullRom(p0, p1, p2, p3, s / (float)SamplesPerSegment));
        }

        float total = 0f;
        distances.Add(0f);
        for (int i = 1; i < points.Count; i++)
        {
            total += Vector3.Distance(points[i - 1], points[i]);
            distances.Add(total);
        }
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
    }

    private void OnDrawGizmos()
    {
        if (_waypoints == null || _waypoints.Length == 0)
            return;

        var points = new List<Vector3>();
        var distances = new List<float>();
        BuildPath(Application.isPlaying && _points.Count > 0 ? _points[0] : transform.position, points, distances);

        Gizmos.color = new Color(1f, 0.85f, 0.1f);
        for (int i = 1; i < points.Count; i++)
            Gizmos.DrawLine(points[i - 1], points[i]);
        foreach (Transform waypoint in _waypoints)
            if (waypoint != null)
                Gizmos.DrawWireSphere(waypoint.position, 0.4f);
    }
}
