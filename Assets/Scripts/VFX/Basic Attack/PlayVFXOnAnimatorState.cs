using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Serialization;
using Effekseer;

public class PlayVFXOnAnimatorState : StateMachineBehaviour
{
    public enum PlayMode
    {
        /// <summary>เด้งทีเดียวแล้วจบ — ท่าต่อย ท่ายิง</summary>
        OneShot,

        /// <summary>ค้างไว้ตราบที่ยังอยู่ใน state นี้ ออกจาก state แล้วลบทิ้ง — โล่ ออร่า</summary>
        HoldWhileInState
    }

    /// <summary>
    /// ขยับเอฟเฟกต์โดยอิงแกนของอะไร
    /// สำคัญเพราะแกนของกระดูกไม่ได้ตั้งตรงเหมือนแกนโลก
    /// คนทำ rig วางไว้ยังไงก็เป็นอย่างนั้น ใส่ Y บวกอาจลงล่างก็ได้
    /// </summary>
    public enum OffsetSpace
    {
        /// <summary>อิงตัวละคร — Y ขึ้นบน, Z ไปทางที่ตัวละครหัน (เข้าใจง่ายสุด)</summary>
        Character,

        /// <summary>อิงกระดูก — หมุนตามมือ แกนเป็นไปตามที่คนทำ rig วางไว้</summary>
        Bone,

        /// <summary>อิงโลก — Y ขึ้นบนเสมอ แต่ไม่หันตามตัวละคร</summary>
        World
    }

    public enum RotationMode
    {
        /// <summary>ไม่หมุนเลย ใช้ท่าที่คนทำเอฟเฟกต์ออกแบบมา — ลองอันนี้ก่อนเสมอ</summary>
        None,

        /// <summary>หันตามกระดูกที่เป็นจุดกำเนิด</summary>
        FollowOrigin,

        /// <summary>หันตามกล้อง — มักถูกสำหรับ FPP</summary>
        FaceCamera
    }

    [Header("โหมด")]
    [Tooltip("OneShot = เด้งทีเดียวจบ (ท่าต่อย)\n" +
        "HoldWhileInState = ค้างไว้จนออกจาก state (โล่บล็อก)\n\n" +
        "โหมด Hold ใช้ได้เฉพาะกับ Vfx Prefab เท่านั้น")]
    [SerializeField] private PlayMode _playMode = PlayMode.OneShot;

    [Header("VFX แบบ Effekseer")]
    [Tooltip("ขนาดปรับที่ช่อง Scale ของตัว asset เอง ไม่ใช่ที่นี่\nโหมด Hold ใช้ช่องนี้ไม่ได้")]
    // ชื่อเดิมคือ _effect — บอก Unity ไว้ว่าเคยชื่ออะไร
    // ไม่งั้น state ต่อยทั้ง 4 ตัวที่ตั้งค่าไว้แล้วจะกลายเป็น None ต้องมานั่งใส่ใหม่
    [FormerlySerializedAs("_effect")]
    [SerializeField] private EffekseerEffectAsset _effekseerEffect;

    [Header("VFX แบบ Prefab ของ Unity")]
    [Tooltip("Prefab ที่มี ParticleSystem หรือ VFX Graph อยู่ข้างใน\n" +
        "เช่น prefab Block / Block Hit / Parry ที่ทำในยูนิตี้")]
    [SerializeField] private GameObject _vfxPrefab;

    [Tooltip("โหมด OneShot: ทำลาย prefab หลังกี่วินาที (0 = ไม่ทำลายให้)\n" +
        "โหมด Hold: ไม่ใช้ช่องนี้ เพราะลบตอนออกจาก state อยู่แล้ว")]
    [SerializeField] private float _prefabLifetime = 3f;

    [Tooltip("ให้ prefab เป็นลูกของจุดกำเนิด = วิ่งตามมือ/ตัวละคร\n" +
        "โหมด Hold ควรเปิดไว้ ไม่งั้นโล่จะค้างอยู่กับที่ตอนเดิน")]
    [SerializeField] private bool _parentPrefabToOrigin = false;

    [Tooltip("โหมด Hold เท่านั้น — ปล่อยปุ่มแล้วรอกี่วินาทีถึงลบโล่ทิ้ง\n\n" +
        "ระบบบล็อกสั่งเข้า state ซ้ำทุกเฟรม ทำให้ Unity ยิง Exit/Enter สลับกันรัวๆ\n" +
        "ถ้าลบทันทีที่ออกจาก state โล่จะกระพริบ ค่านี้คือช่วงผ่อนผันให้มันอยู่ข้ามจังหวะสะดุดไปได้\n" +
        "สั้นเกิน = กระพริบ / ยาวเกิน = โล่ค้างหลังปล่อยปุ่ม")]
    [SerializeField] private float _holdGraceSeconds = 0.15f;

    [Tooltip("โหมด Hold เท่านั้น — หลังสั่งหยุดพ่นอนุภาคแล้ว รออีกกี่วินาทีถึงลบทิ้ง\n\n" +
        "เราไม่ลบทันทีเพราะภาพจะหายวับ ทั้งที่ตอนขึ้นมามันค่อยๆ ก่อตัว\n" +
        "สั่งหยุดพ่นแล้วปล่อยให้อนุภาคที่ออกมาแล้วเล่นจนจบ = ได้ fade out ตามที่คนทำ VFX ออกแบบไว้\n" +
        "ตั้งให้ยาวพอๆ กับอายุอนุภาคที่ยาวที่สุดในเอฟเฟกต์นั้น")]
    [SerializeField] private float _holdTailSeconds = 1.5f;

    [Header("จังหวะ")]
    [Tooltip("เล่นตอนคลิปเดินไปกี่ % (0 = เฟรมแรก, 0.3 = 30%)\n" +
        "ท่าต่อยราว 0.25-0.4 / ท่าบล็อกใช้ 0 ได้เลยเพราะต้องขึ้นทันที\n" +
        "ตั้งให้ตรงกับเสียงใน PlaySoundOnAnimatorState ของ state เดียวกัน")]
    [Range(0f, 0.95f)]
    [SerializeField] private float _triggerAt = 0.1f;

    [Header("จุดกำเนิด")]
    [Tooltip("ใส่แค่ชื่อกระดูกพอ เช่น Hand_R หรือ mixamorig:LeftHand\n" +
        "ไม่ต้องใส่พาธเต็ม เดี๋ยวมันไล่หาเองทั้งต้นไม้\n\n" +
        "เว้นว่าง = ใช้ตัวที่มี Animator\n" +
        "หาชื่อไม่เจอ? ติ๊ก Log Play แล้วเล่นดู มันจะบอกชื่อที่ใกล้เคียงให้")]
    [SerializeField] private string _originName = "";

    [Tooltip("ขยับจากจุดกำเนิด — อิงแกนตาม Offset Space ข้างล่าง")]
    [SerializeField] private OffsetSpace _offsetSpace = OffsetSpace.Character;

    [Tooltip("ระยะที่จะขยับ ตามแกนของ Offset Space")]
    [SerializeField] private Vector3 _offset = Vector3.zero;

    [Header("ทิศทาง")]
    [Tooltip("เริ่มจาก None ดูท่าเดิมก่อน แล้วค่อยลองโหมดอื่น")]
    [SerializeField] private RotationMode _rotationMode = RotationMode.None;

    [Tooltip("หมุนเพิ่ม (องศา) ปรับทีละ 90 ก่อน: Y=90 / Y=180 / X=90 / X=-90\n" +
        "VFX Graph พวกวงแหวน/วงเวทมักถูกสร้างมานอนราบ ตั้งขึ้นด้วย X ±90")]
    [SerializeField] private Vector3 _rotationOffset = Vector3.zero;

    [Header("Options")]
    [Tooltip("เล่นซ้ำได้ไหมถ้าคลิปวนรอบ / ท่าโจมตีไม่วน ปิดไว้ได้\nโหมด Hold ไม่ใช้ช่องนี้")]
    [SerializeField] private bool _retriggerOnLoop = false;

    [Header("Debug")]
    [Tooltip("บอกว่าเล่นเมื่อไหร่ จากกระดูกไหน และถ้าหากระดูกไม่เจอจะเสนอชื่อที่ใกล้เคียงให้")]
    [SerializeField] private bool _logPlay = false;

    [Tooltip("วาดกากบาทสีเหลืองตรงจุดที่เล่น 2 วินาที ดูใน Scene view")]
    [SerializeField] private bool _drawDebugPoint = false;

    // Unity สร้างสำเนาของ behaviour นี้ให้ Animator แต่ละตัว
    // ตัวแปรข้างในจึงเป็นของใครของมัน ผีหลายตัวไม่กวนกัน
    private bool _hasPlayed;
    private int _lastLoopIndex = -1;
    private GameObject _heldInstance;
    private VFXKeepAlive _heldKeepAlive;

    // การไล่หากระดูกทั้งต้นไม้แพงเกินกว่าจะทำทุกครั้ง เลยจำไว้ใช้ซ้ำ
    private Transform _cachedOrigin;
    private Transform _cachedRoot;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        _hasPlayed = false;
        _lastLoopIndex = -1;

        // ⚠️ ห้ามลบของที่ค้างอยู่ตรงนี้
        // ระบบบล็อกสั่งเข้า state เดิมซ้ำทุกเฟรม OnStateEnter จึงถูกเรียกรัวๆ
        // ถ้าลบตรงนี้ โล่จะถูกลบแล้วสร้างใหม่ทุกเฟรม = ภาพกระพริบและเอฟเฟกต์ไม่ทันเล่น
        // การเก็บกวาดเป็นหน้าที่ของ VFXKeepAlive บนตัว prefab แทน
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (_effekseerEffect == null && _vfxPrefab == null) return;

        // ยังอยู่ใน state และตัวดูแลยังทำงานอยู่ = ต่ออายุให้มัน ไม่ต้องสร้างใหม่
        //
        // เช็คจาก _heldKeepAlive ไม่ใช่ _heldInstance เพราะตอนเอฟเฟกต์กำลังจางหาย
        // GameObject ยังอยู่แต่ตัวดูแลถูกลบไปแล้ว ถ้าเช็คจาก GameObject
        // แล้วผู้เล่นกางโล่ใหม่ตอนนั้นพอดี มันจะไม่สร้างใหม่ให้ = โล่ไม่ขึ้น
        if (_playMode == PlayMode.HoldWhileInState && _heldKeepAlive != null)
        {
            _heldKeepAlive.Ping();
            return;
        }

        float progress = stateInfo.normalizedTime;
        int loopIndex = Mathf.FloorToInt(progress);
        float inClip = progress - loopIndex;   // 0..1 ภายในรอบปัจจุบัน

        if (_playMode == PlayMode.OneShot && _retriggerOnLoop && loopIndex != _lastLoopIndex)
        {
            _lastLoopIndex = loopIndex;
            _hasPlayed = false;
        }

        if (_hasPlayed) return;
        if (inClip < _triggerAt) return;

        _hasPlayed = true;
        Play(animator);
    }

    /// <summary>
    /// ไม่ลบอะไรตรงนี้เช่นกัน — ดูเหตุผลใน OnStateEnter
    /// พอออกจาก state จริงๆ ก็ไม่มีใคร Ping แล้ว VFXKeepAlive จะลบตัวเองเอง
    /// </summary>
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // ตั้งใจว่างไว้
        //
        // ห้ามเซ็ต _heldInstance = null ตรงนี้ด้วย ไม่งั้นพอเข้า state ใหม่อีกเฟรมถัดมา
        // มันจะมองว่ายังไม่มีโล่แล้วสร้างเพิ่มอีกตัว กลายเป็นโล่ซ้อนกันเรื่อยๆ
        //
        // พอ VFXKeepAlive ลบ GameObject ทิ้งจริงๆ ตัวแปรนี้จะกลายเป็น null
        // ตามกลไกของ Unity เอง รอบหน้าถึงจะสร้างใหม่ ซึ่งถูกต้องแล้ว
    }

    private void Play(Animator animator)
    {
        Transform origin = ResolveOrigin(animator);
        Vector3 position = origin.position + ResolveOffset(animator, origin);
        Quaternion rotation = BuildRotation(origin);

        if (_logPlay)
        {
            Debug.Log(
                $"[AnimVFX] {_playMode} @ {_triggerAt:P0} — จาก '{origin.name}' | " +
                $"Effekseer: {(_effekseerEffect != null ? _effekseerEffect.name : "-")} | " +
                $"Prefab: {(_vfxPrefab != null ? _vfxPrefab.name : "-")}", animator);
        }

        if (_drawDebugPoint)
        {
            Debug.DrawLine(position + Vector3.up * 0.15f, position - Vector3.up * 0.15f, Color.yellow, 2f);
            Debug.DrawLine(position + Vector3.right * 0.15f, position - Vector3.right * 0.15f, Color.yellow, 2f);
            Debug.DrawLine(position + Vector3.forward * 0.15f, position - Vector3.forward * 0.15f, Color.yellow, 2f);
        }

        PlayEffekseer(position, rotation);
        SpawnPrefab(origin, position, rotation);
    }

    private void PlayEffekseer(Vector3 position, Quaternion rotation)
    {
        if (_effekseerEffect == null) return;

        if (_playMode == PlayMode.HoldWhileInState)
        {
#if UNITY_EDITOR
            Debug.LogWarning(
                "[AnimVFX] โหมด Hold ใช้กับ Effekseer ไม่ได้ — สั่งหยุดกลางคันไม่ได้ " +
                "ให้ใช้ช่อง Vfx Prefab แทน (ตัว Effekseer จะเล่นแบบเด้งทีเดียว)");
#endif
        }

        var handle = EffekseerSystem.PlayEffect(_effekseerEffect, position);
        handle.SetRotation(rotation);
    }

    private void SpawnPrefab(Transform origin, Vector3 position, Quaternion rotation)
    {
        if (_vfxPrefab == null) return;

        GameObject spawned = _parentPrefabToOrigin
            ? Object.Instantiate(_vfxPrefab, position, rotation, origin)
            : Object.Instantiate(_vfxPrefab, position, rotation);

        if (_playMode == PlayMode.HoldWhileInState)
        {
            // ไม่ตั้งเวลาทำลายตายตัว — ให้ VFXKeepAlive เป็นคนดูแลอายุแทน
            // ตราบที่ยังอยู่ใน state เราจะ Ping ให้มันทุกเฟรม พอหยุด Ping มันลบตัวเอง
            _heldInstance = spawned;

            _heldKeepAlive = spawned.GetComponent<VFXKeepAlive>();
            if (_heldKeepAlive == null)
                _heldKeepAlive = spawned.AddComponent<VFXKeepAlive>();

            _heldKeepAlive.Configure(_holdGraceSeconds, _holdTailSeconds);
            return;
        }

        // ไม่ทำลายให้ = prefab ต้องลบตัวเอง ไม่งั้นมันจะกองสะสมทุกครั้งที่เล่นท่า
        if (_prefabLifetime > 0f)
            Object.Destroy(spawned, _prefabLifetime);
    }

    /// <summary>
    /// แปลงค่า Offset ให้เป็นระยะในพิกัดโลก ตามแกนที่เลือกไว้
    ///
    /// ที่ต้องมีตัวเลือกเพราะแกนของกระดูกไม่ได้ตั้งตรงตามที่คนคาด
    /// เช่นตอนมือห้อยลง แกน Y ของกระดูกมือจะชี้ลงพื้น ใส่ Y บวกก็ยิ่งจมลงไปอีก
    /// </summary>
    private Vector3 ResolveOffset(Animator animator, Transform origin)
    {
        if (_offset == Vector3.zero) return Vector3.zero;

        switch (_offsetSpace)
        {
            case OffsetSpace.Bone:
                return origin.rotation * _offset;

            case OffsetSpace.World:
                return _offset;

            default:
                return animator.transform.rotation * _offset;
        }
    }

    private Quaternion BuildRotation(Transform origin)
    {
        Quaternion baseRotation;

        switch (_rotationMode)
        {
            case RotationMode.FollowOrigin:
                baseRotation = origin.rotation;
                break;

            case RotationMode.FaceCamera:
                Camera cam = Camera.main;
                baseRotation = cam != null ? cam.transform.rotation : Quaternion.identity;
                break;

            default:
                baseRotation = Quaternion.identity;
                break;
        }

        return baseRotation * Quaternion.Euler(_rotationOffset);
    }

    /// <summary>
    /// StateMachineBehaviour อยู่บน Animator Controller ซึ่งเป็น asset
    /// จึงลากอ้างอิง GameObject ในซีนมาใส่ไม่ได้ ต้องหาเอาตอนรัน
    ///
    /// รับได้ทั้งพาธเต็ม (Armature/Hips/.../Hand_R) และชื่อเปล่าๆ (Hand_R)
    /// ชื่อเปล่าจะไล่หาทั้งต้นไม้ให้ เพราะพาธเต็มพิมพ์ผิดง่ายเกินไป
    /// </summary>
    private Transform ResolveOrigin(Animator animator)
    {
        Transform root = animator.transform;

        if (string.IsNullOrEmpty(_originName)) return root;

        if (_cachedOrigin != null && _cachedRoot == root)
            return _cachedOrigin;

        Transform found = root.Find(_originName) ?? FindDeep(root, _originName);

        if (found == null)
        {
            if (_logPlay)
            {
                Debug.LogWarning(
                    $"[AnimVFX] หา '{_originName}' ไม่เจอใต้ {animator.name} — ใช้ตัวหลักแทน\n" +
                    SuggestNames(root, _originName), animator);
            }
            return root;
        }

        _cachedOrigin = found;
        _cachedRoot = root;
        return found;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == name) return child;

            Transform deeper = FindDeep(child, name);
            if (deeper != null) return deeper;
        }

        return null;
    }

    /// <summary>
    /// พอหาไม่เจอ การบอกแค่ "ไม่เจอ" ไม่ช่วยอะไร
    /// เลยไล่ชื่อที่ใกล้เคียงมาเสนอ จะได้ก็อปไปวางได้เลยไม่ต้องไล่คลิกใน Hierarchy
    /// </summary>
    private static string SuggestNames(Transform root, string wanted)
    {
        string needle = wanted.ToLowerInvariant();
        var hints = new List<string>();
        CollectHints(root, needle, hints);

        if (hints.Count == 0)
            return "ไม่เจอชื่อที่ใกล้เคียงเลย ลองเปิด Hierarchy ดูชื่อกระดูกจริงๆ";

        var sb = new StringBuilder("ชื่อที่ใกล้เคียงในตัวนี้: ");
        for (int i = 0; i < hints.Count && i < 15; i++)
        {
            if (i > 0) sb.Append(" · ");
            sb.Append(hints[i]);
        }

        return sb.ToString();
    }

    private static void CollectHints(Transform root, string needle, List<string> into)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            string lower = child.name.ToLowerInvariant();

            if (lower.Contains(needle) || lower.Contains("hand") || lower.Contains("wrist"))
                into.Add(child.name);

            CollectHints(child, needle, into);
        }
    }
}