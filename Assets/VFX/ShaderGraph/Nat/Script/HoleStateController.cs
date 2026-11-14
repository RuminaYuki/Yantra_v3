
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

public class HoleStateController : MonoBehaviour
{
    public enum HoleState
    {
        Idle,
        Charging,
        Activated
    }

    const string ChargeAmountName = "ChargeAmount";
    const string ActivateBurstEvent = "ActivateBurst";

    [Header("References")]
    public MeshRenderer holeDecal;
    public VisualEffect holeVFX;

    [Header("State")]
    public HoleState selectedState = HoleState.Idle;

    [Header("Charging Settings")]
    [Min(0.01f)]
    public float chargingPulseSpeed = 1.2f;

    [Min(0.1f)]
    public float chargingDuration = 3f;

    [Header("Shader Reference")]
    public string holePulseReference = "_HolePulse";

    private Material originalMaterial;
    private Material runtimeMaterial;

    private HoleState previousState;

    private float chargeAmount;
    private float idleHolePulse;
    private float pulseTime;

    private bool initialized = false;

    void Start()
    {
        // ตรวจสอบ References
        if (holeDecal == null)
        {
            Fail("ยังไม่ได้ใส่ Hole Decal");
            return;
        }

        if (holeVFX == null)
        {
            Fail("ยังไม่ได้ใส่ Hole VFX");
            return;
        }

        originalMaterial = holeDecal.sharedMaterial;

        if (originalMaterial == null)
        {
            Fail("Hole Decal ไม่มี Material");
            return;
        }

        if (holeVFX.visualEffectAsset == null)
        {
            Fail("Hole VFX ไม่มี VFX Graph Asset");
            return;
        }

        // ตรวจสอบ ChargeAmount
        if (!holeVFX.HasFloat(ChargeAmountName))
        {
            Fail(
                "ไม่พบ Float ChargeAmount ใน VFX Graph\n" +
                "Exposed Properties: " +
                ListVfxProperties(holeVFX.visualEffectAsset)
            );
            return;
        }

        // ตรวจสอบ HolePulse
        if (!originalMaterial.HasProperty(holePulseReference))
        {
            Fail(
                "ไม่พบ Shader Property: " +
                holePulseReference + "\n" +
                "Properties: " +
                ListShaderProperties(originalMaterial.shader)
            );
            return;
        }

        // เก็บค่า HolePulse เดิมของ Idle
        idleHolePulse = originalMaterial.GetFloat(
            holePulseReference
        );

        // สร้าง Material แยกสำหรับ Object นี้
        runtimeMaterial = new Material(originalMaterial);
        holeDecal.sharedMaterial = runtimeMaterial;

        previousState = selectedState;

        // เริ่มต้นโดยไม่สร้างประกาย Charging
        chargeAmount = 0f;

        holeVFX.SetFloat(
            ChargeAmountName,
            chargeAmount
        );

        initialized = true;

        // ถ้าเริ่มเกมด้วย Activated ให้ปล่อย Burst
        if (selectedState == HoleState.Activated)
        {
            TriggerActivated();
        }

        Debug.Log(
            "[Hole] Controller Ready: " + selectedState,
            this
        );
    }

    void Update()
    {
        if (!initialized)
            return;

        // ตรวจการเปลี่ยน State ผ่าน Inspector
        if (selectedState != previousState)
        {
            OnStateChanged(selectedState);
            previousState = selectedState;
        }

        switch (selectedState)
        {
            case HoleState.Idle:
                UpdateIdle();
                break;

            case HoleState.Charging:
                UpdateCharging();
                break;

            case HoleState.Activated:
                UpdateActivated();
                break;
        }

        // ส่งค่าควบคุม Charging ไปยัง VFX Graph
        holeVFX.SetFloat(
            ChargeAmountName,
            chargeAmount
        );
    }

    void OnStateChanged(HoleState newState)
    {
        switch (newState)
        {
            case HoleState.Idle:

                // ปิดระบบสะสมพลัง
                chargeAmount = 0f;

                // คืนค่า HolePulse แบบ Idle เดิม
                runtimeMaterial.SetFloat(
                    holePulseReference,
                    idleHolePulse
                );

                break;

            case HoleState.Charging:

                // เริ่มสะสมพลังใหม่
                chargeAmount = 0f;
                pulseTime = 0f;

                break;

            case HoleState.Activated:

                // หยุดสร้างประกาย Charging
                chargeAmount = 0f;

                holeVFX.SetFloat(
                    ChargeAmountName,
                    0f
                );

                // ปล่อยควันและประกาย Burst
                TriggerActivated();

                break;
        }

        Debug.Log(
            "[Hole] Changed State: " + newState,
            this
        );
    }

    void UpdateIdle()
    {
        // ไม่มีประกาย Charging ใหม่
        chargeAmount = 0f;

        // ไม่เขียนทับ HolePulse
        // ใช้ค่า Shader เดิม
    }

    void UpdateCharging()
    {
        // ค่อย ๆ เพิ่มพลังจาก 0 ถึง 1
        chargeAmount = Mathf.MoveTowards(
            chargeAmount,
            1f,
            Time.deltaTime / Mathf.Max(
                0.1f,
                chargingDuration
            )
        );

        // ทำให้หลุม Pulse ระหว่าง Charging
        UpdateHolePulse();
    }

    void UpdateActivated()
    {
        // ปิดระบบสร้างประกาย Charging
        chargeAmount = 0f;

        // หลุมยังคงเคลื่อนไหว
        UpdateHolePulse();
    }

    void TriggerActivated()
    {
        if (holeVFX == null)
            return;

        // ปิด Charge Energy ก่อนปล่อย Burst
        holeVFX.SetFloat(
            ChargeAmountName,
            0f
        );

        // สั่ง Burst Smoke และ Burst Sparks
        holeVFX.SendEvent(ActivateBurstEvent);

        Debug.Log(
            "[Hole] Activated Burst!",
            this
        );
    }

    void UpdateHolePulse()
    {
        pulseTime += Time.deltaTime * chargingPulseSpeed;

        float pulse = 0.5f + 0.5f * Mathf.Sin(
            pulseTime * Mathf.PI * 2f
        );

        runtimeMaterial.SetFloat(
            holePulseReference,
            pulse
        );
    }

    // ใช้เปลี่ยนสถานะจาก Gameplay Script
    public void SetState(HoleState newState)
    {
        selectedState = newState;
    }

    void OnDestroy()
    {
        if (runtimeMaterial != null)
        {
            // คืน Material เดิม
            if (holeDecal != null)
                holeDecal.sharedMaterial = originalMaterial;

            Destroy(runtimeMaterial);
        }
    }

    void Fail(string message)
    {
        Debug.LogError("[Hole] " + message, this);
        enabled = false;
    }

    static string ListVfxProperties(VisualEffectAsset asset)
    {
        var properties = new List<VFXExposedProperty>();
        asset.GetExposedProperties(properties);

        if (properties.Count == 0)
            return "ไม่มี";

        var names = new List<string>();

        foreach (var property in properties)
        {
            names.Add(
                property.name + " (" +
                property.type.Name + ")"
            );
        }

        return string.Join(", ", names);
    }

    static string ListShaderProperties(Shader shader)
    {
        var names = new List<string>();

        for (int i = 0; i < shader.GetPropertyCount(); i++)
        {
            names.Add(shader.GetPropertyName(i));
        }

        return string.Join(", ", names);
    }
}
