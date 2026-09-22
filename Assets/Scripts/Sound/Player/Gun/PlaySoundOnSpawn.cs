using UnityEngine;

public class PlaySoundOnSpawn : MonoBehaviour
{
    [Header("เสียง")]
    [SerializeField] private SoundID _sound;

    [Header("Options")]
    [Tooltip("ให้เสียงวิ่งตามของชิ้นนี้\n" +
        "เสียงปืน: ปิด (เสียงควรค้างอยู่ที่ปากกระบอก ไม่วิ่งตามกระสุนไป)\n" +
        "เสียงของที่ลอยอยู่กับที่: เปิดได้")]
    [SerializeField] private bool _followObject = false;

    [Tooltip("ใช้ตำแหน่งนี้แทนตำแหน่งตัวเอง — เว้นว่างได้")]
    [SerializeField] private Transform _customOrigin;

    [Header("Debug")]
    [SerializeField] private bool _logPlay = false;

    // OnEnable ไม่ใช่ Start เพราะเผื่อวันหน้าเปลี่ยนไปใช้พูลแทน Instantiate
    // ของจากพูลจะไม่เรียก Start ซ้ำ แต่เรียก OnEnable ทุกครั้งที่ถูกปลุก
    private void OnEnable()
    {
        if (_logPlay)
            Debug.Log($"[SpawnSound] {name} → {(_sound != null ? _sound.name : "ยังไม่ได้ใส่เสียง")}", this);

        if (_sound == null || SoundManager.Instance == null) return;

        Transform origin = _customOrigin != null ? _customOrigin : transform;

        if (_followObject)
            SoundManager.Instance.PlaySFXAttached(_sound, origin);
        else
            SoundManager.Instance.PlaySFX(_sound, origin.position);
    }
}