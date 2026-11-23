using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S77a：第五章结局触发区**实际位置诊断**（只读）。
    ///
    /// 玩家反馈：还没走到最后一间房间（H7 出口舱）就开始播结局了。
    ///
    /// 代码默认值：triggerCenter = (0, 1, 39)，triggerSize = (5, 3, 4)
    ///   → 触发范围 Z[37, 41]
    ///
    /// 第五章房间：
    ///   H6 升降井 中心 (0,0,40)  范围约 Z[36, 44]
    ///   H7 出口舱 中心 (0,0,54)  范围约 Z[48, 60]
    ///
    ///   所以 Z=39 落在 H6 内部 → 提前触发
    ///
    /// 本工具输出：
    ///   · 场景里实际的 ChapterEndingSystem 组件配置
    ///   · 所有 floor/room 的实际边界
    ///   · 触发区落在哪个房间
    ///   · 撤离控制台的实际位置
    ///
    /// **只读，不修改任何东西。**
    /// </summary>
    public static class Ch5EndingTriggerDiagnose
    {
        private const string SCENE = "Assets/_Project/Scenes/Level_Chapter5_Graybox.unity";

        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine("  第五章结局触发区诊断（只读）");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");

            EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);
            Physics.SyncTransforms();

            // ══════ 1. 结局系统实际配置 ══════
            sb.AppendLine();
            sb.AppendLine("──── ① ChapterEndingSystem 实际配置 ────");

            var endings = Object.FindObjectsByType<AbyssEcho.Narrative.ChapterEndingSystem>(FindObjectsSortMode.None);
            sb.AppendLine($"  找到 {endings.Length} 个 ChapterEndingSystem");

            var t = typeof(AbyssEcho.Narrative.ChapterEndingSystem);
            var tcF = t.GetField("triggerCenter", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            var tsF = t.GetField("triggerSize", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            var delayF = t.GetField("triggerDelay", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

            foreach (var e in endings)
            {
                if (e == null) continue;
                Vector3 tc = tcF != null ? (Vector3)tcF.GetValue(e) : Vector3.zero;
                Vector3 ts = tsF != null ? (Vector3)tsF.GetValue(e) : Vector3.zero;
                float delay = delayF != null ? (float)delayF.GetValue(e) : 0f;

                sb.AppendLine();
                sb.AppendLine($"  GameObject: {e.gameObject.name}   active={e.gameObject.activeInHierarchy}");
                sb.AppendLine($"    对象位置 = {e.transform.position}");
                sb.AppendLine($"    triggerCenter = {tc}");
                sb.AppendLine($"    triggerSize   = {ts}");
                sb.AppendLine($"    triggerDelay  = {delay}");
                sb.AppendLine($"    → 触发范围 X[{tc.x - ts.x / 2:F1}, {tc.x + ts.x / 2:F1}]  " +
                              $"Y[{tc.y - ts.y / 2:F1}, {tc.y + ts.y / 2:F1}]  " +
                              $"Z[{tc.z - ts.z / 2:F1}, {tc.z + ts.z / 2:F1}]");

                // 判断落在哪个房间
                float zMin = tc.z - ts.z / 2f;
                float zMax = tc.z + ts.z / 2f;
                sb.AppendLine($"    → 该范围属于：{WhichRoom(zMin, zMax)}");
            }

            // ══════ 2. 第五章房间实际边界（从 floor 对象读）══════
            sb.AppendLine();
            sb.AppendLine("──── ② 第五章各房间的 floor 实际边界 ────");
            sb.AppendLine($"  {"对象",-22} {"X 范围",-22} {"Y",-14} {"Z 范围",-22} 中心");

            for (int i = 1; i <= 7; i++)
            {
                var go = GameObject.Find($"Floor_H{i}");
                if (go == null) continue;
                var r = go.GetComponent<Renderer>();
                if (r == null) continue;
                var b = r.bounds;
                sb.AppendLine($"  {go.name,-22} " +
                              $"[{b.min.x,7:F1},{b.max.x,7:F1}]".PadRight(22) +
                              $"[{b.min.y,5:F1},{b.max.y,5:F1}]".PadRight(14) +
                              $"[{b.min.z,7:F1},{b.max.z,7:F1}]".PadRight(22) +
                              $"({b.center.x:F1},{b.center.z:F1})");
            }

            // ══════ 3. 撤离控制台位置 ══════
            sb.AppendLine();
            sb.AppendLine("──── ③ 撤离控制台（EvacuationConsole）位置 ────");
            var consoles = Object.FindObjectsByType<AbyssEcho.Puzzle.EvacuationConsole>(FindObjectsSortMode.None);
            sb.AppendLine($"  找到 {consoles.Length} 个");
            foreach (var c in consoles)
            {
                if (c == null) continue;
                sb.AppendLine($"    {c.gameObject.name}  @ {c.transform.position}");
                sb.AppendLine($"      HasChosen={c.HasChosen}  ChosenEnding='{c.ChosenEnding}'");
                var exF = typeof(AbyssEcho.Puzzle.EvacuationConsole).GetField("exitDoor");
                var ed = exF?.GetValue(c) as MonoBehaviour;
                sb.AppendLine($"      exitDoor={(ed != null ? ed.gameObject.name + " @ " + ed.transform.position : "null")}");
            }

            // ══════ 4. 其他触发相关对象 ══════
            sb.AppendLine();
            sb.AppendLine("──── ④ 场景里名字含 Ending/Trigger/Exit 的对象 ────");
            foreach (var go in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                string n = go.name;
                if (n.Contains("Ending") || n.Contains("Trigger") || n.Contains("Exit") ||
                    go.GetComponent<AbyssEcho.Narrative.ChapterEndingSystem>() != null)
                {
                    sb.AppendLine($"  {n,-32} @ {go.transform.position}  组件={DescribeComponents(go.gameObject)}");
                }
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch5EndingTriggerDiagnose.txt",
                              text, new UTF8Encoding(false));
        }

        private static string WhichRoom(float zMin, float zMax)
        {
            // 按房间 Z 范围判断
            var rooms = new (string name, float zmin, float zmax)[]
            {
                ("H1 底层平台", -5f, 5f),
                ("H2 上行廊", 6f, 20f),
                ("H3 中转平台", 24f, 34f),
                ("H4 值班室", 25f, 33f),
                ("H5 仓库", 25f, 33f),
                ("H6 升降井", 36f, 44f),
                ("H7 出口舱", 48f, 60f),
            };

            foreach (var r in rooms)
            {
                if (zMin >= r.zmin - 1f && zMax <= r.zmax + 1f)
                    return $"{r.name}（完整落在内）";
            }
            foreach (var r in rooms)
            {
                if (zMax >= r.zmin && zMin <= r.zmax)
                    return $"跨越 {r.name}（部分重叠）";
            }
            return "未命中任何房间";
        }

        private static string DescribeComponents(GameObject go)
        {
            var names = new List<string>();
            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null) { names.Add("MissingScript!"); continue; }
                if (c is Transform || c is MeshFilter || c is MeshRenderer) continue;
                names.Add(c.GetType().Name);
            }
            return names.Count > 0 ? string.Join(",", names) : "无";
        }
    }
}