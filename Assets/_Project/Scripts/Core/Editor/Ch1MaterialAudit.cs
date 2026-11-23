using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S67：第一章材质现状审计（只读）。
    /// 列出场景中每个 Renderer 用的材质，统计哪些是纯色、哪些有贴图。
    /// </summary>
    public static class Ch1MaterialAudit
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("══════════════════════════════════════════");
            sb.AppendLine("  第一章材质现状审计（只读）");
            sb.AppendLine("══════════════════════════════════════════");

            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter1_Graybox.unity",
                                         OpenSceneMode.Single);

            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            sb.AppendLine($"场景中 Renderer 总数 = {renderers.Length}");

            var matStat = new Dictionary<string, int>();
            var noMat = new List<string>();
            var pinkMat = new List<string>();
            int texCount = 0, solidCount = 0;

            foreach (var r in renderers)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats == null || mats.Length == 0) { noMat.Add(r.gameObject.name); continue; }

                foreach (var m in mats)
                {
                    if (m == null) { pinkMat.Add(r.gameObject.name); continue; }
                    if (!matStat.ContainsKey(m.name)) matStat[m.name] = 0;
                    matStat[m.name]++;
                }
            }

            sb.AppendLine($"  无材质 Renderer = {noMat.Count}");
            sb.AppendLine($"  Missing 材质槽 = {pinkMat.Count}");
            sb.AppendLine();

            sb.AppendLine("══════ 材质使用统计 ══════");
            sb.AppendLine($"{"材质名",-30} {"使用次数",-10} {"有贴图",-8} 来源路径");
            sb.AppendLine(new string('-', 96));

            var guidToPath = new Dictionary<string, string>();
            foreach (var g in AssetDatabase.FindAssets("t:Material"))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                guidToPath[Path.GetFileNameWithoutExtension(p)] = p;
            }

            var keys = new List<string>(matStat.Keys);
            keys.Sort((a, b) => matStat[b].CompareTo(matStat[a]));

            foreach (var k in keys)
            {
                string path = guidToPath.TryGetValue(k, out var pp) ? pp : "（不在 Assets 中）";
                bool hasTex = false;
                float metallic = -1, smooth = -1;
                Color col = Color.magenta;

                if (path.StartsWith("Assets") && File.Exists(path))
                {
                    string c = File.ReadAllText(path);
                    hasTex = System.Text.RegularExpressions.Regex.IsMatch(c, "m_Texture: \\{fileID: [0-9]{6,}");
                    var mm = System.Text.RegularExpressions.Regex.Match(c, "m_Metallic: ([\\d.]+)");
                    if (mm.Success) metallic = float.Parse(mm.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                    var sm = System.Text.RegularExpressions.Regex.Match(c, "m_Smoothness: ([\\d.]+)");
                    if (sm.Success) smooth = float.Parse(sm.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                    var cm = System.Text.RegularExpressions.Regex.Match(c, "m_Color: \\{r: ([\\d.]+), g: ([\\d.]+), b: ([\\d.]+)");
                    if (cm.Success) col = new Color(float.Parse(cm.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                                                   float.Parse(cm.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture),
                                                   float.Parse(cm.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture));
                }

                if (hasTex) texCount++; else solidCount++;

                sb.AppendLine($"{k,-30} {matStat[k],-10} {(hasTex ? "✅是" : "❌纯色"),-8} {path}");
                if (!hasTex)
                    sb.AppendLine($"{"",-30} 颜色=({col.r:F2},{col.g:F2},{col.b:F2}) Metallic={metallic:F2} Smooth={smooth:F2}");
            }

            sb.AppendLine();
            sb.AppendLine($"有贴图的材质 = {texCount}");
            sb.AppendLine($"纯色材质 = {solidCount}");

            // 逐 Renderer 清单
            sb.AppendLine();
            sb.AppendLine("══════ 逐 Renderer 材质分配 ══════");
            sb.AppendLine($"{"对象名",-34} {"材质",-28}");
            sb.AppendLine(new string('-', 80));

            var list = new List<string>();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                var names = new List<string>();
                if (mats != null) foreach (var m in mats) names.Add(m != null ? m.name : "NULL");
                list.Add($"  {r.gameObject.name,-32} {string.Join(",", names)}");
            }
            list.Sort();
            foreach (var l in list) sb.AppendLine(l);

            // 贴图资源
            sb.AppendLine();
            sb.AppendLine("══════ 可用 PBR 贴图资源 ══════");
            foreach (var d in Directory.GetDirectories("Assets/_Project/Art/Textures/ambientCG"))
            {
                string name = Path.GetFileName(d);
                var files = Directory.GetFiles(d, "*.jpg");
                sb.AppendLine($"  {name}  ({files.Length} 张)");
                foreach (var f in files)
                    sb.AppendLine($"    {Path.GetFileName(f)}");
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch1MaterialAudit.txt",
                              text, new UTF8Encoding(false));
        }
    }
}