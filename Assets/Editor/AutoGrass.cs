using UnityEditor;
using UnityEngine;

namespace Yantra.TerrainTools
{
    internal static class AutoGrass
    {
        public struct Settings
        {
            public DetailPrototype prototype;
            public bool[] layers;
            public float minWeight;
            public float maxSlope;
            public float strength;
            public float noiseScale;
            public float noiseAmount;
            public bool replace;
        }

        public static int Run(Terrain[] terrains, Settings s)
        {
            int done = 0;
            try
            {
                for (int ti = 0; ti < terrains.Length; ti++)
                {
                    Terrain terrain = terrains[ti];
                    TerrainData td = terrain.terrainData;
                    if (td == null) continue;

                    int proto = FindPrototype(td, s.prototype);
                    if (proto < 0) continue;

                    Undo.RegisterCompleteObjectUndo(td, "ลงหญ้าอัตโนมัติ");

                    int dw = td.detailWidth, dh = td.detailHeight;
                    int aw = td.alphamapWidth, ah = td.alphamapHeight, al = td.alphamapLayers;
                    float[,,] alpha = td.GetAlphamaps(0, 0, aw, ah);
                    int[,] map = s.replace ? new int[dh, dw] : td.GetDetailLayer(0, 0, dw, dh, proto);

                    int maxValue = td.detailScatterMode == DetailScatterMode.CoverageMode ? 255 : td.maxDetailScatterPerRes;
                    Vector3 size = td.size;
                    Vector3 origin = terrain.GetPosition();

                    for (int y = 0; y < dh; y++)
                    {
                        if (y % 64 == 0)
                            EditorUtility.DisplayProgressBar("ลงหญ้าอัตโนมัติ", terrain.name, (ti + (float)y / dh) / terrains.Length);

                        float v = (y + 0.5f) / dh;
                        int ay = Mathf.Min(ah - 1, (int)(v * ah));

                        for (int x = 0; x < dw; x++)
                        {
                            float u = (x + 0.5f) / dw;
                            int ax = Mathf.Min(aw - 1, (int)(u * aw));

                            float weight = 0f;
                            for (int l = 0; l < al && l < s.layers.Length; l++)
                                if (s.layers[l]) weight += alpha[ay, ax, l];

                            int value = 0;
                            if (weight >= s.minWeight && td.GetSteepness(u, v) <= s.maxSlope)
                            {
                                float noise = 1f;
                                if (s.noiseAmount > 0f)
                                {
                                    float n = Mathf.PerlinNoise((origin.x + u * size.x) * s.noiseScale, (origin.z + v * size.z) * s.noiseScale);
                                    noise = Mathf.Lerp(1f, n, s.noiseAmount);
                                }
                                value = Mathf.RoundToInt(maxValue * s.strength * Mathf.Clamp01(weight) * noise);
                            }

                            map[y, x] = s.replace ? value : Mathf.Max(map[y, x], value);
                        }
                    }

                    td.SetDetailLayer(0, 0, proto, map);
                    EditorUtility.SetDirty(td);
                    done++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            return done;
        }

        public static int Clear(Terrain[] terrains, DetailPrototype prototype)
        {
            int done = 0;
            foreach (Terrain terrain in terrains)
            {
                TerrainData td = terrain.terrainData;
                if (td == null) continue;
                int proto = FindPrototype(td, prototype);
                if (proto < 0) continue;

                Undo.RegisterCompleteObjectUndo(td, "ล้างหญ้า");
                td.SetDetailLayer(0, 0, proto, new int[td.detailHeight, td.detailWidth]);
                EditorUtility.SetDirty(td);
                done++;
            }
            return done;
        }

        static int FindPrototype(TerrainData td, DetailPrototype target)
        {
            DetailPrototype[] protos = td.detailPrototypes;
            for (int i = 0; i < protos.Length; i++)
                if (TTTerrain.SamePrototype(protos[i], target)) return i;
            return -1;
        }
    }
}
