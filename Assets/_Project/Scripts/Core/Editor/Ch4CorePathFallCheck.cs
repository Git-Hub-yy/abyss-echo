using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S71a：第四章「前往核心腔」路径的**逐点落地检查**。
    ///
    /// 玩家反馈："进入核心舱的路上又掉落虚空的安全隐患"
    ///
    /// ══════════════════════ 检查方式 ══════════════════════
    ///
    ///   不用 Raycast（会被墙挡住），改用**密集采样 + 地面碰撞体检测**：
    ///
    ///   对路线上每个采样点 (x, z)：
    ///     ① 向下发射射线，看是否命中"可站立"的地面碰撞体
    ///     ② 排除 Floor/地面 以外的墙、顶、道具
    ///     ③ 记录地面高度，若低于 -1 米 → 判定为虚空
    ///
    ///   采样密度：每 0.25 米一个点 —— 比之前的 0.5 米更细，
    ///   因为 2 米的虚空缺口用 0.5 米采样可能漏掉。
    ///
    /// ══════════════════════ 检查范围 ══════════════════════
    ///
    ///   G5 北门 → G5_G7_Link → G7 西门 → G7 内部
    ///   同时检查 G5 内部到北门的路径。
    ///
    /// 只读。
    /// </summary>
    public static class Ch4CorePathFallCheck
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity";

        // 采样密度（米）
        private const float Step = 0.25f;

        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════");
            sb.AppendLine("  第四章 前往核心腔路径 —— 落地检查");
            sb.AppendLine($"  采样密度 {Step} 米");
            sb.AppendLine("═══════════════════════════════════════════════════");

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();

            // ══ 找关键位置 ══
            var g5 = GameObject.Find("Floor_G5");
            var link = GameObject.Find("Floor_G5_G7_Link");
            var g7 = GameObject.Find("Floor_G7");

            sb.AppendLine();
            sb.AppendLine("── 关键地面对象 ──");
            Report(sb, "Floor_G5", g5);
            Report(sb, "Floor_G5_G7_Link", link);
            Report(sb, "Floor_G7", g7);

            // 列出所有 G5/G7 相关地面
            sb.AppendLine();
            sb.AppendLine("── 所有含 G5 或 G7 的地面对象 ──");
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (r == null) continue;
                string n = r.gameObject.name;
                if (!n.Contains("Floor") && !n.Contains("Transit")) continue;
                if (!n.Contains("G5") && !n.Contains("G7")) continue;

                var b = r.bounds;
                sb.AppendLine($"  {n,-32} X[{b.min.x,7:F2},{b.max.x,7:F2}] " +
                              $"Y[{b.min.y,6:F2},{b.max.y,6:F2}] Z[{b.min.z,7:F2},{b.max.z,7:F2}]");
            }

            // ══ 密集采样：从 G5 中心向北走到 G7 中心 ══
            sb.AppendLine();
            sb.AppendLine("── 路径采样（沿 x=-19 从 z=3 到 z=25）──");
            sb.AppendLine($"{"z",-8} {"地面高度",-12} {"命中的对象",-32} 状态");
            sb.AppendLine(new string('-', 90));

            var holes = new List<float>();
            float lastHoleStart = -999, lastHoleEnd = -999;

            for (float z = 3f; z <= 25f; z += Step)
            {
                Vector3 origin = new Vector3(-19f, 6f, z);
                float groundY = -999f;
                string hitName = "无";

                // 向下射线，找最高的地面碰撞体
                var hits = Physics.RaycastAll(origin, Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.point.y.CompareTo(b.point.y));

                foreach (var h in hits)
                {
                    if (h.collider == null) continue;
                    string n = h.collider.gameObject.name;

                    // 只要地面类
                    bool isFloor = n.StartsWith("Floor") || n.Contains("Floor") ||
                                   n.Contains("Transit") || n.Contains("G5Door_Threshold");
                    if (!isFloor) continue;

                    // 该点是否真的在这个碰撞体范围内
                    var b = h.collider.bounds;
                    if (-19f < b.min.x - 0.01f || -19f > b.max.x + 0.01f) continue;
                    if (z < b.min.z - 0.01f || z > b.max.z + 0.01f) continue;

                    if (h.point.y > groundY)
                    {
                        groundY = h.point.y;
                        hitName = n;
                    }
                }

                bool isHole = groundY < -999f + 1f;

                if (isHole)
                {
                    holes.Add(z);
                    if (lastHoleStart < -900f) lastHoleStart = z;
                    lastHoleEnd = z;
                    sb.AppendLine($"{z,-8:F2} {"无地面",-12} {hitName,-32} ❌ 虚空");
                }
                else
                {
                    if (lastHoleStart > -900f)
                    {
                        sb.AppendLine($"  └─ 虚空区间结束: z[{lastHoleStart:F2}, {lastHoleEnd:F2}]  长度 {lastHoleEnd - lastHoleStart:F2} 米");
                        lastHoleStart = -999f;
                    }
                    // 只打印每 1 米一次，避免刷屏
                    if (Mathf.Abs(z - Mathf.Round(z)) < Step * 0.5f)
                        sb.AppendLine($"{z,-8:F2} {groundY,-12:F2} {hitName,-32} ✅");
                }
            }

            if (lastHoleStart > -900f)
                sb.AppendLine($"  └─ 虚空区间结束: z[{lastHoleStart:F2}, {lastHoleEnd:F2}]  长度 {lastHoleEnd - lastHoleStart:F2} 米");

            // ══ 横向扫描：检查连廊宽度方向是否也完整 ══
            sb.AppendLine();
            sb.AppendLine("── 横向扫描（在 z=5~9 之间，x 从 -25 到 -13）──");
            var holesX = new List<(float z, float x)>();

            for (float z = 4.5f; z <= 9.5f; z += 0.5f)
            {
                var row = new StringBuilder();
                row.Append($"  z={z:F1}: ");
                for (float x = -25f; x <= -13f; x += 0.5f)
                {
                    Vector3 o = new Vector3(x, 6f, z);
                    bool hasFloor = false;
                    var hits = Physics.RaycastAll(o, Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore);
                    foreach (var h in hits)
                    {
                        if (h.collider == null) continue;
                        string n = h.collider.gameObject.name;
                        bool isFloor = n.StartsWith("Floor") || n.Contains("Floor") ||
                                       n.Contains("Transit") || n.Contains("G5Door_Threshold");
                        if (!isFloor) continue;
                        var b = h.collider.bounds;
                        if (x < b.min.x - 0.01f || x > b.max.x + 0.01f) continue;
                        if (z < b.min.z - 0.01f || z > b.max.z + 0.01f) continue;
                        hasFloor = true;
                        break;
                    }
                    row.Append(hasFloor ? "█" : "·");
                    if (!hasFloor) holesX.Add((z, x));
                }
                sb.AppendLine(row.ToString());
            }

            sb.AppendLine();
            sb.AppendLine("  图例：█=有地面  ·=虚空");
            sb.AppendLine($"  横向扫描发现虚空点 {holesX.Count} 个");
            if (holesX.Count > 0)
            {
                for (int i = 0; i < Mathf.Min(20, holesX.Count); i++)
                    sb.AppendLine($"    虚空点 z={holesX[i].z:F1} x={holesX[i].x:F1}");
            }

            // ══ 总结 ══
            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════════════════");
            sb.AppendLine($"  纵向采样虚空点 {holes.Count} 个");
            if (holes.Count > 0)
            {
                holes.Sort();
                sb.AppendLine($"  虚空范围: z[{holes[0]:F2}, {holes[holes.Count - 1]:F2}]");
            }
            sb.AppendLine("═══════════════════════════════════════════════════");

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch4CorePathFallCheck.txt",
                              text, new UTF8Encoding(false));
        }

        private static void Report(StringBuilder sb, string label, GameObject go)
        {
            if (go == null) { sb.AppendLine($"  ❌ {label} 不存在"); return; }
            var b = go.GetComponent<Renderer>()?.bounds ?? new Bounds();
            sb.AppendLine($"  {label,-24} X[{b.min.x,7:F2},{b.max.x,7:F2}] " +
                          $"Y[{b.min.y,6:F2},{b.max.y,6:F2}] Z[{b.min.z,7:F2},{b.max.z,7:F2}]");
        }
    }
}