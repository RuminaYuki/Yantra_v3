using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SDFcl.Tool.TreeGenerator
{
    /// <summary>Bake ต้นไม้เป็น Prefab (Mesh รวม + LOD + Atlas ใบ + Collider)</summary>
    public static class TreeBaker
    {
        // ------------------------------------------------------------------ paths

        public static string FullPath(string assetPath)
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(root, assetPath).Replace('\\', '/');
        }

        public static string ToAssetPath(string absolute)
        {
            if (string.IsNullOrEmpty(absolute)) return null;
            absolute = absolute.Replace('\\', '/').TrimEnd('/');
            string data = Application.dataPath.Replace('\\', '/');
            if (string.Equals(absolute, data, StringComparison.OrdinalIgnoreCase)) return "Assets";
            if (absolute.StartsWith(data + "/", StringComparison.OrdinalIgnoreCase)) return "Assets" + absolute.Substring(data.Length);
            return null;
        }

        public static bool IsAssetFolderPath(string path)
        {
            return !string.IsNullOrEmpty(path) && (path == "Assets" || path.StartsWith("Assets/"));
        }

        public static string SafeName(string n)
        {
            if (string.IsNullOrWhiteSpace(n)) n = "Tree";
            foreach (char c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
            n = n.Replace('/', '_').Replace('\\', '_').Trim();
            return n.Length == 0 ? "Tree" : n;
        }

        public static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/').TrimEnd('/');
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path);
            parent = string.IsNullOrEmpty(parent) ? "Assets" : parent.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ------------------------------------------------------------------ Bake

        public static string Bake(TreePreset p, string folder, string name)
        {
            if (!IsAssetFolderPath(folder)) throw new Exception("โฟลเดอร์ปลายทางต้องอยู่ใน Assets");
            name = SafeName(name);
            string dir = folder.TrimEnd('/') + "/" + name;
            EnsureFolder(dir);

            var results = new TreeBuildResult[3];
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    EditorUtility.DisplayProgressBar("Tree Generator", "สร้าง LOD" + i + " …", 0.1f + i * 0.2f);
                    results[i] = TreeBuilder.Build(p, TreeBuildOptions.ForLOD(i));
                }

                int types = results[0].leafTypeCount;
                bool hasLeaves = results[0].leafCount > 0;

                // ---------- แยกใบที่รวม atlas ได้ กับใบที่ shader ต่าง
                Shader primary = TreeMaterialUtil.EffectiveLeafShader(p, 0);
                var atlasTypes = new List<int>();
                var sepTypes = new List<int>();
                for (int k = 0; k < types; k++)
                {
                    if (TreeMaterialUtil.EffectiveLeafShader(p, k) == primary) atlasTypes.Add(k);
                    else sepTypes.Add(k);
                }

                Rect[] rects = null;
                Material atlasMat = null;
                if (hasLeaves && atlasTypes.Count > 0)
                {
                    EditorUtility.DisplayProgressBar("Tree Generator", "รวมใบเป็น Atlas …", 0.75f);
                    int cols = Mathf.CeilToInt(Mathf.Sqrt(atlasTypes.Count));
                    int tile = Mathf.Max(32, p.atlasSize / cols - p.atlasPadding * 2);
                    var tiles = new Texture2D[atlasTypes.Count];
                    for (int i = 0; i < tiles.Length; i++) tiles[i] = TreeMaterialUtil.MakeLeafTile(p, atlasTypes[i], tile);
                    var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                    rects = atlas.PackTextures(tiles, p.atlasPadding, p.atlasSize, false);

                    string atlasPath = dir + "/" + name + "_LeafAtlas.png";
                    File.WriteAllBytes(FullPath(atlasPath), atlas.EncodeToPNG());
                    Object.DestroyImmediate(atlas);
                    foreach (Texture2D t in tiles) Object.DestroyImmediate(t);

                    AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceUpdate);
                    var imp = AssetImporter.GetAtPath(atlasPath) as TextureImporter;
                    if (imp != null)
                    {
                        imp.textureType = TextureImporterType.Default;
                        imp.alphaIsTransparency = true;
                        imp.mipmapEnabled = true;
                        imp.sRGBTexture = true;
                        imp.wrapMode = TextureWrapMode.Clamp;
                        imp.SaveAndReimport();
                    }
                    var atlasTex = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);

                    Material template = TreeMaterialUtil.LeafTemplate(p, atlasTypes[0]);
                    TreeMaterialUtil.SetMainTexture(template, atlasTex);
                    TreeMaterialUtil.SetMainColor(template, Color.white);
                    atlasMat = SaveMaterial(template, dir + "/" + name + "_Leaves.mat");
                }

                Material barkMat = BarkMaterialForBake(p, results[0].barkIndex, folder);

                // ---------- materials ตามลำดับ submesh
                var mats = new List<Material> { barkMat };
                if (hasLeaves)
                {
                    if (atlasMat != null) mats.Add(atlasMat);
                    foreach (int k in sepTypes) mats.Add(p.leafMaterials[k].material);
                }

                // ---------- meshes
                EditorUtility.DisplayProgressBar("Tree Generator", "บันทึก Mesh …", 0.85f);
                var meshes = new Mesh[3];
                for (int i = 0; i < 3; i++)
                {
                    meshes[i] = Combine(results[i].mesh, hasLeaves, atlasTypes, rects, sepTypes);
                    meshes[i].name = name + "_LOD" + i;
                }

                string meshPath = dir + "/" + name + "_Mesh.asset";
                if (AssetDatabase.LoadMainAssetAtPath(meshPath) != null) AssetDatabase.DeleteAsset(meshPath);
                AssetDatabase.CreateAsset(meshes[0], meshPath);
                AssetDatabase.AddObjectToAsset(meshes[1], meshPath);
                AssetDatabase.AddObjectToAsset(meshes[2], meshPath);
                AssetDatabase.ImportAsset(meshPath);

                // ---------- prefab
                EditorUtility.DisplayProgressBar("Tree Generator", "สร้าง Prefab …", 0.95f);
                var root = new GameObject(name);
                var renderers = new Renderer[3];
                Material[] matArray = mats.ToArray();
                for (int i = 0; i < 3; i++)
                {
                    var go = new GameObject(name + "_LOD" + i);
                    go.transform.SetParent(root.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = meshes[i];
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterials = matArray;
                    if (i == 2 && p.lodShadowsOff) mr.shadowCastingMode = ShadowCastingMode.Off;
                    renderers[i] = mr;
                }

                var group = root.AddComponent<LODGroup>();
                group.SetLODs(new[]
                {
                    new LOD(0.4f, new[] { renderers[0] }),
                    new LOD(0.15f, new[] { renderers[1] }),
                    new LOD(0.025f, new[] { renderers[2] })
                });
                group.RecalculateBounds();

                if (p.trunkCollider)
                {
                    var cap = root.AddComponent<CapsuleCollider>();
                    float radius = Mathf.Max(0.05f, results[0].trunkBaseRadius * 0.9f);
                    float height = Mathf.Max(results[0].firstForkHeight, radius * 2f + 0.1f);
                    cap.radius = radius;
                    cap.height = height;
                    cap.center = new Vector3(0f, height * 0.5f, 0f);
                }

                string prefabPath = dir + "/" + name + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Object.DestroyImmediate(root);
                AssetDatabase.SaveAssets();
                return prefabPath;
            }
            finally
            {
                foreach (TreeBuildResult r in results)
                    if (r != null && r.mesh != null) Object.DestroyImmediate(r.mesh);
                EditorUtility.ClearProgressBar();
            }
        }

        public static List<string> BakeBatch(TreePreset p, string folder, string baseName, int count)
        {
            var paths = new List<string>();
            baseName = SafeName(baseName);
            for (int i = 0; i < count; i++)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Bake หลายต้น", (i + 1) + " / " + count, i / (float)count)) break;
                TreePreset v = TreeVariation.CreateVariant(p, unchecked(p.seed * 1000 + i + 1), null);
                try
                {
                    paths.Add(Bake(v, folder, baseName + "_v" + (i + 1).ToString("00")));
                }
                finally
                {
                    Object.DestroyImmediate(v);
                }
            }
            EditorUtility.ClearProgressBar();
            return paths;
        }

        // ------------------------------------------------------------------ helpers

        static Mesh Combine(Mesh src, bool hasLeaves, List<int> atlasTypes, Rect[] rects, List<int> sepTypes)
        {
            var m = new Mesh { indexFormat = src.indexFormat };
            Vector3[] v = src.vertices;
            Vector2[] uv = src.uv;
            var uv2 = new List<Vector2>();
            src.GetUVs(1, uv2);
            Color[] cols = src.colors;
            var done = new bool[v.Length];

            var subs = new List<int[]> { src.GetTriangles(0) };
            if (hasLeaves)
            {
                if (atlasTypes.Count > 0 && rects != null)
                {
                    var merged = new List<int>();
                    for (int ai = 0; ai < atlasTypes.Count; ai++)
                    {
                        int sub = 1 + atlasTypes[ai];
                        if (sub >= src.subMeshCount) continue;
                        int[] t = src.GetTriangles(sub);
                        Rect rc = rects[ai];
                        foreach (int idx in t)
                        {
                            if (done[idx]) continue;
                            uv[idx] = new Vector2(rc.x + uv[idx].x * rc.width, rc.y + uv[idx].y * rc.height);
                            done[idx] = true;
                        }
                        merged.AddRange(t);
                    }
                    subs.Add(merged.ToArray());
                }
                foreach (int k in sepTypes)
                    subs.Add(1 + k < src.subMeshCount ? src.GetTriangles(1 + k) : new int[0]);
            }

            m.vertices = v;
            m.normals = src.normals;
            m.tangents = src.tangents;
            m.uv = uv;
            if (uv2.Count == v.Length) m.SetUVs(1, uv2);
            if (cols != null && cols.Length == v.Length) m.colors = cols;
            m.subMeshCount = subs.Count;
            for (int i = 0; i < subs.Count; i++) m.SetTriangles(subs[i], i, false);
            m.RecalculateBounds();
            return m;
        }

        static Material SaveMaterial(Material m, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.shader = m.shader;
                existing.CopyPropertiesFromMaterial(m);
                existing.shaderKeywords = m.shaderKeywords;
                existing.renderQueue = m.renderQueue;
                TreeMaterialUtil.HDRPValidate(existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(m);
                return existing;
            }
            m.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(m, path);
            TreeMaterialUtil.HDRPValidate(m);
            return m;
        }

        static Material BarkMaterialForBake(TreePreset p, int barkIndex, string folder)
        {
            WeightedMaterial entry = barkIndex >= 0 && barkIndex < p.barkMaterials.Count ? p.barkMaterials[barkIndex] : null;
            if (entry != null && entry.material != null) return entry.material;

            Color c = entry != null ? entry.color : p.barkColor;
            string shared = folder.TrimEnd('/') + "/_Shared";
            EnsureFolder(shared);

            string texPath = shared + "/Bark_Default.png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(texPath) == null)
            {
                File.WriteAllBytes(FullPath(texPath), TreeMaterialUtil.ProceduralBarkTexture.EncodeToPNG());
                AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
                var imp = AssetImporter.GetAtPath(texPath) as TextureImporter;
                if (imp != null)
                {
                    imp.wrapMode = TextureWrapMode.Repeat;
                    imp.sRGBTexture = true;
                    imp.SaveAndReimport();
                }
            }

            string matPath = shared + "/Bark_" + ColorUtility.ToHtmlStringRGB(c) + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existing != null) return existing;
            Material m = TreeMaterialUtil.CreateMaterial(c, AssetDatabase.LoadAssetAtPath<Texture2D>(texPath), false, false);
            AssetDatabase.CreateAsset(m, matPath);
            return m;
        }
    }
}
