using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S69：第三、四、五章材质现状审计（只读）。
    /// 任务要求先检查每章 Renderer 数量、材质槽、空材质、默认 Lit、纯色材质。
    /// </summary>
    public static class Ch345MaterialAudit
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine("  第三/四/五章材质现状审计（只读）");
            sb.AppendLine("═══════════════════════════════════════");

            AuditChapter(sb, 3, "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity");
            AuditChapter(sb, 4, "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity");
            AuditChapter(sb, 5, "Assets/_Project/Scenes/Level_Chapter5_Graybox.unity");

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch345MaterialAudit.txt",
                              text, new UTF8Encoding(false));
        }

        private static void AuditChapter(StringBuilder sb, int ch, string path)
        {
            sb.AppendLine();
            sb.AppendLine($"══════════ 第{ch}章 ══════════");

            try { EditorSceneManager.OpenScene(path, OpenSceneMode.Single); }
            catch { sb.AppendLine("  ❌ 场景打开失败"); return; }
            Physics.SyncTransforms();

            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            sb.AppendLine($"  Renderer 总数 = {renderers.Length}");

            var stat = new Dictionary<string, int>();
            var texStat = new Dictionary<string, bool>();
            int nullSlots = 0, defaultLit = 0, pink = 0;
            var defaultLitObjects = new List<string>();
            var nullObjects = new List<string>();

            foreach (var r in renderers)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats == null || mats.Length == 0) { nullSlots++; nullObjects.Add(r.gameObject.name); continue; }

                foreach (var m in mats)
                {
                    if (m == null) { nullSlots++; nullObjects.Add(r.gameObject.name); continue; }
                    if (!stat.ContainsKey(m.name)) stat[m.name] = 0;
                    stat[m.name]++;

                    if (m.name == "Lit" && AssetDatabase.GetAssetPath(m).StartsWith("Packages/"))
                    {
                        defaultLit++;
                        if (defaultLitObjects.Count < 200) defaultLitObjects.Add(r.gameObject.name);
                    }
                    if (m.shader != null && m.shader.name.Contains("Error")) pink++;

                    if (!texStat.ContainsKey(m.name))
                        texStat[m.name] = m.GetTexture("_BaseMap") != null;
                }
            }

            sb.AppendLine($"  空材质槽 = {nullSlots}");
            sb.AppendLine($"  URP 默认 Lit.mat = {defaultLit}");
            sb.AppendLine($"  错误 shader = {pink}");
            sb.AppendLine($"  材质种类 = {stat.Count}");
            int withTex = 0;
            foreach (var v in texStat.Values) if (v) withTex++;
            sb.AppendLine($"  有贴图的材质 = {withTex} / {texStat.Count}");

            sb.AppendLine();
            sb.AppendLine("  ── 材质使用统计 ──");
            var keys = new List<string>(stat.Keys);
            keys.Sort((a, b) => stat[b].CompareTo(stat[a]));
            foreach (var k in keys)
                sb.AppendLine($"    {k,-28} {stat[k],4} 次   贴图={(texStat[k] ? "✓" : "✗")}");

            if (defaultLitObjects.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"  ── 使用 URP 默认 Lit 的对象（前 30 个）──");
                for (int i = 0; i < Mathf.Min(30, defaultLitObjects.Count); i++)
                    sb.AppendLine($"    {defaultLitObjects[i]}");
                sb.AppendLine($"    ...共 {defaultLitObjects.Count} 个");
            }

            // UV 检查（只记录）
            sb.AppendLine();
            sb.AppendLine("  ── UV 异常网格（只记录）──");
            var uvIssues = new List<string>();
            var seen = new HashSet<Mesh>();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var mesh = mf.sharedMesh;
                if (!mesh.name.StartsWith("MESH_")) continue;
                if (!seen.Add(mesh)) continue;
                var uv = mesh.uv;
                if (uv == null || uv.Length == 0) continue;

                var ws = r.bounds.size;
                float mnU = 999, mxU = -999, mnV = 999, mxV = -999;
                foreach (var v in uv)
                {
                    mnU = Mathf.Min(mnU, v.x); mxU = Mathf.Max(mxU, v.x);
                    mnV = Mathf.Min(mnV, v.y); mxV = Mathf.Max(mxV, v.y);
                }
                float su = mxU - mnU, sv = mxV - mnV;
                float[] d = { ws.x, ws.y, ws.z };
                System.Array.Sort(d);
                float pm1 = su / Mathf.Max(0.01f, d[2]);
                float pm2 = sv / Mathf.Max(0.01f, d[1]);
                if ((pm1 > 0.001f && (pm1 < 0.25f || pm1 > 1.0f)) ||
                    (pm2 > 0.001f && (pm2 < 0.25f || pm2 > 1.0f)))
                    uvIssues.Add($"    {mesh.name,-34} 尺寸={ws.x:F1}x{ws.y:F1}x{ws.z:F1} UV={su:F2}x{sv:F2} U/米={pm1:F3} V/米={pm2:F3}");
            }
            foreach (var u in uvIssues) sb.AppendLine(u);
            sb.AppendLine($"    共 {uvIssues.Count} 个");
        }
    }
}