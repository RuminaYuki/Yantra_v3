using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ModelBrowser
{
    /// <summary>A folder or a single prefab/model that feeds a sub-category. Stored by GUID so moving/renaming assets doesn't break it.</summary>
    [Serializable]
    public class SourceEntry
    {
        public string guid;
        public bool includeSubfolders = true;

        public string AssetPath => AssetDatabase.GUIDToAssetPath(guid);
        public bool IsFolder => AssetDatabase.IsValidFolder(AssetPath);
    }

    [Serializable]
    public class SubCategory
    {
        public string name = "New";
        public List<SourceEntry> sources = new List<SourceEntry>();
    }

    [Serializable]
    public class MainCategory
    {
        public string name = "New";
        public List<SubCategory> subCategories = new List<SubCategory>();
    }

    /// <summary>Stores categories and favorites. Only references assets — never moves or modifies them.</summary>
    public class ModelBrowserDatabase : ScriptableObject
    {
        public List<MainCategory> categories = new List<MainCategory>();
        public List<string> favorites = new List<string>();
        [Tooltip("Also list model files (FBX, OBJ...) that are not saved as .prefab")]
        public bool includeModelFiles;

        public static event Action Changed;

        static readonly HashSet<string> ModelExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".fbx", ".obj", ".blend", ".dae", ".3ds", ".dxf", ".max", ".ma", ".mb", ".skp"
        };

        public static bool IsPrefabFile(string path) => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
        public static bool IsModelFile(string path) => ModelExtensions.Contains(Path.GetExtension(path));
        public static bool IsPlaceableFile(string path) => IsPrefabFile(path) || IsModelFile(path);

        public static ModelBrowserDatabase FindExisting()
        {
            string guid = AssetDatabase.FindAssets("t:" + nameof(ModelBrowserDatabase)).FirstOrDefault();
            return guid == null ? null : AssetDatabase.LoadAssetAtPath<ModelBrowserDatabase>(AssetDatabase.GUIDToAssetPath(guid));
        }

        public void Save()
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssetIfDirty(this);
            Changed?.Invoke();
        }

        /// <summary>Returns asset paths of every placeable item a source points to.</summary>
        public List<string> Resolve(SourceEntry source)
        {
            var result = new List<string>();
            string path = source.AssetPath;
            if (string.IsNullOrEmpty(path)) return result;

            if (AssetDatabase.IsValidFolder(path))
            {
                foreach (string g in AssetDatabase.FindAssets("t:GameObject", new[] { path }))
                {
                    string p = AssetDatabase.GUIDToAssetPath(g);
                    if (!IsPrefabFile(p) && !(includeModelFiles && IsModelFile(p))) continue;
                    if (!source.includeSubfolders && ParentFolder(p) != path) continue;
                    result.Add(p);
                }
            }
            else if (IsPlaceableFile(path))
            {
                result.Add(path);
            }
            return result;
        }

        public int CountInFolder(string folder, bool includeSubfolders) =>
            Resolve(new SourceEntry { guid = AssetDatabase.AssetPathToGUID(folder), includeSubfolders = includeSubfolders }).Count;

        static string ParentFolder(string path) => Path.GetDirectoryName(path)?.Replace('\\', '/');
    }
}
