using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S68：第二章材质现状审计（只读，不改任何东西）。
    ///
    /// 任务要求先检查：
    ///   · 所有 Renderer
    ///   · 当前使用的 Material
    ///   · 哪些是纯色
    ///   · 哪些是 Unity 默认材质
    ///   · 是否存在 UV 拉伸（只记录，不修改）
    /// </summary>
    public static class Ch2MaterialAudit
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("══════════════════════════════════════════");
            sb.AppendLine("  第二章材质现状审计（只读）");
            sb.AppendLine("══════════════════════════════════════════");

            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter2_Graybox.unity",
                                         OpenSceneMode.Single);
            Physics.SyncTransforms();

            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            sb.AppendLine($"场景中 Renderer 总数 = {renderers.Length}");

            var stat = new Dictionary<string, int>();
            var nullSlots = new List<string>();
            var defaultMats = new List<string>();
            var texturless = new Dictionary<string, string>();

            foreach (var r in renderers)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats == null || mats.Length == 0) { nullSlots.Add(r.gameObject.name); continue; }

                foreach (var m in mats)
                {
                    if (m == null) { nullSlots.Add(r.gameObject.name); continue; }
                    if (!stat.ContainsKey(m.name)) stat[m.name] = 0;
                    stat[m.name]++;

                    // Unity 默认材质
                    if (m.name == "Default-Material" || m.name == "Default-Diffuse" ||
                        (m.shader != null && m.shader.name == "Standard" && m.name.StartsWith("Default")))
                        defaultMats.Add($"{r.gameObject.name}:{m.name}");

                    // 记录贴图状态
                    if (!texturless.ContainsKey(m.name))
                    {
                        bool tex = m.GetTexture("_BaseMap") != null;
                        bool emit = m.IsKeywordEnabled("_EMISSION");
                        string sp = AssetDatabase.GetAssetPath(m);
                        texturless[m.name] = $"贴图={(tex ? "✓" : "✗")} 发光={(emit ? "✓" : "-")} 路径={sp}";
                    }
                }
            }

            sb.AppendLine($"空材质槽 = {nullSlots.Count}");
            sb.AppendLine($"Unity 默认材质 = {defaultMats.Count}");
            sb.AppendLine();

            sb.AppendLine("══════ 材质使用统计 ══════");
            sb.AppendLine($"{"材质名",-28} {"次数",-8} 状态");
            sb.AppendLine(new string('-', 90));
            var keys = new List<string>(stat.Keys);
            keys.Sort((a, b) => stat[b].CompareTo(stat[a]));
            foreach (var k in keys)
                sb.AppendLine($"{k,-28} {stat[k],-8} {texturless[k]}");

            sb.AppendLine();
            int withTex = 0, without = 0;
            foreach (var v in texturless.Values) { if (v.Contains("贴图=✓")) withTex++; else without++; }
            sb.AppendLine($"有贴图的材质 = {withTex}");
            sb.AppendLine($"纯色材质 = {without}");

            // 逐 Renderer 清单（按对象名分组）
            sb.AppendLine();
            sb.AppendLine("══════ 逐 Renderer 材质分配 ══════");
            var list = new List<string>();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var names = new List<string>();
                if (r.sharedMaterials != null)
                    foreach (var m in r.sharedMaterials) names.Add(m != null ? m.name : "NULL");

                var ws = r.bounds.size;
                var mf = r.GetComponent<MeshFilter>();
                string meshName = mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "-";

                list.Add($"  {r.gameObject.name,-34} {string.Join(",", names),-24} " +
                         $"{ws.x:F1}x{ws.y:F1}x{ws.z:F1}  mesh={meshName}");
            }
            list.Sort();
            foreach (var l in list) sb.AppendLine(l);

            // UV 检查（只记录不修改）
            sb.AppendLine();
            sb.AppendLine("══════ UV 拉伸检查（只记录，不修改）══════");
            sb.AppendLine("  判定标准：UV跨度/米 与 0.5（灰盒约定）偏离超过 2 倍视为异常");
            sb.AppendLine();
            var uvIssues = new List<string>();
            var checkedMeshes = new HashSet<Mesh>();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var mesh = mf.sharedMesh;
                if (!mesh.name.StartsWith("MESH_")) continue;
                if (!checkedMeshes.Add(mesh)) continue;
                var uv = mesh.uv;
                if (uv == null || uv.Length == 0) continue;

                var ws = r.bounds.size;
                float mnU = 999, mxU = -999, mnV = 999, mxV = -999;
                foreach (var v in uv)
                {
                    mnU = Mathf.Min(mnU, v.x); mxU = Mathf.Max(mxU, v.x);
                    mnV = Mathf.Min(mnV, v.y); mxV = Mathf.Max(mxV, v.y);
                }
                float spanU = mxU - mnU, spanV = mxV - mnV;

                float[] dims = { ws.x, ws.y, ws.z };
                System.Array.Sort(dims);
                float d1 = Mathf.Max(0.01f, dims[2]);
                float d2 = Mathf.Max(0.01f, dims[1]);

                float uPerM = spanU / d1;
                float vPerM = spanV / d2;

                bool bad = (uPerM > 0.001f && (uPerM < 0.25f || uPerM > 1.0f)) ||
                           (vPerM > 0.001f && (vPerM < 0.25f || vPerM > 1.0f));
                if (bad)
                    uvIssues.Add($"  {mesh.name,-36} 尺寸={ws.x:F1}x{ws.y:F1}x{ws.z:F1} " +
                                 $"UV跨度={spanU:F2}x{spanV:F2} U/米={uPerM:F3} V/米={vPerM:F3}");
            }
            foreach (var u in uvIssues) sb.AppendLine(u);
            sb.AppendLine($"\n疑似 UV 异常网格 = {uvIssues.Count} 个");

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch2MaterialAudit.txt",
                              text, new UTF8Encoding(false));
        }
    }
}