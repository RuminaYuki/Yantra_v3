// สแกน — ทำจอขาวดำ + คลื่นโซนาร์วิ่งไปตามพื้นและสิ่งก่อสร้างที่กล้องมองเห็น
// ทำงานก่อน Post Process: ภาพกับความลึกยังขนาดเดียวกัน (เกมเรนเดอร์เล็กแล้วขยายตอน Post Process)
// ทำขาวดำเองในนี้ คลื่นเลยยังเป็นสี แล้วยังได้ Bloom ทำให้เรือง
// อ่านความลึกของจอ แล้วระบายสีตามระยะจากจุดสแกน — ข้างหลังกำแพงไม่มีในภาพ เลยไม่ทะลุกำแพงเอง
// ScanAbility ตั้งค่าทั้งหมดผ่านตัวแปร global ไม่ต้องตั้งอะไรใน Material
Shader "FullScreen/SonarScan"
{
    HLSLINCLUDE

    #pragma vertex Vert

    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch

    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"

    float4 _SonarOrigin;    // xyz = จุดเริ่มคลื่น
    float4 _SonarParams;    // x = รัศมีหน้าคลื่น, y = ความหนาหน้าคลื่น, z = ความยาวหางคลื่น, w = ความแรงคลื่น
    float4 _SonarColor;
    float4 _SonarShape;     // x = ระยะไกลสุด, y = ระยะห่างเส้นวง (0 = ไม่มี), z = 1 = โหมดทดสอบ, w = ความสว่างคลื่น
    float4 _SonarGray;      // x = ขาวดำแค่ไหน 0–1

    float Wave(float3 positionWS)
    {
        float dist = distance(positionWS, _SonarOrigin.xyz);
        float behind = _SonarParams.x - dist;   // มากกว่า 0 = คลื่นผ่านไปแล้ว

        float width = max(_SonarParams.y, 0.001);
        if (behind < -width)
            return 0.0;                          // คลื่นยังมาไม่ถึง

        // หน้าคลื่น — เส้นสว่างคม
        float front = saturate(1.0 - abs(behind) / width);
        front *= front;

        // หางคลื่น — หมึกจาง ๆ ที่ค่อย ๆ หายไปข้างหลัง
        float trail = behind > 0.0 ? saturate(1.0 - behind / max(_SonarParams.z, 0.001)) : 0.0;
        trail *= trail;

        // เส้นวงซ้อน ๆ ในหาง ให้ดูเป็นโซนาร์
        float rings = 0.0;
        if (_SonarShape.y > 0.0)
            rings = smoothstep(0.9, 1.0, frac(dist / _SonarShape.y)) * trail;

        // จางตรงขอบระยะสแกน
        float range = 1.0 - smoothstep(_SonarShape.x * 0.85, _SonarShape.x, dist);

        return saturate(front + trail * 0.3 + rings * 0.8) * range;
    }

    float4 FullScreenPass(Varyings varyings) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);

        float3 color = CustomPassLoadCameraColor(varyings.positionCS.xy, 0);

        // ขาวดำ
        float luma = dot(color, float3(0.2126, 0.7152, 0.0722));
        color = lerp(color, luma.xxx, saturate(_SonarGray.x));

        float depth = LoadCameraDepth(varyings.positionCS.xy);
        if (depth == UNITY_RAW_FAR_CLIP_VALUE)
            return float4(color, 1);             // ท้องฟ้า — ไม่มีคลื่น

        // โหมดทดสอบ — ทุกอย่างที่ไม่ใช่ท้องฟ้าเป็นสีแดง
        if (_SonarShape.z > 0.5)
            return float4(lerp(color, float3(1, 0, 0), 0.4), 1);

        if (_SonarParams.w <= 0.0)
            return float4(color, 1);

        PositionInputs posInput = GetPositionInput(varyings.positionCS.xy, _ScreenSize.zw, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);
        float3 positionWS = GetAbsolutePositionWS(posInput.positionWS);

        float wave = Wave(positionWS) * _SonarParams.w * _SonarColor.a;
        color = lerp(color, _SonarColor.rgb * max(_SonarShape.w, 0.01), wave);
        return float4(color, 1);
    }

    ENDHLSL

    SubShader
    {
        Tags{ "RenderPipeline" = "HDRenderPipeline" }
        Pass
        {
            Name "Custom Pass 0"

            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
                #pragma fragment FullScreenPass
            ENDHLSL
        }
    }
    Fallback Off
}
