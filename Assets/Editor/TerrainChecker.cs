using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Yantra.TerrainTools
{
    internal static class TerrainChecker
    {
        public enum Level { Error, Warning, Info }

        public class Issue
        {
            public Level level;
            public string text;
            public Object target;
            public string fixLabel;
            public System.Action fix;
        }

        public static List<Issue> Run()
        {
            var issues = new List<Issue>();

            foreach (Terrain t in Terrain.activeTerrains)
            {
                TerrainData td = t.terrainData;
                string name = "Terrain “" + t.name + "”";
                if (td == null)
                {
                    Add(issues, Level.Error, name + " ไม่มี TerrainData", t);
                    continue;
                }

                int layers = td.terrainLayers.Length;
                if (layers > 8)
                    Add(issues, Level.Error, name + " มีสีพื้น " + layers + " Layer เกิน 8 สีที่เกินจะหายตอนเล่นจริง", t);

                DetailPrototype[] details = td.detailPrototypes;
                bool instancingFixNeeded = false;
                for (int i = 0; i < details.Length; i++)
                {
                    DetailPrototype d = details[i];
                    string dn = TTTerrain.PrototypeName(d);
                    if (!d.usePrototypeMesh)
                        Add(issues, Level.Error, name + " หญ้า “" + dn + "” เป็นแบบรูปแบน HDRP ไม่แสดง ต้องเปลี่ยนเป็น Detail Mesh", t);
                    else if (d.prototype == null)
                        Add(issues, Level.Error, name + " มีช่องหญ้าที่ไม่ได้ใส่โมเดล", t);
                    else if (!d.useInstancing)
                        instancingFixNeeded = true;
                }
                if (instancingFixNeeded)
                {
                    TerrainData capturedTd = td;
                    Add(issues, Level.Warning, name + " มีหญ้าที่ไม่ได้ติ๊ก Use GPU Instancing", t, "ติ๊กให้", () =>
                    {
                        Undo.RecordObject(capturedTd, "เปิด GPU Instancing");
                        DetailPrototype[] arr = capturedTd.detailPrototypes;
                        foreach (DetailPrototype d in arr)
                            if (d.usePrototypeMesh) d.useInstancing = true;
                        capturedTd.detailPrototypes = arr;
                        EditorUtility.SetDirty(capturedTd);
                    });
                }

                if (!t.drawTreesAndFoliage)
                    Add(issues, Level.Info, name + " ซ่อนต้นไม้อยู่ (ตอนเซฟระบบจะเปิดคืนในไฟล์ให้เอง)", t, "เปิดคืน", () => { t.drawTreesAndFoliage = true; SceneView.RepaintAll(); });

                if (!t.drawInstanced)
                    Add(issues, Level.Warning, name + " ไม่ได้ติ๊ก Draw Instanced", t, "ติ๊กให้", () => { Undo.RecordObject(t, "Draw Instanced"); t.drawInstanced = true; });

                foreach (TreePrototype tp in td.treePrototypes)
                {
                    if (tp.prefab == null)
                        Add(issues, Level.Error, name + " มีช่องต้นไม้ที่ไม่ได้ใส่ prefab", t);
                    else if (tp.prefab.GetComponentInChildren<Collider>(true) == null)
                        Add(issues, Level.Warning, "ต้นไม้ “" + tp.prefab.name + "” ไม่มี Collider ผู้เล่นจะเดินทะลุ", tp.prefab);
                }

                var col = t.GetComponent<TerrainCollider>();
                if (col == null)
                    Add(issues, Level.Error, name + " ไม่มี Terrain Collider ผู้เล่นจะตกพื้น", t);
                else if (td.treeInstanceCount > 0 && TreeCollidersOff(col))
                    Add(issues, Level.Warning, name + " ไม่ได้ติ๊ก Enable Tree Colliders", col, "ติ๊กให้", () => SetTreeColliders(col, true));

                int floating = GroundSnap.CountFloating(t, 0.05f);
                if (floating > 0)
                    Add(issues, Level.Warning, name + " มีต้นไม้ลอย " + floating + " ต้น", t, "ทำให้ติดพื้น",
                        () => GroundSnap.FixTerrainTrees(new[] { t }, 0.05f, true, 0.4f));

                if (t.detailObjectDistance > 80f)
                    Add(issues, Level.Info, name + " ระยะหญ้า " + t.detailObjectDistance.ToString("0") + " m ไกลกว่าที่แนะนำ (40–60)", t, "ตั้งเป็น 60",
                        () => { Undo.RecordObject(t, "ระยะหญ้า"); t.detailObjectDistance = 60f; });

                if (t.heightmapPixelError < 5f)
                    Add(issues, Level.Info, name + " Pixel Error " + t.heightmapPixelError.ToString("0.#") + " ต่ำกว่าที่แนะนำ (5–10)", t, "ตั้งเป็น 5",
                        () => { Undo.RecordObject(t, "Pixel Error"); t.heightmapPixelError = 5f; });
            }

            int dummies = ScaleDummy.Count();
            if (dummies > 0)
                Add(issues, Level.Info, "มีหุ่นเทียบสเกล " + dummies + " ตัว (ไม่ติดไปในเกม ลบได้ถ้าไม่ใช้แล้ว)", null, "ลบทั้งหมด", () => ScaleDummy.DeleteAll());

            if (Terrain.activeTerrains.Length > 0 && TTTerrain.FindRoot(WallDrawer.RootName) == null)
                Add(issues, Level.Info, "ฉากนี้ยังไม่มีกำแพงล่องหน ผู้เล่นอาจเดินหลุดแมพ", null);

            if (Terrain.activeTerrains.Length == 0)
                Add(issues, Level.Info, "ฉากนี้ไม่มี Terrain", null);

            issues.Sort((a, b) => a.level.CompareTo(b.level));
            return issues;
        }

        // ช่อง Enable Tree Colliders ไม่มีให้เรียกจากโค้ดตรงๆ เลยอ่านผ่าน SerializedObject
        static SerializedProperty FindTreeColliderProperty(SerializedObject so)
        {
            SerializedProperty it = so.GetIterator();
            bool enterChildren = true;
            while (it.Next(enterChildren))
            {
                enterChildren = false;
                if (it.propertyType == SerializedPropertyType.Boolean &&
                    it.name.IndexOf("TreeCollider", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return it.Copy();
            }
            return null;
        }

        static bool TreeCollidersOff(TerrainCollider col)
        {
            SerializedProperty prop = FindTreeColliderProperty(new SerializedObject(col));
            return prop != null && !prop.boolValue;
        }

        static void SetTreeColliders(TerrainCollider col, bool on)
        {
            var so = new SerializedObject(col);
            SerializedProperty prop = FindTreeColliderProperty(so);
            if (prop == null) return;
            prop.boolValue = on;
            so.ApplyModifiedProperties();
        }

        static void Add(List<Issue> list, Level level, string text, Object target, string fixLabel = null, System.Action fix = null)
        {
            list.Add(new Issue { level = level, text = text, target = target, fixLabel = fixLabel, fix = fix });
        }

        public static GUIContent Icon(Level level)
        {
            switch (level)
            {
                case Level.Error: return EditorGUIUtility.IconContent("console.erroricon.sml");
                case Level.Warning: return EditorGUIUtility.IconContent("console.warnicon.sml");
                default: return EditorGUIUtility.IconContent("console.infoicon.sml");
            }
        }
    }
}