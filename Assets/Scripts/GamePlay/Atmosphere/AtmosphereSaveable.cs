using System;
using UnityEngine;

// เซฟบรรยากาศของฉาก (แสงหลัก + Fog Maximum Height ที่ AtmosphereController เปลี่ยนไป) — Load แล้วกลับมาเหมือนตอนเซฟ
// วางไว้ตัวเดียวกับ AtmosphereController ไม่ต้องใส่ SaveId
//
// เก็บเป็นตัวเลขดิบ ไม่เก็บ Profile — ไฟล์เซฟเก็บ reference ของ ScriptableObject ไม่ได้
// และถ้าเซฟตอนกำลังเฟดอยู่ ก็ได้ค่าระหว่างทางตรงตามที่เห็น
[RequireComponent(typeof(AtmosphereController))]
public class AtmosphereSaveable : SaveableBehaviour<AtmosphereSaveable.SaveData>
{
    [Serializable]
    public class SaveData
    {
        public float intensity;
        public Color filter;
        public bool useTemperature;
        public float temperature;
        public bool hasFog;            // เซฟเก่าไม่มีช่องนี้ = false = ไม่แตะหมอก
        public float fogMaximumHeight;
    }

    private AtmosphereController controller;

    // แยกตามฉาก — ไม่งั้นฉากอื่นจะดึงค่าของ Level1 ไปใช้
    protected override string DefaultKey => "atmosphere_" + gameObject.scene.name;

    private void Awake()
    {
        controller = GetComponent<AtmosphereController>();
    }

    protected override SaveData Capture()
    {
        if (controller == null || !controller.TryGetCurrent(out AtmosphereController.State s)) return null;

        return new SaveData
        {
            intensity = s.intensity,
            filter = s.filter,
            useTemperature = s.useTemperature,
            temperature = s.temperature,
            hasFog = s.hasFog,
            fogMaximumHeight = s.fogMaximumHeight,
        };
    }

    protected override void Restore(SaveData state, int version)
    {
        if (controller == null) return;

        controller.SetInstant(new AtmosphereController.State
        {
            intensity = state.intensity,
            filter = state.filter,
            useTemperature = state.useTemperature,
            temperature = state.temperature,
            hasFog = state.hasFog,
            fogMaximumHeight = state.fogMaximumHeight,
        });
    }
}
