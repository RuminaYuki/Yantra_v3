using UnityEditor;
using UnityEngine;

public class TerrainHeightOffset : EditorWindow
{
    Terrain terrain;
    float raiseMeters = 50f;
    bool compensateTransform = true;

    [MenuItem("Tools/Terrain Height Offset")]
    static void Open() => GetWindow<TerrainHeightOffset>("Terrain Offset");

    void OnGUI()
    {
        terrain = (Terrain)EditorGUILayout.ObjectField("Terrain", terrain, typeof(Terrain), true);
        raiseMeters = EditorGUILayout.FloatField("Dig room (m)", raiseMeters);
        compensateTransform = EditorGUILayout.Toggle("Move terrain down to compensate", compensateTransform);

        GUI.enabled = terrain && raiseMeters > 0f;
        if (GUILayout.Button("Apply")) Apply();
        GUI.enabled = true;
    }

    void Apply()
    {
        TerrainData data = terrain.terrainData;
        Undo.RegisterCompleteObjectUndo(new Object[] { data, terrain.transform }, "Offset Terrain Height");

        int res = data.heightmapResolution;
        float[,] h = data.GetHeights(0, 0, res, res);

        float oldY = data.size.y;
        float newY = oldY + raiseMeters;

        // แปลงค่าความสูงให้อิงกับ Terrain Height ใหม่ รูปทรงเดิมจะไม่ถูกตัดหรือยืด
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
                h[y, x] = (h[y, x] * oldY + raiseMeters) / newY;

        Vector3 size = data.size;
        size.y = newY;
        data.size = size;
        data.SetHeights(0, 0, h);

        if (compensateTransform)
            terrain.transform.position -= Vector3.up * raiseMeters;

        EditorUtility.SetDirty(data);
    }
}