#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// ตาเรืองแสงสีเหลืองของตานี — เมนู Tools > Yantra > Tani Glowing Eyes
///
/// ตาของตานีวาดอยู่ในรูปหน้า (Tanif_ace.png) ใช้ material TaniMatt_Face
/// เครื่องมือนี้ใส่ Emission ให้ material นั้น โดยใช้รูป Tani_EyeGlowMask
/// (ขาวเฉพาะม่านตา ที่เหลือดำ) — เลยเรืองแค่ตา หน้าไม่เรืองตาม
///
/// Exposure Weight = 0 → ตาสว่างเท่าเดิมทุกฉาก ไม่ขึ้นกับแสง / exposure ของกล้อง
/// กดซ้ำได้ (ค่าจะถูกตั้งกลับเป็นค่าข้างล่าง) / Ctrl+Z ย้อนได้
/// </summary>
public static class TaniEyeGlowSetup
{
    private const string Title = "Tani Glowing Eyes";
    private const string MaterialName = "TaniMatt_Face";
    private const string MaskName = "Tani_EyeGlowMask";

    // เหลืองอำพัน — อยากเปลี่ยนสี/ความสว่างทีหลัง ปรับที่ material ตรง Emission Inputs ได้เลย
    private static readonly Color GlowColor = new Color(1f, 0.78f, 0.15f);
    private const float GlowIntensity = 3f;

    [MenuItem("Tools/Yantra/Setup (ใช้ครั้งเดียว)/Tani Glowing Eyes (ตาเรืองแสงตานี)")]
    private static void Apply()
    {
        Material mat = Find<Material>(MaterialName, "t:Material");
        Texture2D mask = Find<Texture2D>(MaskName, "t:Texture2D");

        if (mat == null || mask == null)
        {
            EditorUtility.DisplayDialog(Title,
                "หาไม่เจอ:\n" +
                (mat == null ? "- material " + MaterialName + "\n" : "") +
                (mask == null ? "- รูป " + MaskName + ".png\n" : ""), "ตกลง");
            return;
        }

        Undo.RecordObject(mat, Title);

        mat.SetTexture("_EmissiveColorMap", mask);
        HDMaterial.SetUseEmissiveIntensity(mat, true);
        HDMaterial.SetEmissiveColor(mat, GlowColor);
        HDMaterial.SetEmissiveIntensity(mat, GlowIntensity, EmissiveIntensityUnit.Nits);
        mat.SetFloat("_EmissiveExposureWeight", 0f);   // ไม่ขึ้นกับ exposure
        mat.SetFloat("_AlbedoAffectEmissive", 0f);     // สีตาเดิม (น้ำตาล) ไม่ไปย้อมแสง
        HDMaterial.ValidateMaterial(mat);

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssetIfDirty(mat);
        Selection.activeObject = mat;

        Debug.Log("[TaniEyeGlowSetup] ใส่ตาเรืองแสงให้ " + mat.name + " แล้ว", mat);
        EditorUtility.DisplayDialog(Title,
            "ใส่ตาเรืองแสงให้ " + mat.name + " แล้ว\n\n" +
            "อยากปรับ: เลือก material นี้ > Emission Inputs\n" +
            "- Emissive Color = สี\n- Emission Intensity = ความสว่าง (ตอนนี้ " + GlowIntensity + ")", "ตกลง");
    }

    private static T Find<T>(string name, string filter) where T : Object
    {
        foreach (string guid in AssetDatabase.FindAssets(name + " " + filter))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == name)
                return AssetDatabase.LoadAssetAtPath<T>(path);
        }
        return null;
    }
}
#endif
