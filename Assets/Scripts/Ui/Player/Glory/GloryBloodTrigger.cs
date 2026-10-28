using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// วางบนตัวผีหรือตัวลูก (เช่นตัวที่มี GloryInteractor ระดับอก) — หาท่า Glory ของผีให้เอง ไม่ต้องต่อสาย
public class GloryBloodTrigger : MonoBehaviour
{
    public enum SprayDirection
    {
        TowardCamera,
        AwayFromCamera,
        EffectPoint,
        PrefabDefault
    }

    [Header("จังหวะเลือดกระเด็น")]
    [Tooltip("ท่าเล่นไปกี่วินาทีถึงเลือดกระเด็น นับจากตอนท่าเริ่มจริง\n" +
             "กด + เพิ่มได้ ถ้าอยากให้กระเด็นหลายจังหวะ (เช่น หมัด 1 / 2 / 3)")]
    [SerializeField] private List<float> _bloodTimes = new List<float> { 0.5f };

    [Header("VFX เลือดกระเด็น (ไม่ใส่ก็ได้)")]
    [Tooltip("ลาก Prefab เลือดกระเด็นของเพื่อนใส่")]
    [SerializeField] private GameObject _splatterEffect;

    [Tooltip("จุดที่เลือดพุ่งออก — เว้นว่าง = ตัวนี้")]
    [SerializeField] private Transform _effectPoint;

    [Tooltip("เลือดพุ่งไปทางไหน\n" +
             "TowardCamera = พุ่งเข้าหาจอ เข้ากับเลือดสาดจอ\n" +
             "AwayFromCamera = พุ่งไปข้างหลังผี ตามแรงหมัด\n" +
             "EffectPoint = ตามลูกศรสีฟ้าของจุดเกิด\n" +
             "PrefabDefault = ทิศเดิมที่เพื่อนตั้งไว้ใน prefab")]
    [SerializeField] private SprayDirection _direction = SprayDirection.TowardCamera;

    [Tooltip("ลบเอฟเฟกต์ทิ้งหลังกี่วินาที")]
    [SerializeField, Min(0.1f)] private float _effectLifetime = 3f;

    [Header("เลือดสาดจอ")]
    [Tooltip("ติ๊ก = สั่ง ScreenBloodOverlay ใน UIRoot ให้สาดเลือดบนจอด้วย")]
    [SerializeField] private bool _splashScreen = true;

    [Header("Debug")]
    [Tooltip("ขึ้น log ทุกจังหวะที่เลือดกระเด็น ไว้จูนเวลาให้ตรงหมัด")]
    [SerializeField] private bool _logPlay;

    private PairedAnimationActor _actor;
    private bool _wasPlaying;

    private void Awake()
    {
        _actor = GetComponentInParent<PairedAnimationActor>();
        if (_actor == null)
            _actor = GetComponentInChildren<PairedAnimationActor>();

        if (_actor == null)
            Debug.LogWarning("[GloryBlood] หา PairedAnimationActor ของผีไม่เจอ — วางสคริปต์นี้บนตัวผีหรือตัวลูกของผี", this);
    }

    private void OnEnable() => _wasPlaying = false;

    private void OnDisable() => StopAllCoroutines();

    private void Update()
    {
        bool playing = IsGloryPlaying();
        if (playing && !_wasPlaying)
        {
            StopAllCoroutines();
            StartCoroutine(BleedRoutine());
        }
        _wasPlaying = playing;
    }

    // นับตอนท่าเล่นจริงเท่านั้น ไม่นับช่วงดูดตัวเข้าที่ / รอกล้อง
    private bool IsGloryPlaying()
    {
        if (_actor == null)
            return false;

        PairedRequest request = _actor.CurrentRequest;
        return request.Animation != null && request.Type == PairedActionType.Glory &&
               !_actor.IsPreparing && !_actor.IsFinished;
    }

    private IEnumerator BleedRoutine()
    {
        var times = new List<float>(_bloodTimes);
        times.Sort();

        float elapsed = 0f;
        foreach (float time in times)
        {
            while (elapsed < time)
            {
                yield return null;
                elapsed += Time.deltaTime;

                if (!IsGloryPlaying())
                {
                    if (_logPlay)
                        Debug.LogWarning($"[GloryBlood] ท่าจบตอน {elapsed:0.00} วิ — จังหวะ {time:0.00} วิ ไม่ได้เล่น (ตั้งเวลาเกินความยาวท่า?)", this);
                    yield break;
                }
            }

            Bleed(elapsed);
        }
    }

    private void Bleed(float time)
    {
        if (_logPlay)
            Debug.Log($"[GloryBlood] เลือดกระเด็น @ {time:0.00} วิ", this);

        PlayEffect();

        if (!_splashScreen)
            return;

        if (ScreenBloodOverlay.Instance != null)
            ScreenBloodOverlay.Instance.Splash();
        else if (_logPlay)
            Debug.LogWarning("[GloryBlood] ไม่เจอ ScreenBloodOverlay ใน UIRoot — จอจะไม่มีเลือด", this);
    }

    private void PlayEffect()
    {
        if (_splatterEffect == null)
            return;

        Transform point = _effectPoint != null ? _effectPoint : transform;
        GameObject instance = Instantiate(_splatterEffect, point.position, GetSprayRotation(point));
        instance.SetActive(true);

        // Particle ที่ไม่ได้ติ๊ก Play On Awake จะไม่เล่นเอง — สั่งเฉพาะตัวบนสุด ตัวลูกเล่นตาม
        foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (IsTopParticle(ps, instance.transform) && !ps.isPlaying)
                ps.Play(true);
        }

        Destroy(instance, _effectLifetime);
    }

    // จุดเกิดหมุนตามกระดูก ซึ่งด้านหน้าของกระดูก mixamo มักชี้ขึ้นฟ้า — เลยคำนวณทิศจากกล้องแทน
    private Quaternion GetSprayRotation(Transform point)
    {
        if (_direction == SprayDirection.EffectPoint)
            return point.rotation;
        if (_direction == SprayDirection.PrefabDefault)
            return _splatterEffect.transform.rotation;

        Camera cam = Camera.main;
        if (cam == null)
            return point.rotation;

        Vector3 toCamera = cam.transform.position - point.position;
        if (toCamera.sqrMagnitude < 0.0001f)
            return point.rotation;

        Vector3 dir = _direction == SprayDirection.TowardCamera ? toCamera : -toCamera;
        return Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    private static bool IsTopParticle(ParticleSystem ps, Transform root)
    {
        if (ps.transform == root)
            return true;

        for (Transform t = ps.transform.parent; t != null; t = t.parent)
        {
            if (t.TryGetComponent<ParticleSystem>(out _))
                return false;
            if (t == root)
                break;
        }
        return true;
    }
}