using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S76b：第五章**出生点光照分析**（只读）。
    ///
    /// 玩家截图的出生点位置：坐标显示 (0.00, 0.00, 18.00) 附近
    /// 表现：紫、粉、绿三种有机材质全部过曝，像荧光塑料
    ///
    /// 但同一章其他位置（朝南）的膜是暗紫色，正常。
    ///
    /// 所以问题不是材质颜色，而是**该位置光照过强**。
    ///
    /// 本工具输出：
    ///   · 出生点附近的相机位置
    ///   · 该位置 20 米内的所有灯光（类型、强度、范围、颜色）
    ///   · 该位置可见的有机墙
    ///   · 与该位置的平均照度估算
    ///
    /// 只读。
    /// </summary>
    public static class Ch5SpawnLighting
    {
        private const string SCENE = "Assets/_Project/Scenes/Level_Chapter5_Graybox.unity";

        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine("  第五章出生点光照分析");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");

            EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);
            Physics.SyncTransforms();

            // 找玩家出生点
            var motor = Object.FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            Vector3 spawn = motor != null ? motor.transform.position : Vector3.zero;
            sb.AppendLine();
            sb.AppendLine($"玩家出生点 = {spawn}");

            // ── 出生点周围灯光 ──
            sb.AppendLine();
            sb.AppendLine("──── 出生点 25 米内的灯光 ────");
            sb.AppendLine($"{"灯光名",-28} {"类型",-10} {"强度",-8} {"范围",-8} {"颜色",-22} {"距离",-8} 启用");

            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            var near = new List<(Light l, float d)>();
            foreach (var l in lights)
            {
                if (l == null) continue;
                float d = Vector3.Distance(l.transform.position, spawn);
                if (d <= 25f) near.Add((l, d));
            }
            near.Sort((a, b) => a.d.CompareTo(b.d));

            foreach (var (l, d) in near)
            {
                var c = l.color;
                sb.AppendLine($"{l.gameObject.name,-28} {l.type,-10} {l.intensity,-8:F2} {l.range,-8:F1} " +
                              $"({c.r:F2},{c.g:F2},{c.b:F2})".PadRight(22) + $" {d,-8:F1} {(l.enabled ? "✓" : "✗")}");
            }

            // ── 出生点视野内的有机体 ──
            sb.AppendLine();
            sb.AppendLine("──── 出生点 30 米内的有机材质物体 ────");
            sb.AppendLine($"{"对象",-26} {"材质",-24} {"_BaseColor 亮度",-16} {"距离",-8}");
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (r == null || r.sharedMaterial == null) continue;
                if (!r.sharedMaterial.name.StartsWith("M_Org_")) continue;
                float d = Vector3.Distance(r.bounds.center, spawn);
                if (d > 30f) continue;
                var bc = r.sharedMaterial.GetColor("_BaseColor");
                sb.AppendLine($"{r.gameObject.name,-26} {r.sharedMaterial.name,-24} {bc.maxColorComponent,-16:F2} {d,-8:F1}");
            }

            // ── 全局光照概览 ──
            sb.AppendLine();
            sb.AppendLine("──── 第五章全部灯光统计 ────");
            int total = 0, enabled = 0, dirCount = 0, pointCount = 0, spotCount = 0;
            float maxIntensity = 0;
            string maxName = "";
            foreach (var l in lights)
            {
                if (l == null) continue;
                total++;
                if (!l.enabled) continue;
                enabled++;
                if (l.type == LightType.Directional) dirCount++;
                else if (l.type == LightType.Point) pointCount++;
                else if (l.type == LightType.Spot) spotCount++;
                if (l.intensity > maxIntensity) { maxIntensity = l.intensity; maxName = l.gameObject.name; }
            }
            sb.AppendLine($"  灯光总数={total}  启用={enabled}  平行光={dirCount}  点光={pointCount}  聚光={spotCount}");
            sb.AppendLine($"  最亮灯光 = {maxName} 强度={maxIntensity:F2}");

            // ── 环境光 ──
            var ambient = RenderSettings.ambientLight;
            sb.AppendLine();
            sb.AppendLine("──── 环境光 ────");
            sb.AppendLine($"  ambientMode = {RenderSettings.ambientMode}");
            sb.AppendLine($"  ambientLight = ({ambient.r:F3},{ambient.g:F3},{ambient.b:F3})  亮度={ambient.maxColorComponent:F3}");
            sb.AppendLine($"  skybox = {(RenderSettings.skybox != null ? RenderSettings.skybox.name : "无")}");

            // ── URP 后处理 ──
            sb.AppendLine();
            sb.AppendLine("──── 对比：第三章（同样材质但视觉正常）────");
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter3_Graybox.unity", OpenSceneMode.Single);
            int ch3Lights = 0;
            float ch3Sum = 0;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l == null || !l.enabled) continue;
                ch3Lights++;
                if (l.type == LightType.Point || l.type == LightType.Spot) ch3Sum += l.intensity;
            }
            sb.AppendLine($"  第三章：启用灯光={ch3Lights}  点光+聚光强度和={ch3Sum:F1}");

            // 回第五章算同样的指标
            EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);
            int ch5Lights = 0;
            float ch5Sum = 0;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l == null || !l.enabled) continue;
                ch5Lights++;
                if (l.type == LightType.Point || l.type == LightType.Spot) ch5Sum += l.intensity;
            }
            sb.AppendLine($"  第五章：启用灯光={ch5Lights}  点光+聚光强度和={ch5Sum:F1}");
            sb.AppendLine($"  → 第五章比第三章亮度 = {(ch3Sum > 0 ? ch5Sum / ch3Sum : 0):F2} 倍");

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch5SpawnLighting.txt",
                              text, new UTF8Encoding(false));
        }
    }
}