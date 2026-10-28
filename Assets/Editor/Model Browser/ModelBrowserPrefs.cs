using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ModelBrowser
{
    /// <summary>Per-machine settings (EditorPrefs). Recent list is per project.</summary>
    public static class ModelBrowserPrefs
    {
        const string Prefix = "ModelBrowser.";

        // ---- Placement ----
        public static bool Snap { get => EditorPrefs.GetBool(Prefix + "Snap", true); set => EditorPrefs.SetBool(Prefix + "Snap", value); }
        public static bool SnapY { get => EditorPrefs.GetBool(Prefix + "SnapY", false); set => EditorPrefs.SetBool(Prefix + "SnapY", value); }
        public static float GridSize { get => EditorPrefs.GetFloat(Prefix + "GridSize", 1f); set => EditorPrefs.SetFloat(Prefix + "GridSize", Mathf.Max(0.01f, value)); }
        public static float RotationStep { get => EditorPrefs.GetFloat(Prefix + "RotationStep", 90f); set => EditorPrefs.SetFloat(Prefix + "RotationStep", value); }
        public static bool UseParent { get => EditorPrefs.GetBool(Prefix + "UseParent", false); set => EditorPrefs.SetBool(Prefix + "UseParent", value); }
        /// <summary>true = stick to surfaces under the mouse; false = always use the horizontal plane.</summary>
        public static bool SurfaceMode { get => EditorPrefs.GetBool(Prefix + "SurfaceMode", true); set => EditorPrefs.SetBool(Prefix + "SurfaceMode", value); }
        public static float PlaneHeight { get => EditorPrefs.GetFloat(Prefix + "PlaneHeight", 0f); set => EditorPrefs.SetFloat(Prefix + "PlaneHeight", value); }
        /// <summary>Lift the object so the bottom of its bounds sits on the surface (for prefabs whose pivot isn't at the bottom).</summary>
        /// <summary>Layers the placement ray may stick to (e.g. only "Ground"). Default: everything.</summary>
        public static int SurfaceMask { get => EditorPrefs.GetInt(Prefix + "SurfaceMask", ~0); set => EditorPrefs.SetInt(Prefix + "SurfaceMask", value); }
        public static bool ShowBounds { get => EditorPrefs.GetBool(Prefix + "ShowBounds", false); set => EditorPrefs.SetBool(Prefix + "ShowBounds", value); }
        public static bool AlignBase { get => EditorPrefs.GetBool(Prefix + "AlignBase", true); set => EditorPrefs.SetBool(Prefix + "AlignBase", value); }
        /// <summary>Step for manual height offset (E / Q, Ctrl+Scroll). Uses Grid size when Snap Y is on.</summary>
        public static float HeightStep { get => EditorPrefs.GetFloat(Prefix + "HeightStep", 0.25f); set => EditorPrefs.SetFloat(Prefix + "HeightStep", Mathf.Max(0.001f, value)); }

        // ---- Thumbnails ----
        /// <summary>false = use Unity's AssetPreview only (handy for checking whether a GPU error comes from our renderer).</summary>
        public static bool UseCustomThumbnails { get => EditorPrefs.GetBool(Prefix + "UseCustomThumbs", true); set => EditorPrefs.SetBool(Prefix + "UseCustomThumbs", value); }
        public static float ThumbYaw { get => EditorPrefs.GetFloat(Prefix + "ThumbYaw", 215f); set => EditorPrefs.SetFloat(Prefix + "ThumbYaw", value); }
        public static float ThumbPitch { get => EditorPrefs.GetFloat(Prefix + "ThumbPitch", 25f); set => EditorPrefs.SetFloat(Prefix + "ThumbPitch", value); }
        public static float ThumbLight { get => EditorPrefs.GetFloat(Prefix + "ThumbLight", 1.3f); set => EditorPrefs.SetFloat(Prefix + "ThumbLight", value); }
        public static float ThumbAmbient { get => EditorPrefs.GetFloat(Prefix + "ThumbAmbient", 0.35f); set => EditorPrefs.SetFloat(Prefix + "ThumbAmbient", value); }
        public static Color ThumbBackground
        {
            get => ColorUtility.TryParseHtmlString("#" + EditorPrefs.GetString(Prefix + "ThumbBg", "3A3A3AFF"), out var c) ? c : new Color(0.23f, 0.23f, 0.23f);
            set => EditorPrefs.SetString(Prefix + "ThumbBg", ColorUtility.ToHtmlStringRGBA(value));
        }

        // ---- Recent (per project) ----
        public static event Action RecentChanged;
        static string RecentKey => Prefix + "Recent." + StableHash(Application.dataPath);
        const int MaxRecent = 40;

        public static List<string> GetRecentGuids() =>
            EditorPrefs.GetString(RecentKey, "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).ToList();

        public static void AddRecent(string guid)
        {
            var list = GetRecentGuids();
            list.Remove(guid);
            list.Insert(0, guid);
            if (list.Count > MaxRecent) list.RemoveRange(MaxRecent, list.Count - MaxRecent);
            EditorPrefs.SetString(RecentKey, string.Join("|", list));
            RecentChanged?.Invoke();
        }

        static uint StableHash(string s)
        {
            uint h = 2166136261;
            foreach (char c in s) { h ^= c; h *= 16777619; }
            return h;
        }
    }

    /// <summary>Notifies the window when prefabs/models/folders are added, removed or moved.</summary>
    class ModelBrowserAssetWatcher : AssetPostprocessor
    {
        public static event Action AssetsChanged;

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (AssetsChanged == null) return;
            bool relevant = imported.Concat(deleted).Concat(moved).Concat(movedFrom)
                .Any(p => ModelBrowserDatabase.IsPlaceableFile(p) || !System.IO.Path.HasExtension(p));
            if (relevant) AssetsChanged.Invoke();
        }
    }
}
