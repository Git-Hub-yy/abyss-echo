using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S54：第四章 G5 → G7 通路真实几何审计。
    ///
    /// ══════════════════ 检查什么 ══════════════════
    ///
    ///   不能只看房间坐标 —— 矩形房间"相接"不代表**有门洞**。
    ///   要从 G5 内部逐段向 G7 方向做射线/胶囊检测，
    ///   确认墙上真的有洞、洞的高度够玩家通过。
    ///
    ///   同时检查：
    ///     · 核心西门 Door_HeartGate 是否挡住通路
    ///     · 心瓣 HeartValve 是否真实存在、启用、可交互
    ///     · G7 内部是否空旷（没有误放的墙体）
    /// </summary>
    public static class Ch4G5G7Audit
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("══════ 第四章 G5 → G7 通路审计 ══════");

            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter4_Graybox.unity",
                                         OpenSceneMode.Single);

            // ══ 1. 关键对象完整信息 ══
            sb.AppendLine();
            sb.AppendLine("──── [1] 关键对象 ────");
            foreach (var name in new[] { "G5", "G5_G7_Link", "G7", "Door_HeartGate", "HeartValve" })
            {
                var go = GameObject.Find(name);
                if (go == null)
                {
                    sb.AppendLine($"  ❌ 找不到对象 {name}");
                    continue;
                }
                DumpObject(sb, go);
            }

            // 按名称模糊找
            sb.AppendLine();
            sb.AppendLine("  模糊查找（含组织甬道/核心西门/心瓣）：");
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                string n = t.name;
                if (n.Contains("组织甬道") || n.Contains("核心西门") || n.Contains("心瓣") ||
                    n == "G5" || n == "G7" || n.Contains("HeartValve") || n.Contains("HeartGate"))
                {
                    DumpObject(sb, t.gameObject, "    ");
                }
            }

            // ══ 2. 墙体几何 ══
            sb.AppendLine();
            sb.AppendLine("──── [2] G5 / G7 附近的墙体碰撞体 ────");
            var walls = new List<(string name, Vector3 min, Vector3 max)>();
            foreach (var c in Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None))
            {
                if (c.isTrigger) continue;
                Vector3 p = c.transform.position;
                // G5 中心 (-19,0,0) 到 G7 中心 (0,0,16) 之间
                if (p.x > -30f && p.x < 12f && p.z > -12f && p.z < 28f)
                {
                    Vector3 size = Vector3.Scale(c.size, c.transform.lossyScale);
                    Vector3 min = p - size * 0.5f;
                    Vector3 max = p + size * 0.5f;
                    walls.Add((c.gameObject.name, min, max));
                }
            }
            sb.AppendLine($"  该区域 BoxCollider 数量 = {walls.Count}");
            int shown = 0;
            foreach (var w in walls)
            {
                if (shown++ >= 25) { sb.AppendLine("    ..."); break; }
                sb.AppendLine($"    {w.name,-28} x[{w.min.x,7:F1},{w.max.x,7:F1}] " +
                              $"y[{w.min.y,6:F1},{w.max.y,6:F1}] z[{w.min.z,7:F1},{w.max.z,7:F1}]");
            }

            // ══ 3. 玩家实际路径检测 ══
            sb.AppendLine();
            sb.AppendLine("──── [3] G5 → G7 逐段通路检测 ────");
            sb.AppendLine("  用玩家胶囊（半径 0.35，高 1.8）沿路线逐段检测：");

            var path = new[]
            {
                new Vector3(-19f, 0.9f, 0f),     // G5 中心
                new Vector3(-19f, 0.9f, 4f),     // G5 北侧
                new Vector3(-19f, 0.9f, 6f),     // 进入 G5_G7_Link?（实际 G5 东门在 z=8）
                new Vector3(-19f, 0.9f, 8f),     // G5 东门位置
                new Vector3(-15f, 0.9f, 8f),     // 走廊中段
                new Vector3(-11.5f, 0.9f, 8f),   // 核心西门
                new Vector3(-8f, 0.9f, 8f),      // 进入 G7
                new Vector3(-5f, 0.9f, 10f),     // G7 内
                new Vector3(0f, 0.9f, 16f),      // 心瓣位置
            };

            for (int i = 0; i < path.Length - 1; i++)
            {
                Vector3 a = path[i], b = path[i + 1];
                Vector3 dir = (b - a).normalized;
                float dist = Vector3.Distance(a, b);

                // 胶囊检测（玩家体积）
                bool blocked = Physics.CapsuleCast(
                    a + Vector3.up * 0.9f, a + Vector3.up * 0.1f, 0.35f,
                    dir, out var hit, dist, ~0, QueryTriggerInteraction.Ignore);

                // 直接看该点是否有物体
                var atPoint = Physics.OverlapCapsule(a + Vector3.up * 0.1f, a + Vector3.up * 1.7f,
                                                     0.35f, ~0, QueryTriggerInteraction.Ignore);
                string atNames = "";
                foreach (var o in atPoint)
                {
                    if (o == null || o.isTrigger) continue;
                    atNames += o.gameObject.name + " ";
                }

                sb.AppendLine($"    [{i}] {a} → {b}");
                sb.AppendLine($"        长度 {dist:F1}m  胶囊阻挡={(blocked ? "❌ 是" : "✅ 否")}" +
                              (blocked ? $" 撞到 '{hit.collider.gameObject.name}' @ {hit.point}" : ""));
                if (atNames.Length > 0)
                    sb.AppendLine($"        起点处重叠: {atNames}");
            }

            // ══ 4. 心瓣可交互性 ══
            sb.AppendLine();
            sb.AppendLine("──── [4] 心瓣 HeartValve ────");
            var valve = GameObject.Find("HeartValve");
            if (valve != null)
            {
                var pulse = valve.GetComponent<AbyssEcho.Puzzle.PulseSyncPuzzle>();
                var col = valve.GetComponent<Collider>();
                sb.AppendLine($"  存在 = ✅  位置 = {valve.transform.position}");
                sb.AppendLine($"  activeInHierarchy = {valve.activeInHierarchy}");
                sb.AppendLine($"  PulseSyncPuzzle = {(pulse != null ? (pulse.enabled ? "✅ 启用" : "❌ 禁用") : "❌ 无组件")}");
                sb.AppendLine($"  Collider = {(col != null ? col.GetType().Name + $" isTrigger={col.isTrigger}" : "❌ 无")}");

                if (pulse != null)
                {
                    var so = new SerializedObject(pulse);
                    sb.AppendLine($"  beatInterval = {so.FindProperty("beatInterval")?.floatValue}");
                    sb.AppendLine($"  syncWindow = {so.FindProperty("syncWindow")?.floatValue}");
                    sb.AppendLine($"  requiredSyncs = {so.FindProperty("requiredSyncs")?.intValue}");
                    sb.AppendLine($"  doorToUnlock = {(so.FindProperty("doorToUnlock")?.objectReferenceValue != null ? "✅ 有引用" : "❌ 空")}");
                    var ip = so.FindProperty("interactDistance");
                    if (ip != null) sb.AppendLine($"  interactDistance = {ip.floatValue}");
                }
            }

            // ══ 5. 门状态 ══
            sb.AppendLine();
            sb.AppendLine("──── [5] Door_HeartGate ────");
            var gate = GameObject.Find("Door_HeartGate");
            if (gate != null)
            {
                var door = gate.GetComponent<AbyssEcho.Interaction.DoorInteractable>();
                sb.AppendLine($"  位置 = {gate.transform.position}");
                if (door != null)
                {
                    sb.AppendLine($"  locked = {door.locked}  IsOpen = {door.IsOpen}");
                    sb.AppendLine($"  slideDistance = {door.slideDistance}");
                    var so = new SerializedObject(door);
                    sb.AppendLine($"  unlockableByInteraction = {so.FindProperty("unlockableByInteraction")?.boolValue}");
                }
            }

            // ══ 6. 视野遮挡：从 G5 看 G7 ══
            sb.AppendLine();
            sb.AppendLine("──── [6] 玩家视角遮挡（1.67m 眼高）────");
            Vector3 eye = new Vector3(-19f, 1.67f, 0f);
            foreach (var target in new[]
            {
                new Vector3(-13f, 1.67f, 8f),
                new Vector3(-10.5f, 1.67f, 8f),
                new Vector3(-8f, 1.67f, 8f),
                new Vector3(0f, 1.5f, 16f),
            })
            {
                Vector3 d = target - eye;
                bool hit = Physics.Raycast(eye, d.normalized, out var h, d.magnitude,
                                           ~0, QueryTriggerInteraction.Ignore);
                sb.AppendLine($"    看向 {target}: {(hit ? $"被 '{h.collider.gameObject.name}' 挡住 @ {h.point}" : "✅ 可见")}");
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(Application.dataPath), "../Reports/Ch4G5G7_Audit.txt")),
                text, new UTF8Encoding(false));
        }

        private static void DumpObject(StringBuilder sb, GameObject go, string indent = "  ")
        {
            string path = GetPath(go.transform);
            sb.AppendLine($"{indent}● {path}");
            sb.AppendLine($"{indent}  位置 = {go.transform.position}");
            sb.AppendLine($"{indent}  旋转 = {go.transform.eulerAngles}");
            sb.AppendLine($"{indent}  缩放 = {go.transform.lossyScale}");
            sb.AppendLine($"{indent}  activeSelf={go.activeSelf} activeInHierarchy={go.activeInHierarchy}");

            var cols = go.GetComponents<Collider>();
            if (cols.Length == 0) sb.AppendLine($"{indent}  Collider = 无");
            foreach (var c in cols)
            {
                string size = "";
                if (c is BoxCollider bc) size = $" size={Vector3.Scale(bc.size, c.transform.lossyScale)} center={bc.center}";
                else if (c is CapsuleCollider cap) size = $" radius={cap.radius} height={cap.height}";
                sb.AppendLine($"{indent}  Collider = {c.GetType().Name}{size} isTrigger={c.isTrigger} enabled={c.enabled}");
            }

            var comps = go.GetComponents<Component>();
            var names = new List<string>();
            foreach (var c in comps) if (c != null) names.Add(c.GetType().Name);
            sb.AppendLine($"{indent}  组件 = {string.Join(", ", names)}");
        }

        private static string GetPath(Transform t)
        {
            string p = t.name;
            var cur = t.parent; int g = 0;
            while (cur != null && g++ < 20) { p = cur.name + "/" + p; cur = cur.parent; }
            return p;
        }
    }
}