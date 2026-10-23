using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// ต้องอยู่ในโฟลเดอร์ Editor
public static class FogWallCreator
{
    private const string ShaderName = "Shader Graphs/FogWall";

    [MenuItem("Tools/VFX/กำแพงหมอก/สร้างกลุ่ม (พื้นที่ต่อสู้)")]
    private static void CreateGroup()
    {
        var go = new GameObject("ArenaFogWalls");
        go.AddComponent<FogWallGroup>();
        PlaceAtSceneView(go.transform);
        Undo.RegisterCreatedObjectUndo(go, "สร้างกลุ่มกำแพงหมอก");
        Selection.activeGameObject = go;
    }

    // เลือกกำแพงของเพื่อน (เช่น Barrier) ก่อน = หมอกจะขึ้นพอดีกำแพงนั้น และเริ่มแบบปิดอยู่
    [MenuItem("Tools/VFX/กำแพงหมอก/สร้างกำแพง (เลือก Barrier หรือกลุ่มก่อน)")]
    private static void CreateWall()
    {
        Material mat = GetOrCreateMaterial();
        if (mat == null) return;

        var go = new GameObject("FogWall");
        Transform selected = Selection.activeTransform;
        Transform parent = selected;
        if (selected != null && selected.GetComponent<FogWall>() != null)
            parent = selected.parent;   // เลือกกำแพงอยู่ ให้ไปเป็นพี่น้องกัน ไม่ซ้อนในกำแพง

        Renderer fitTo = null;
        if (parent != null && parent.GetComponent<FogWallGroup>() != null)
        {
            go.transform.SetParent(parent, false);
            go.transform.position = selected.position;
        }
        else if (selected != null && selected.GetComponent<Renderer>() != null)
        {
            fitTo = selected.GetComponent<Renderer>();
            go.transform.SetParent(selected.parent, false);
        }
        else
        {
            PlaceAtSceneView(go.transform);
        }

        var wall = go.AddComponent<FogWall>();
        var so = new SerializedObject(wall);
        so.FindProperty("_material").objectReferenceValue = mat;
        if (fitTo != null) FitToRenderer(go.transform, so, fitTo);
        so.ApplyModifiedPropertiesWithoutUndo();

        Undo.RegisterCreatedObjectUndo(go, "สร้างกำแพงหมอก");
        Selection.activeGameObject = go;
    }

    // วางให้ทับกำแพงเดิม: ด้านยาวของกล่อง = ความกว้างหมอก, ฐานอยู่ที่ก้นกล่อง
    private static void FitToRenderer(Transform wall, SerializedObject so, Renderer target)
    {
        Transform t = target.transform;
        Bounds local = target.localBounds;
        Vector3 size = Vector3.Scale(local.size, t.lossyScale);
        size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));

        Vector3 bottom = t.TransformPoint(local.center - new Vector3(0f, local.extents.y, 0f));
        bool alongX = size.x >= size.z;

        wall.position = bottom;
        float yaw = t.eulerAngles.y + (alongX ? 0f : 90f);   // หมุนแค่แกน Y หมอกจะได้ตั้งตรงเสมอ
        wall.rotation = Quaternion.Euler(0f, yaw, 0f);

        so.FindProperty("_width").floatValue = alongX ? size.x : size.z;
        so.FindProperty("_height").floatValue = Mathf.Max(2f, size.y);
        so.FindProperty("_startClosed").boolValue = true;
    }

    private static void PlaceAtSceneView(Transform t)
    {
        SceneView view = SceneView.lastActiveSceneView;
        if (view == null) return;
        Vector3 pos = view.pivot;
        if (Physics.Raycast(pos + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 200f))
            pos = hit.point;
        t.position = pos;
    }

    private static Material GetOrCreateMaterial()
    {
        string[] found = AssetDatabase.FindAssets("FogWall_Mat t:Material");
        if (found.Length > 0)
            return AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(found[0]));

        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            EditorUtility.DisplayDialog("กำแพงหมอก", "หา Shader FogWall ไม่เจอ\nวาง FogWall.shadergraph ในโปรเจกต์ก่อน", "ตกลง");
            return null;
        }

        string dir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(shader)).Replace('\\', '/');
        var mat = new Material(shader) { name = "FogWall_Mat" };

        Texture2D noise = FindNoise();
        if (noise != null) mat.SetTexture("_NoiseTex", noise);

        ResetHdrpKeywords(mat);
        AssetDatabase.CreateAsset(mat, dir + "/FogWall_Mat.mat");
        AssetDatabase.SaveAssets();
        return mat;
    }

    private static Texture2D FindNoise()
    {
        string[] found = AssetDatabase.FindAssets("FogWall_Noise t:Texture2D");
        if (found.Length == 0) return null;

        string path = AssetDatabase.GUIDToAssetPath(found[0]);
        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            bool changed = importer.textureType != TextureImporterType.Default
                || importer.sRGBTexture || importer.wrapMode != TextureWrapMode.Repeat || !importer.mipmapEnabled;
            if (changed)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ตั้ง keyword ของ HDRP ให้ material ใหม่ (เรียกผ่าน reflection กันคอมไพล์พังถ้า HDRP เปลี่ยนชื่อ)
    private static void ResetHdrpKeywords(Material mat)
    {
        try
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = asm.GetType("UnityEditor.Rendering.HighDefinition.HDShaderUtils");
                MethodInfo m = t?.GetMethod("ResetMaterialKeywords", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Material) }, null);
                if (m == null) continue;
                m.Invoke(null, new object[] { mat });
                return;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[FogWall] ตั้งค่า material ไม่สำเร็จ ลองคลิก FogWall_Mat ให้ Inspector เปิดหนึ่งครั้ง: " + e.Message);
        }
    }
}