using System;
using UnityEngine;

// เซฟค่าแสงหลักของฉาก (ที่ GlobalLightController เปลี่ยนไปตาม Trigger) — Load แล้วแสงกลับมาเหมือนตอนเซฟ
// วางไว้ตัวเดียวกับ GlobalLightController (Directional Light ของฉาก) ไม่ต้องใส่ SaveId
//
// เก็บเป็นตัวเลขดิบ ไม่เก็บ Profile — ไฟล์เซฟเก็บ reference ของ ScriptableObject ไม่ได้
// และถ้าเซฟตอนแสงกำลังเฟดอยู่ ก็ได้ค่าระหว่างทางตรงตามที่เห็น
[RequireComponent(typeof(GlobalLightController))]
public class GlobalLightSaveable : SaveableBehaviour<GlobalLightSaveable.SaveData>
{
    [Serializable]
    public class SaveData
    {
        public float intensity;
        public Color filter;
        public bool useTemperature;
        public float temperature;
    }

    private GlobalLightController controller;

    // แยกตามฉาก — ไม่งั้นฉากอื่นจะดึงแสงของ Level1 ไปใช้
    protected override string DefaultKey => "globalLight_" + gameObject.scene.name;

    private void Awake()
    {
        controller = GetComponent<GlobalLightController>();
    }

    protected override SaveData Capture()
    {
        if (controller == null ||
            !controller.TryGetCurrent(out float intensity, out Color filter, out bool useTemperature, out float temperature))
            return null;

        return new SaveData
        {
            intensity = intensity,
            filter = filter,
            useTemperature = useTemperature,
            temperature = temperature,
        };
    }

    protected override void Restore(SaveData state, int version)
    {
        if (controller == null) return;
        controller.SetInstant(state.intensity, state.filter, state.useTemperature, state.temperature);
    }
}
