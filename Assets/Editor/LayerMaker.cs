using System.IO;
using UnityEditor;
using UnityEngine;

namespace Yantra.TerrainTools
{
    // สร้าง Terrain Layer พร้อมตั้งค่ารูปให้ถูก (Normal Map / Mask Map ไม่ใช้ sRGB)
    internal static class LayerMaker
    {
        public static void AutoAssign(Object[] dropped, ref Texture2D diffuse, ref Texture2D normal, ref Texture2D mask, ref string layerName)
        {
            foreach (Object o in dropped)
            {
                var tex = o as Texture2D;
                if (tex == null) continue;

                string n = tex.name.ToLowerInvariant();
                if (n.Contains("normal") || n.EndsWith("_n") || n.EndsWith("_nrm") || n.EndsWith("_nor"))
                    normal = tex;
                else if (n.Contains("mask") || n.EndsWith("_m") || n.EndsWith("_mods"))
                    mask = tex;
                else
                    diffuse = tex;
            }

            if (string.IsNullOrEmpty(layerName) && diffuse != null)
                layerName = "TL_" + StripSuffix(diffuse.name);
        }

        static string StripSuffix(string name)
        {
            string[] suffixes = { "_BaseColor", "_basecolor", "_Albedo", "_albedo", "_Diffuse", "_diffuse", "_Color", "_color", "_D", "_d" };
            foreach (string s in suffixes)
                if (name.EndsWith(s)) return name.Substring(0, name.Length - s.Length);
            return name;
        }

        public static TerrainLayer Create(Texture2D diffuse, Texture2D normal, Texture2D mask, string layerName, Vector2 tileSize, Terrain addTo)
        {
            if (normal != null) FixImporter(normal, true);
            if (mask != null) FixImporter(mask, false);

            var layer = new TerrainLayer
            {
                diffuseTexture = diffuse,
                normalMapTexture = normal,
                maskMapTexture = mask,
                tileSize = tileSize
            };

            string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(diffuse)).Replace('\\', '/');
            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder)) folder = "Assets";
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + layerName + ".terrainlayer");
            AssetDatabase.CreateAsset(layer, path);
            AssetDatabase.SaveAssets();

            if (addTo != null && addTo.terrainData != null)
            {
                TerrainData td = addTo.terrainData;
                TerrainLayer[] layers = td.terrainLayers;
                if (layers.Length >= 8)
                {
                    EditorUtility.DisplayDialog("ใส่ไม่ได้", "Terrain “" + addTo.name + "” มีสีพื้นครบ 8 Layer แล้ว\nสร้างไฟล์ Layer ให้แล้ว แต่ไม่ได้ใส่ลง Terrain", "เข้าใจแล้ว");
                }
                else
                {
                    Undo.RecordObject(td, "เพิ่ม Terrain Layer");
                    var next = new TerrainLayer[layers.Length + 1];
                    layers.CopyTo(next, 0);
                    next[layers.Length] = layer;
                    td.terrainLayers = next;
                    EditorUtility.SetDirty(td);
                }
            }

            EditorGUIUtility.PingObject(layer);
            return layer;
        }

        static void FixImporter(Texture2D tex, bool isNormal)
        {
            string path = AssetDatabase.GetAssetPath(tex);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            bool changed = false;
            if (isNormal && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                changed = true;
            }
            if (!isNormal && importer.sRGBTexture)
            {
                importer.sRGBTexture = false;
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
        }
    }
}
