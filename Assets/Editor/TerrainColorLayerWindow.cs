using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class TerrainColorLayerWindow : EditorWindow
{
    const string Folder = "Assets/TerrainColors";

    Terrain terrain;
    string layerName = "Grass";
    Color color = new Color(0.35f, 0.6f, 0.25f);
    float tileSize = 10f;

    [MenuItem("Tools/Terrain Color Layer")]
    static void Open() => GetWindow<TerrainColorLayerWindow>("Terrain Color");

    void OnGUI()
    {
        terrain = (Terrain)EditorGUILayout.ObjectField("Terrain", terrain, typeof(Terrain), true);
        layerName = EditorGUILayout.TextField("Name", layerName);
        color = EditorGUILayout.ColorField("Color", color);
        tileSize = EditorGUILayout.FloatField("Tile Size", tileSize);

        GUI.enabled = terrain && !string.IsNullOrWhiteSpace(layerName);
        if (GUILayout.Button("Add Color Layer")) AddLayer();
        GUI.enabled = true;
    }

    void AddLayer()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets", "TerrainColors");

        // 1) สร้าง texture สีพื้นแล้วบันทึกเป็น PNG
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        tex.SetPixels(Enumerable.Repeat(color, 16).ToArray());
        tex.Apply();

        string texPath = AssetDatabase.GenerateUniqueAssetPath($"{Folder}/{layerName}.png");
        File.WriteAllBytes(texPath, tex.EncodeToPNG());
        DestroyImmediate(tex);
        AssetDatabase.ImportAsset(texPath);
        var diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

        // 2) สร้าง Terrain Layer
        var layer = new TerrainLayer
        {
            diffuseTexture = diffuse,
            tileSize = new Vector2(tileSize, tileSize)
        };
        string layerPath = AssetDatabase.GenerateUniqueAssetPath($"{Folder}/{layerName}.terrainlayer");
        AssetDatabase.CreateAsset(layer, layerPath);

        // 3) ใส่ layer เข้า Terrain
        TerrainData data = terrain.terrainData;
        Undo.RegisterCompleteObjectUndo(data, "Add Terrain Color Layer");
        data.terrainLayers = data.terrainLayers.Append(layer).ToArray();
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
    }
}