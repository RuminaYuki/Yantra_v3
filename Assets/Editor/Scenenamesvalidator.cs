#if UNITY_EDITOR
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ตรวจตอนเปิด Editor ว่าชื่อทุกตัวใน SceneNames ยังมี scene จริงและอยู่ใน Build List
/// ถ้ามีคนเปลี่ยนชื่อ scene ตอน merge จะรู้ทันทีแทนที่จะไปพังตอน runtime
/// วางไฟล์นี้ในโฟลเดอร์ชื่อ Editor เท่านั้น
/// </summary>
[InitializeOnLoad]
public static class SceneNamesValidator
{
    static SceneNamesValidator()
    {
        EditorApplication.delayCall += Validate;
    }

    [MenuItem("Tools/Yantra/ตรวจสอบชื่อ Scene")]
    public static void Validate()
    {
        var fields = typeof(SceneNames)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string));

        var inBuild = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path))
            .ToArray();

        foreach (var field in fields)
        {
            string sceneName = (string)field.GetRawConstantValue();

            if (!inBuild.Contains(sceneName))
            {
                Debug.LogError(
                    $"[SceneNames] '{field.Name}' ชี้ไปที่ scene ชื่อ \"{sceneName}\" " +
                    $"ซึ่งไม่มีใน Build List\n" +
                    $"scene ที่มีตอนนี้: {string.Join(", ", inBuild)}");
            }
        }
    }
}
#endif