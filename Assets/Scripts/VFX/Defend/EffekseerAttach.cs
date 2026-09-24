using UnityEngine;
using Effekseer;

// PlayEffect() เล่นที่จุดเดียวในโลก ตามใครไม่ได้
// เลยสร้าง GameObject ลูกแล้วแปะ EffekseerEmitter ซึ่งย้ายเอฟเฟกต์ตามตัวมันทุกเฟรม
public static class EffekseerAttach
{
    private const float FallbackLifetime = 5f;

    public static void Play(EffekseerEffectAsset asset, Transform parent,
        Vector3 position, Quaternion rotation, float lifetime)
    {
        if (asset == null || parent == null) return;

        var carrier = new GameObject($"[VFX] {asset.name}");
        carrier.transform.SetPositionAndRotation(position, rotation);
        carrier.transform.SetParent(parent, true);

        // กระดูกบางตัว scale ไม่ใช่ 1 → Unity ชดเชย localScale ให้ แล้ว Emitter เอาไปคูณขนาดเอฟเฟกต์
        carrier.transform.localScale = Vector3.one;

        var emitter = carrier.AddComponent<EffekseerEmitter>();
        emitter.effectAsset = asset;
        emitter.Play();

        // Emitter ไม่ลบตัวเอง และตอนถูกลบจะตัดเอฟเฟกต์ทิ้ง → lifetime ต้องยาวกว่าตัวเอฟเฟกต์
        Object.Destroy(carrier, lifetime > 0f ? lifetime : FallbackLifetime);
    }
}