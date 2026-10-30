using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.HighDefinition;
using System.IO;

public class ItemIconGenerator : EditorWindow
{
    private GameObject targetPrefab;
    private int iconSize = 512;
    private Vector3 cameraOffset = new Vector3(0, 0.2f, -2.5f);
    private Vector3 modelRotation = new Vector3(0, 145f, 0);
    private float cameraOrthoSize = 1f;
    private bool useOrthographic = false;

    [Header("HDRP Settings")]
    private bool disablePostProcessing = true;
    private bool useCustomLight = true;
    private float lightIntensity = 10000f; // ค่าความสว่างสำหรับ HDRP (Lux)

    private string saveFolderPath = "Assets/Icons";
    private Texture2D previewTexture;

    [MenuItem("Tools/Item Icon Generator (HDRP)")]
    public static void ShowWindow()
    {
        GetWindow<ItemIconGenerator>("Icon Generator HDRP");
    }

    private void OnGUI()
    {
        GUILayout.Label("ตั้งค่าโมเดลและรูปภาพ", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        targetPrefab = (GameObject)EditorGUILayout.ObjectField("Model / Prefab", targetPrefab, typeof(GameObject), false);
        iconSize = EditorGUILayout.IntPopup("ขนาดรูป (px)", iconSize, new string[] { "128", "256", "512", "1024" }, new int[] { 128, 256, 512, 1024 });

        GUILayout.Space(10);
        GUILayout.Label("ปรับมุมกล้องและโมเดล", EditorStyles.boldLabel);
        cameraOffset = EditorGUILayout.Vector3Field("ระยะห่างกล้อง (Offset)", cameraOffset);
        modelRotation = EditorGUILayout.Vector3Field("หมุนโมเดล (Rotation)", modelRotation);

        useOrthographic = EditorGUILayout.Toggle("กล้องแบบ Orthographic", useOrthographic);
        if (useOrthographic)
        {
            cameraOrthoSize = EditorGUILayout.Slider("ขนาดซูม (Ortho Size)", cameraOrthoSize, 0.1f, 10f);
        }

        GUILayout.Space(10);
        GUILayout.Label("ตั้งค่าแสงและเรนเดอร์ (HDRP)", EditorStyles.boldLabel);
        disablePostProcessing = EditorGUILayout.Toggle("ปิด Post-Processing", disablePostProcessing);
        useCustomLight = EditorGUILayout.Toggle("สร้างไฟส่องไอเทมเพิ่ม", useCustomLight);
        if (useCustomLight)
        {
            lightIntensity = EditorGUILayout.Slider("ความสว่างไฟ", lightIntensity, 100f, 100000f);
        }

        if (EditorGUI.EndChangeCheck() && targetPrefab != null)
        {
            UpdatePreview();
        }

        GUILayout.Space(10);
        saveFolderPath = EditorGUILayout.TextField("โฟลเดอร์ที่เซฟ", saveFolderPath);

        GUILayout.Space(15);

        if (previewTexture != null)
        {
            GUILayout.Label("Preview (ตารางหมากรุก = โปร่งใส):", EditorStyles.boldLabel);
            Rect rect = GUILayoutUtility.GetRect(200, 200, GUILayout.ExpandWidth(false));
            EditorGUI.DrawTextureTransparent(rect, previewTexture, ScaleMode.ScaleToFit);
        }

        GUILayout.Space(15);

        GUI.enabled = targetPrefab != null;
        if (GUILayout.Button("สร้างและบันทึกเป็น Sprite (Generate Icon)", GUILayout.Height(40)))
        {
            SaveIcon();
        }
        GUI.enabled = true;
    }

    private void UpdatePreview()
    {
        if (previewTexture != null) DestroyImmediate(previewTexture);
        previewTexture = RenderModelToTexture(256);
    }

    private Texture2D RenderModelToTexture(int size)
    {
        if (targetPrefab == null) return null;

        Vector3 spawnPos = new Vector3(10000, 10000, 10000);
        GameObject instance = Instantiate(targetPrefab, spawnPos, Quaternion.Euler(modelRotation));
        Bounds bounds = CalculateBounds(instance);

        // สร้างกล้องพร้อม HDAdditionalCameraData สำหรับ HDRP
        GameObject camObj = new GameObject("TempHDRPIconCam");
        Camera cam = camObj.AddComponent<Camera>();
        HDAdditionalCameraData hdCam = camObj.AddComponent<HDAdditionalCameraData>();

        cam.orthographic = useOrthographic;
        cam.orthographicSize = cameraOrthoSize;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 100f;

        // ตั้งค่าพื้นหลังโปร่งใสด้วยระบบของ HDRP
        hdCam.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
        hdCam.backgroundColorHDR = new Color(0, 0, 0, 0);

        // ปิด Post-Processing และ Exposure อัตโนมัติหากไม่ต้องการให้กวนค่า Alpha หรือความสว่าง
        if (disablePostProcessing)
        {
            hdCam.customRenderingSettings = true;
            hdCam.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)FrameSettingsField.Postprocess] = true;
            hdCam.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.Postprocess, false);
        }

        camObj.transform.position = bounds.center + cameraOffset;
        camObj.transform.LookAt(bounds.center);

        // สร้างไฟเสริมสำหรับ HDRP
        GameObject lightObj = null;
        if (useCustomLight)
        {
            lightObj = new GameObject("TempHDRPIconLight");
            Light light = lightObj.AddComponent<Light>();
            HDAdditionalLightData hdLight = lightObj.AddComponent<HDAdditionalLightData>();

            light.type = LightType.Directional;
            light.intensity = lightIntensity;
            lightObj.transform.rotation = camObj.transform.rotation * Quaternion.Euler(25f, -30f, 0f);
        }

        // ใช้ RenderTexture แบบ ARGBHalf เพื่อรองรับข้อมูลสีและ Alpha ของ HDRP
        RenderTexture rt = RenderTexture.GetTemporary(size, size, 24, RenderTextureFormat.ARGBHalf);
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        tex.Apply();

        // ล้างค่าทั้งหมด
        RenderTexture.active = null;
        cam.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);

        DestroyImmediate(instance);
        DestroyImmediate(camObj);
        if (lightObj != null) DestroyImmediate(lightObj);

        return tex;
    }

    private Bounds CalculateBounds(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(obj.transform.position, Vector3.one);

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            b.Encapsulate(renderers[i].bounds);
        }
        return b;
    }

    private void SaveIcon()
    {
        Texture2D finalTex = RenderModelToTexture(iconSize);
        if (finalTex == null) return;

        if (!Directory.Exists(saveFolderPath))
        {
            Directory.CreateDirectory(saveFolderPath);
        }

        string fileName = $"{targetPrefab.name}_Icon.png";
        string fullPath = Path.Combine(saveFolderPath, fileName);

        byte[] bytes = finalTex.EncodeToPNG();
        File.WriteAllBytes(fullPath, bytes);
        DestroyImmediate(finalTex);

        AssetDatabase.Refresh();

        TextureImporter importer = AssetImporter.GetAtPath(fullPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        Debug.Log($"สร้างไอคอน HDRP สำเร็จ! บันทึกไว้ที่: {fullPath}");

        Object savedAsset = AssetDatabase.LoadAssetAtPath<Object>(fullPath);
        EditorGUIUtility.PingObject(savedAsset);
    }
}