using UnityEngine;

// ใส่ที่ prefab กระสุน (ตัวเดียวกับ PlayVFXOnSpawn) กระสุนเกิดเมื่อไหร่ ปืนปล่อยควันและกล้องดีดทันที
// กระสุนปกติเลือก Normal / กระสุน Ultimate เลือก Ultimate
public class GunSmokeOnSpawn : MonoBehaviour
{
    [SerializeField] private GunSmoke.ShotType _shot = GunSmoke.ShotType.Normal;

    [Tooltip("ให้กล้องดีดด้วย (ต้องมี CameraRecoil ที่กล้อง)")]
    [SerializeField] private bool _cameraRecoil = true;

    private void OnEnable()
    {
        GunSmoke smoke = GunSmoke.Active;
        if (smoke != null)
            smoke.Play(_shot);
        else
            Debug.LogWarning("[GunSmoke] กระสุนเกิดแล้ว แต่ไม่เจอ Gun Smoke ที่ปืนที่ถืออยู่ (ยังไม่ได้สร้าง หรือปืนถูกปิดอยู่)", this);

        if (!_cameraRecoil) return;

        if (CameraRecoil.Instance != null)
            CameraRecoil.Instance.Fire(_shot == GunSmoke.ShotType.Ultimate);
        else
            Debug.LogWarning("[GunSmoke] ไม่เจอ CameraRecoil ที่กล้อง กล้องเลยไม่ดีด", this);
    }
}