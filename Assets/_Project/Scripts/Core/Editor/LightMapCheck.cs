using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class LightMapCheck {
        public static void Run() {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter4_Graybox.unity", OpenSceneMode.Single);

            sb.AppendLine("══════ 第四章光源分布 ══════");
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            sb.AppendLine($"场景内 Light 总数 = {lights.Length}");
            sb.AppendLine();

            // 按区域统计
            var regions = new (string name, float x0, float x1, float z0, float z1)[] {
                ("G1 入口腔",     -4, 4,   -4, 4),
                ("G1_G2_Link",   -1.3f, 1.3f, -10, -4),
                ("G2 环形廊",    -12, 12, -14, -10),
                ("G2_G3_Link",   -14, -12, -12.7f, -11.3f),
                ("G3 消化囊",    -24, -14, -17, -7),
                ("G3_G5_Link",   -20.1f, -17.9f, -7, -5),
                ("G5 组织层",    -25, -13, -5, 5),
                ("过渡区",        -20.2f, -9.9f, 5, 6.9f),
                ("G5_G7_Link",   -13, -10, 5, 9.2f),
                ("G7 核心腔",    -10, 10, 7, 25),
                ("G6 神经束",    13, 25, -5, 5),
                ("G4 膜层",      14, 24, -17, -7),
            };

            sb.AppendLine($"{"区域",-16} {"光源数",-8} 详情");
            sb.AppendLine(new string('-', 90));
            foreach (var r in regions) {
                var inR = new System.Collections.Generic.List<Light>();
                foreach (var l in lights) {
                    Vector3 p = l.transform.position;
                    if (p.x >= r.x0 && p.x <= r.x1 && p.z >= r.z0 && p.z <= r.z1) inR.Add(l);
                }
                var detail = new StringBuilder();
                foreach (var l in inR)
                    detail.Append($"[{l.transform.position.x:F1},{l.transform.position.z:F1} int={l.intensity:F1} r={l.range:F0}] ");
                string flag = inR.Count == 0 ? "  ← ❌ 无光！" : "";
                sb.AppendLine($"{r.name,-16} {inR.Count,-8} {detail}{flag}");
            }

            sb.AppendLine();
            sb.AppendLine("══════ 全部光源坐标 ══════");
            foreach (var l in lights)
                sb.AppendLine($"  {l.gameObject.name,-30} {l.transform.position}  int={l.intensity:F1} range={l.range:F1} enabled={l.enabled}");

            sb.AppendLine();
            sb.AppendLine("══════ 环境光设置 ══════");
            sb.AppendLine($"  ambientMode = {RenderSettings.ambientMode}");
            sb.AppendLine($"  ambientLight = {RenderSettings.ambientLight}");
            sb.AppendLine($"  ambientIntensity = {RenderSettings.ambientIntensity}");
            sb.AppendLine($"  fog = {RenderSettings.fog}, density = {RenderSettings.fogDensity}");

            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch4LightMap.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}