using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S67e：修正第一章灰盒网格的 UV 展开错误。
    ///
    /// ══════════════════════ 发现的 bug ══════════════════════
    ///
    ///   灰盒网格生成器对"薄板墙"的 UV 展开算错了：
    ///
    ///     West_B3_Full  尺寸 0.3(厚) x 3.2(高) x 8.0(长)
    ///
    ///     玩家看到的大面（法线 ±X 的面）UV 跨度 = 0.15 x 1.60
    ///       0.15 = 0.3(厚度) x 0.5   ← ❌ 用了厚度！
    ///       1.60 = 3.2(高度) x 0.5   ← 对的
    ///
    ///     正确的应该是：
    ///       U = 8.0(长度) x 0.5 = 4.00
    ///       V = 3.2(高度) x 0.5 = 1.60
    ///
    ///   结果：8 米长的墙面上只有 0.15 个贴图循环，
    ///         整张贴图被横向拉长到 8 米 → 屏幕上就是横向条纹。
    ///
    /// ══════════════════════ 修复策略 ══════════════════════
    ///
    ///   不整体重算 UV（上一轮尝试证明这会破坏其他面），
    ///   而是**只修正"轴向上的错误轴"**：
    ///
    ///     对每个网格，按法线分组看 UV：
    ///       · X 方向的侧面（法线 ±X）应该用 (Z长度, Y高度)
    ///       · Z 方向的侧面（法线 ±Z）应该用 (X长度, Y高度)
    ///       · 顶底面（法线 ±Y）应该用 (X长度, Z长度)
    ///
    ///   检测每组 UV 的跨度是否明显小于应有的值，
    ///   如果小于 50%，判定为展开错误，重新展开该组。
    ///
    /// ══════════════════════ 安全性 ══════════════════════
    ///
    ///   · 只改 UV，不动顶点、法线、三角形
    ///   · 只处理 MESH_ 开头的灰盒网格
    ///   · 处理前已备份 GeneratedMeshes（1055 个）
    ///   · 网格是自动生成的几何体，UV 不参与任何游戏逻辑
    /// </summary>
    public static class Ch1UvRebuildBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";

        /// <summary>UV = 世界尺寸 x 该系数。灰盒原本就是这个约定（0.5 = 每 2 米一个循环）。</summary>
        private const float UvPerMeter = 0.5f;

        [MenuItem("ABYSS ECHO/美术 v8.0/重建第一章 UV 展开")]
        public static void Build() => Run();

        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Log("已打开第一章场景");

            // 收集网格 → 世界尺寸
            var meshSize = new Dictionary<Mesh, Vector3>();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (r == null) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var mesh = mf.sharedMesh;
                if (!mesh.name.StartsWith("MESH_")) continue;

                var ws = r.bounds.size;
                if (!meshSize.ContainsKey(mesh)) meshSize[mesh] = ws;
                else
                {
                    var cur = meshSize[mesh];
                    meshSize[mesh] = new Vector3(Mathf.Max(cur.x, ws.x),
                                                 Mathf.Max(cur.y, ws.y),
                                                 Mathf.Max(cur.z, ws.z));
                }
            }
            Log($"灰盒网格 {meshSize.Count} 个");

            int fixedMeshes = 0, fixedFaces = 0;

            foreach (var kv in meshSize)
            {
                var mesh = kv.Key;
                var ws = kv.Value;
                var uv = mesh.uv;
                var norms = mesh.normals;
                if (uv == null || uv.Length == 0 || norms == null) continue;

                // 期望的 UV 跨度（每个朝向）
                //   ±X 面 → (Z, Y)
                //   ±Z 面 → (X, Y)
                //   ±Y 面 → (X, Z)
                float expUX = ws.z * UvPerMeter;   // X 侧面的 U
                float expVX = ws.y * UvPerMeter;   // X 侧面的 V
                float expUZ = ws.x * UvPerMeter;   // Z 侧面的 U
                float expVZ = ws.y * UvPerMeter;   // Z 侧面的 V
                float expUY = ws.x * UvPerMeter;   // Y 面的 U
                float expVY = ws.z * UvPerMeter;   // Y 面的 V

                // 按朝向收集顶点索引
                var groups = new Dictionary<string, List<int>>();
                for (int i = 0; i < norms.Length; i++)
                {
                    string k = AxisKey(norms[i]);
                    if (!groups.ContainsKey(k)) groups[k] = new List<int>();
                    groups[k].Add(i);
                }

                bool changed = false;
                var newUv = (Vector2[])uv.Clone();

                foreach (var g in groups)
                {
                    var idx = g.Value;
                    if (idx.Count == 0) continue;

                    // 当前跨度
                    float mnU = 999, mxU = -999, mnV = 999, mxV = -999;
                    foreach (var i in idx)
                    {
                        mnU = Mathf.Min(mnU, uv[i].x); mxU = Mathf.Max(mxU, uv[i].x);
                        mnV = Mathf.Min(mnV, uv[i].y); mxV = Mathf.Max(mxV, uv[i].y);
                    }
                    float curU = mxU - mnU, curV = mxV - mnV;

                    float expU, expV;
                    switch (g.Key)
                    {
                        case "X": expU = expUX; expV = expVX; break;
                        case "Z": expU = expUZ; expV = expVZ; break;
                        case "Y": expU = expUY; expV = expVY; break;
                        default: continue;
                    }

                    if (expU < 0.01f || expV < 0.01f) continue;

                    // 判断是否错误：当前跨度明显偏离期望（<50% 或 >200%）
                    bool badU = curU > 0.001f && (curU < expU * 0.5f || curU > expU * 2.0f);
                    bool badV = curV > 0.001f && (curV < expV * 0.5f || curV > expV * 2.0f);

                    // 跨度接近 0 的面（顶/底面常这样）也修正
                    if (curU < 0.001f || curV < 0.001f) { badU = true; badV = true; }

                    if (!badU && !badV) continue;

                    // 该面 UV 是否全部相同（压成一点）→ 需要按位置重算
                    float scU = badU && curU > 0.001f ? expU / curU : 1f;
                    float scV = badV && curV > 0.001f ? expV / curV : 1f;

                    if (curU < 0.001f || curV < 0.001f)
                    {
                        // UV 被压成一条线 → 按顶点位置直接算，而不是缩放
                        //   用该面的两个"展开轴"从局部坐标取
                        RebuildFaceUv(mesh, newUv, idx, g.Key, ws, expU / UvPerMeter, expV / UvPerMeter);
                    }
                    else
                    {
                        for (int j = 0; j < idx.Count; j++)
                        {
                            var i = idx[j];
                            newUv[i] = new Vector2(uv[i].x * scU, uv[i].y * scV);
                        }
                    }
                    fixedFaces++;
                    changed = true;
                }

                if (changed)
                {
                    mesh.uv = newUv;
                    mesh.RecalculateBounds();
                    EditorUtility.SetDirty(mesh);
                    fixedMeshes++;
                    if (fixedMeshes <= 15)
                        Log($"  {mesh.name,-34} 尺寸={ws.x:F1}x{ws.y:F1}x{ws.z:F1}  修正面组");
                }
            }

            Log($"修正网格 {fixedMeshes} 个，面组 {fixedFaces} 处");

            EditorSceneManager.MarkSceneDirty(scene);
            bool ok = EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log(ok ? "场景已保存" : "❌ 保存失败");
        }

        /// <summary>UV 被压成线时，按局部顶点位置重新展开该面。</summary>
        private static void RebuildFaceUv(Mesh mesh, Vector2[] dst, List<int> idx,
                                           string axis, Vector3 ws, float len1, float len2)
        {
            var verts = mesh.vertices;
            // 计算该组顶点的包围盒
            var mn = new Vector3(999, 999, 999);
            var mx = new Vector3(-999, -999, -999);
            foreach (var i in idx)
            {
                var v = verts[i];
                mn = Vector3.Min(mn, v); mx = Vector3.Max(mx, v);
            }
            float sx = Mathf.Max(0.001f, mx.x - mn.x);
            float sy = Mathf.Max(0.001f, mx.y - mn.y);
            float sz = Mathf.Max(0.001f, mx.z - mn.z);

            for (int j = 0; j < idx.Count; j++)
            {
                var i = idx[j];
                var v = verts[i];
                float u, w;
                switch (axis)
                {
                    case "X": u = (v.z - mn.z) / sz; w = (v.y - mn.y) / sy; break;  // 用 Z、Y
                    case "Z": u = (v.x - mn.x) / sx; w = (v.y - mn.y) / sy; break;  // 用 X、Y
                    default: u = (v.x - mn.x) / sx; w = (v.z - mn.z) / sz; break;   // 用 X、Z
                }
                dst[i] = new Vector2(u * len1 * UvPerMeter, w * len2 * UvPerMeter);
            }
        }

        private static string AxisKey(Vector3 n)
        {
            if (Mathf.Abs(n.x) > 0.5f) return "X";
            if (Mathf.Abs(n.y) > 0.5f) return "Y";
            return "Z";
        }

        private static void Log(string msg)
        {
            string line = $"[Ch1Uv2] {msg}";
            Debug.Log(line);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.AppendAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch1UvRebuild_Result.txt",
                                   line + "\n", new UTF8Encoding(false));
            }
            catch { }
        }
    }
}