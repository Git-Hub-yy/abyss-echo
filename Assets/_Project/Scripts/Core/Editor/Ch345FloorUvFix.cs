using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S70e：验证并修复**真实的**地板/天花板 UV 问题。
    ///
    /// ══════════════════════ 真实问题（6 个网格）══════════════════════
    ///
    ///   第三章：
    ///     MESH_Floor_F2            4.8 × 0.5 × 16.3   UV = 8.15 × 8.15
    ///     MESH_Ceiling_F2          4.8 × 0.3 × 16.3   UV = 8.15 × 8.15
    ///     MESH_Floor_F15_F7_Link   2.9 × 0.5 × 7.3    UV = 3.65 × 3.65
    ///     MESH_Ceiling_F15_F7_Link 2.9 × 0.3 × 7.3    UV = 3.65 × 3.65
    ///
    ///   第五章：
    ///     MESH_Floor_H2            5.3 × 0.5 × 14.3   UV = 7.15 × 7.15
    ///     MESH_Ceiling_H2          5.3 × 0.3 × 14.3   UV = 7.15 × 7.15
    ///
    /// ══════════════════════ 错误本质 ══════════════════════
    ///
    ///   正确做法：水平面（地板/天花板）应该
    ///       U = X尺寸 × 0.5,  V = Z尺寸 × 0.5
    ///   即 4.8×16.3 → UV = 2.40 × 8.15
    ///
    ///   实际生成器用了：
    ///       U = max(X,Z) × 0.5 = 8.15
    ///       V = max(X,Z) × 0.5 = 8.15
    ///   两个方向都用了最长的那个轴 → **V 方向被拉伸 3.4 倍**
    ///
    ///   例：Floor_F2 是 16.3 米长、4.8 米宽
    ///       正确 UV: U=2.40（4.8米方向）, V=8.15（16.3米方向）
    ///       实际 UV: U=8.15, V=8.15  → 宽度方向被压扁 3.4 倍
    ///
    /// ══════════════════════ 修复方式 ══════════════════════
    ///
    ///   只改这 6 个网格的 **Y 面（顶/底面）** 的 UV，
    ///   按顶点局部坐标重新展开：
    ///       U = (局部x + 0.5) × X尺寸 × 0.5
    ///       V = (局部z + 0.5) × Z尺寸 × 0.5
    ///
    ///   不改顶点、法线、三角形、碰撞体、材质。
    /// ══════════════════════ 只改 Y 面 ══════════════════════
    ///
    ///   X/Z 侧面（墙的厚度面）保持不变 —— 它们本来就是对的。
    /// </summary>
    public static class Ch345FloorUvFix
    {
        private const float UvPerMeter = 0.5f;

        // 目标网格 → 场景
        private static readonly Dictionary<string, string> Targets = new Dictionary<string, string>
        {
            { "MESH_Floor_F2",            "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity" },
            { "MESH_Ceiling_F2",          "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity" },
            { "MESH_Floor_F15_F7_Link",   "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity" },
            { "MESH_Ceiling_F15_F7_Link", "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity" },
            { "MESH_Floor_H2",            "Assets/_Project/Scenes/Level_Chapter5_Graybox.unity" },
            { "MESH_Ceiling_H2",          "Assets/_Project/Scenes/Level_Chapter5_Graybox.unity" },
        };

        [MenuItem("ABYSS ECHO/美术 v9.3/修复地板天花板 UV")]
        public static void Build() => Run(dryRun: false);

        [MenuItem("ABYSS ECHO/美术 v9.3/预演地板天花板 UV 修复（只读）")]
        public static void DryRun() => Run(dryRun: true);

        public static void Run(bool dryRun)
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════");
            sb.AppendLine(dryRun ? "  地板/天花板 UV 修复 —— 预演（只读）"
                                 : "  地板/天花板 UV 修复 —— 执行");
            sb.AppendLine("═══════════════════════════════════════════════");

            var byScene = new Dictionary<string, List<string>>();
            foreach (var kv in Targets)
            {
                if (!byScene.ContainsKey(kv.Value)) byScene[kv.Value] = new List<string>();
                byScene[kv.Value].Add(kv.Key);
            }

            int fixedCount = 0, failedCount = 0;

            foreach (var sceneKv in byScene)
            {
                string scenePath = sceneKv.Key;
                if (!File.Exists(scenePath)) { sb.AppendLine($"❌ 场景不存在 {scenePath}"); continue; }

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                bool sceneChanged = false;
                sb.AppendLine();
                sb.AppendLine($"══════ {Path.GetFileNameWithoutExtension(scenePath)} ══════");

                foreach (var meshName in sceneKv.Value)
                {
                    Mesh mesh = null;
                    MeshRenderer owner = null;
                    foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                    {
                        if (r == null) continue;
                        var mf = r.GetComponent<MeshFilter>();
                        if (mf != null && mf.sharedMesh != null && mf.sharedMesh.name == meshName)
                        {
                            mesh = mf.sharedMesh;
                            owner = r;
                            break;
                        }
                    }

                    if (mesh == null) { sb.AppendLine($"  ❌ 找不到 {meshName}"); failedCount++; continue; }

                    var ws = owner.bounds.size;
                    var uv = mesh.uv;
                    var verts = mesh.vertices;
                    var norms = mesh.normals;

                    if (uv == null || verts == null || norms == null)
                    { sb.AppendLine($"  ❌ {meshName} 缺少 UV/顶点/法线"); failedCount++; continue; }

                    // 统计 Y 面顶点
                    var yIdx = new List<int>();
                    for (int i = 0; i < norms.Length; i++)
                        if (Mathf.Abs(norms[i].y) > 0.5f) yIdx.Add(i);

                    if (yIdx.Count == 0) { sb.AppendLine($"  ❌ {meshName} 无 Y 面顶点"); failedCount++; continue; }

                    // 修复前 UV 跨度
                    float mnU0 = 999, mxU0 = -999, mnV0 = 999, mxV0 = -999;
                    foreach (var i in yIdx)
                    {
                        mnU0 = Mathf.Min(mnU0, uv[i].x); mxU0 = Mathf.Max(mxU0, uv[i].x);
                        mnV0 = Mathf.Min(mnV0, uv[i].y); mxV0 = Mathf.Max(mxV0, uv[i].y);
                    }

                    // 计算局部 X/Z 范围
                    float mnX = 999, mxX = -999, mnZ = 999, mxZ = -999;
                    foreach (var i in yIdx)
                    {
                        mnX = Mathf.Min(mnX, verts[i].x); mxX = Mathf.Max(mxX, verts[i].x);
                        mnZ = Mathf.Min(mnZ, verts[i].z); mxZ = Mathf.Max(mxZ, verts[i].z);
                    }
                    float sx = Mathf.Max(0.001f, mxX - mnX);
                    float sz = Mathf.Max(0.001f, mxZ - mnZ);

                    // 期望：U 用世界 X 尺寸，V 用世界 Z 尺寸
                    float expU = ws.x * UvPerMeter;
                    float expV = ws.z * UvPerMeter;

                    sb.AppendLine($"  {meshName}");
                    sb.AppendLine($"    尺寸 {ws.x:F2} × {ws.y:F2} × {ws.z:F2}   Y面顶点 {yIdx.Count} 个");
                    sb.AppendLine($"    修复前 Y面 UV: {(mxU0 - mnU0):F3} × {(mxV0 - mnV0):F3}");
                    sb.AppendLine($"    目标    Y面 UV: {expU:F3} × {expV:F3}");

                    if (dryRun)
                    {
                        sb.AppendLine($"    [预演] 将按局部坐标重展开 Y 面");
                        continue;
                    }

                    // 重展开 Y 面 UV
                    var newUv = (Vector2[])uv.Clone();
                    foreach (var i in yIdx)
                    {
                        float nx = (verts[i].x - mnX) / sx;   // 0..1
                        float nz = (verts[i].z - mnZ) / sz;   // 0..1
                        newUv[i] = new Vector2(nx * expU, nz * expV);
                    }

                    mesh.uv = newUv;
                    mesh.RecalculateBounds();
                    EditorUtility.SetDirty(mesh);
                    sceneChanged = true;
                    fixedCount++;

                    // 修复后验证
                    float mnU1 = 999, mxU1 = -999, mnV1 = 999, mxV1 = -999;
                    var checkUv = mesh.uv;
                    foreach (var i in yIdx)
                    {
                        mnU1 = Mathf.Min(mnU1, checkUv[i].x); mxU1 = Mathf.Max(mxU1, checkUv[i].x);
                        mnV1 = Mathf.Min(mnV1, checkUv[i].y); mxV1 = Mathf.Max(mxV1, checkUv[i].y);
                    }
                    sb.AppendLine($"    修复后 Y面 UV: {(mxU1 - mnU1):F3} × {(mxV1 - mnV1):F3}  {(Mathf.Abs((mxU1-mnU1)-expU)<0.01f && Mathf.Abs((mxV1-mnV1)-expV)<0.01f ? "✅" : "❌")}");
                }

                if (sceneChanged)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene, scenePath);
                    sb.AppendLine($"  场景已保存: {Path.GetFileName(scenePath)}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════════════");
            sb.AppendLine($"  修复 {fixedCount} 个，失败 {failedCount} 个");
            sb.AppendLine("═══════════════════════════════════════════════");

            if (!dryRun)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch345FloorUvFix.txt",
                              text, new UTF8Encoding(false));
        }
    }
}