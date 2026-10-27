using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Yantra.TerrainTools
{
    // ซ่อนต้นไม้ชั่วคราวตอนจัดฉาก — ตอนเซฟจะเปิดคืนในไฟล์ให้เสมอ ลืมเปิดคืนก็ไม่หลุดไปใน git
    [InitializeOnLoad]
    internal static class HideTrees
    {
        static readonly List<Terrain> RestoreAfterSave = new List<Terrain>();

        static HideTrees()
        {
            EditorSceneManager.sceneSaving += OnSceneSaving;
            EditorSceneManager.sceneSaved += OnSceneSaved;
            SceneView.duringSceneGui += DrawBanner;
        }

        public static bool AnyHidden
        {
            get
            {
                foreach (Terrain t in Terrain.activeTerrains)
                    if (!t.drawTreesAndFoliage) return true;
                return false;
            }
        }

        public static void Hide()
        {
            foreach (Terrain t in Terrain.activeTerrains)
                t.drawTreesAndFoliage = false;
            SceneView.RepaintAll();
        }

        public static void Show()
        {
            foreach (Terrain t in Terrain.activeTerrains)
                t.drawTreesAndFoliage = true;
            SceneView.RepaintAll();
        }

        public static void Toggle()
        {
            if (AnyHidden) Show();
            else Hide();
            TerrainToolsWindow.RepaintOpen();
        }

        [Shortcut("Yantra/สลับซ่อนต้นไม้ชั่วคราว", KeyCode.T, ShortcutModifiers.Alt | ShortcutModifiers.Shift)]
        static void ToggleShortcut()
        {
            Toggle();
        }

        static void OnSceneSaving(Scene scene, string path)
        {
            RestoreAfterSave.Clear();
            foreach (Terrain t in Terrain.activeTerrains)
            {
                if (t.gameObject.scene != scene || t.drawTreesAndFoliage) continue;
                t.drawTreesAndFoliage = true;
                RestoreAfterSave.Add(t);
            }
        }

        static void OnSceneSaved(Scene scene)
        {
            foreach (Terrain t in RestoreAfterSave)
                if (t != null) t.drawTreesAndFoliage = false;
            RestoreAfterSave.Clear();
        }

        static void DrawBanner(SceneView sv)
        {
            if (!AnyHidden) return;
            TTGui.SceneBanner(sv, 0, "ต้นไม้ซ่อนอยู่ชั่วคราว (Shift+Alt+T)", new Color(0.75f, 0.2f, 0.18f), "เปิดคืน", Show);
        }
    }
}
