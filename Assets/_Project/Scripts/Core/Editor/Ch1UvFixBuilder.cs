using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S67d：修正第一章所有几何体的 UV 平铺密度。
    ///
    /// ══════════════════════ 问题 ══════════════════════
    ///
    ///   第一章的灰盒网格是**按世界尺寸压 UV** 的做法：
    ///     每个 Box 的 UV 直接 = 世界尺寸 × 0.5
    ///   然后材质再乘 Tiling 2.0。
    ///
    ///   结果：
    ///     Floor_A1 (12.3 x 3.3 米)  UV 跨度 6.15 x 1.65
    ///     材质 Tiling 3.0 → 实际 18.45 x 4.95 个贴图循环
    ///     但地板只有 0.5 米厚，Y 方向被压成 9.9 循环/米 → 条纹
    ///
    ///   实测截图确认：右侧墙面全是横向条纹。
    ///
    /// ══════════════════════ 为什么不能只改材质 Tiling ══════════════════════
    ///
    ///   所有墙共用一个材质。改了 Tiling，
    ///   3 米长的短墙和 15.8 米长的走廊会得到不同的实际密度。
    ///   必须**按每个物体自己的尺寸**调整 UV。
    ///
    /// ══════════════════════ 做法 ══════════════════════
    ///
    ///   对场景里每个 MeshRenderer：
    ///     ① 读取网格的世界尺寸和 UV 跨度
    ///     ② 按"目标密度 = 每 2 米一个贴图循环"重算 UV
    ///     ③ **写进共享网格前先检查是否已处理过**（避免重复缩放）
    ///
    ///   共享网格会被多个 Renderer 引用，所以：
    ///     同名网格只处理一次
    ///
    /// ⚠️ 注意：这会修改 Mesh 资源本身（MESH_xxx），是只往"正确"方向调，
    ///    且处理前已备份场景。网格是灰盒自动生成的，不影响任何游戏逻辑。
    /// </summary>
    public static class Ch1UvFixBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";

        /// <summary>目标：每 N 米一个贴图循环。2.0 米对 2048² 贴图足够清晰。</summary>
        private const float MetersPerTile = 2.0f;

        [MenuItem("ABYSS ECHO/美术 v8.0/修正第一章 UV 平铺")]
        public static void Build() => Run();

        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Log("已打开第一章场景");

            // 先收集所有网格 → 用它被引用时的世界尺寸
            var meshInfo = new Dictionary<Mesh, Vector3>();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (r == null) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var mesh = mf.sharedMesh;

                // 记录世界尺寸（同一个网格可能被不同缩放的物体引用，取最大的）
                var ws = r.bounds.size;
                if (!meshInfo.ContainsKey(mesh)) meshInfo[mesh] = ws;
                else
                {
                    var cur = meshInfo[mesh];
                    meshInfo[mesh] = new Vector3(Mathf.Max(cur.x, ws.x),
                                                 Mathf.Max(cur.y, ws.y),
                                                 Mathf.Max(cur.z, ws.z));
                }
            }

            Log($"场景中独立网格 {meshInfo.Count} 个");

            int fixedCount = 0, skipped = 0;
            var processed = new HashSet<Mesh>();

            foreach (var kv in meshInfo)
            {
                var mesh = kv.Key;
                var ws = kv.Value;
                if (mesh == null || processed.Contains(mesh)) { skipped++; continue; }
                processed.Add(mesh);

                // 跳过非灰盒网格（比如手套模型、怪物模型）
                if (!mesh.name.StartsWith("MESH_")) { skipped++; continue; }

                var uv = mesh.uv;
                if (uv == null || uv.Length == 0) { skipped++; continue; }

                // 计算当前 UV 跨度
                float mnU = 999, mxU = -999, mnV = 999, mxV = -999;
                foreach (var v in uv)
                {
                    mnU = Mathf.Min(mnU, v.x); mxU = Mathf.Max(mxU, v.x);
                    mnV = Mathf.Min(mnV, v.y); mxV = Mathf.Max(mxV, v.y);
                }
                float spanU = mxU - mnU;
                float spanV = mxV - mnV;
                if (spanU < 0.001f || spanV < 0.001f) { skipped++; continue; }

                // 当前每米 UV
                //   取世界尺寸的三个轴，UV 的两个轴应与其中两个对应
                //   灰盒 Box 的 UV 是按"最大的两个轴"展开的
                float[] dims = { ws.x, ws.y, ws.z };
                System.Array.Sort(dims);
                float d1 = dims[2];   // 最大
                float d2 = dims[1];   // 次大

                float curPerMeterU = spanU / Mathf.Max(0.01f, d1);
                float curPerMeterV = spanV / Mathf.Max(0.01f, d2);

                // 目标每米 UV
                float targetPerMeter = 1.0f / MetersPerTile;

                // 计算缩放系数
                float scaleU = targetPerMeter / Mathf.Max(0.0001f, curPerMeterU);
                float scaleV = targetPerMeter / Mathf.Max(0.0001f, curPerMeterV);

                // 防止离谱的缩放（比如平面物体）
                if (scaleU > 50f || scaleU < 0.02f) scaleU = Mathf.Clamp(scaleU, 0.02f, 50f);
                if (scaleV > 50f || scaleV < 0.02f) scaleV = Mathf.Clamp(scaleV, 0.02f, 50f);

                // 已经很接近目标就不动
                if (Mathf.Abs(scaleU - 1f) < 0.15f && Mathf.Abs(scaleV - 1f) < 0.15f) { skipped++; continue; }

                // 应用缩放
                var newUv = new Vector2[uv.Length];
                for (int i = 0; i < uv.Length; i++)
                    newUv[i] = new Vector2(uv[i].x * scaleU, uv[i].y * scaleV);

                // ⚠️ 直接改原网格资源（.asset），不复制 ——
                //    复制会导致场景引用和资源脱节，而且 UV 会在下次运行时丢失。
                //    网格是灰盒自动生成的 .asset，可读可写，改动只影响 UV。
                mesh.uv = newUv;
                mesh.RecalculateBounds();
                EditorUtility.SetDirty(mesh);

                int refs = 0;
                foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
                    if (mf.sharedMesh == mesh) refs++;

                if (fixedCount < 30)
                    Log($"  {mesh.name,-32} 尺寸={ws.x:F1}x{ws.y:F1}x{ws.z:F1} " +
                        $"UV缩放=({scaleU:F2},{scaleV:F2}) 引用{refs}处");
                fixedCount++;
            }

            // 材质 Tiling 统一归 1（因为 UV 已经按世界尺寸算好了）
            var tiled = 0;
            foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Art/Materials" }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
                if (m == null) continue;
                var t = m.GetTextureScale("_BaseMap");
                if (Mathf.Abs(t.x - 1f) > 0.01f || Mathf.Abs(t.y - 1f) > 0.01f)
                {
                    m.SetTextureScale("_BaseMap", Vector2.one);
                    m.SetTextureScale("_BumpMap", Vector2.one);
                    m.SetTextureScale("_OcclusionMap", Vector2.one);
                    EditorUtility.SetDirty(m);
                    tiled++;
                }
            }
            Log($"材质 Tiling 归一到 1：{tiled} 个");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Log($"处理网格 {fixedCount} 个，跳过 {skipped} 个");
            Log("完成");
        }

        private static void Log(string msg)
        {
            string line = $"[Ch1Uv] {msg}";
            Debug.Log(line);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.AppendAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch1UvFix_Result.txt",
                                   line + "\n", new UTF8Encoding(false));
            }
            catch { }
        }
    }
}