using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S56b：第四章通行性精确复测（只读）。
    ///
    /// 上一版 CapsuleCast 起点 y=0.05，胶囊半径 0.35 导致底部沉到 y=-0.30，
    /// 误撞地板（顶面 y=0）。本版把胶囊底部放在 y=0.1 之上。
    /// </summary>
    public static class Ch4RouteExact
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("══════ 第四章通行性精确复测（只读）══════");
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter4_Graybox.unity",
                                         OpenSceneMode.Single);
            Physics.SyncTransforms();

            // 玩家胶囊：半径 0.35，总高 1.8 → 球心距 1.8 - 2*0.35 = 1.1
            const float radius = 0.35f;
            const float sphereOffset = 1.1f;
            const float footY = 0.1f;   // 胶囊底部离地 0.1（玩家站在地面上）

            sb.AppendLine($"检测用玩家胶囊：半径={radius} 底部离地={footY} 高=1.8");
            sb.AppendLine();

            // ══ 路线 A：主路（心瓣往北，绕开核心）══
            sb.AppendLine("──── 路线A：G5 → 门洞 → G7 → 心瓣 → 绕过核心 → 门 ────");
            var routeA = new List<Vector3>
            {
                new Vector3(-19f, 0f, 0f),
                new Vector3(-18f, 0f, 3.5f),
                new Vector3(-16.5f, 0f, 7f),
                new Vector3(-13f, 0f, 8f),
                new Vector3(-11.5f, 0f, 8f),
                new Vector3(-9.2f, 0f, 8.5f),
                new Vector3(-7f, 0f, 10f),
                new Vector3(-3f, 0f, 12f),
                new Vector3(0f, 0f, 13.5f),      // 心瓣南侧
            };
            // 绕核心：核心 x[-2,2] z[19,23]，从东侧绕过（x=4）
            var detour = new List<Vector3>
            {
                new Vector3(0f, 0f, 13.5f),
                new Vector3(4f, 0f, 16f),        // 往东
                new Vector3(4.5f, 0f, 20f),      // 沿核心东侧北上
                new Vector3(4f, 0f, 23.5f),      // 绕过核心
                new Vector3(1.5f, 0f, 24f),      // 回到门正前方
            };

            CheckPath(sb, "A1 主路到心瓣", routeA, radius, sphereOffset, footY);
            sb.AppendLine();
            CheckPath(sb, "A2 绕核心东侧到门前", detour, radius, sphereOffset, footY);

            // ══ 路线 B：西侧绕行 ══
            sb.AppendLine();
            sb.AppendLine("──── 路线B：西侧绕核心 ────");
            var detourW = new List<Vector3>
            {
                new Vector3(0f, 0f, 13.5f),
                new Vector3(-4f, 0f, 16f),
                new Vector3(-4.5f, 0f, 20f),
                new Vector3(-4f, 0f, 23.5f),
                new Vector3(-1.5f, 0f, 24f),
            };
            CheckPath(sb, "B 绕核心西侧到门前", detourW, radius, sphereOffset, footY);

            // ══ 门本身 ══
            sb.AppendLine();
            sb.AppendLine("──── 门与触发区 ────");
            var gate = GameObject.Find("Door_HeartGate");
            if (gate != null)
            {
                var gc = gate.GetComponent<Collider>();
                sb.AppendLine($"  门包围盒 = x[{gc.bounds.min.x:F2},{gc.bounds.max.x:F2}] " +
                              $"y[{gc.bounds.min.y:F2},{gc.bounds.max.y:F2}] " +
                              $"z[{gc.bounds.min.z:F2},{gc.bounds.max.z:F2}]");
                sb.AppendLine($"  门高 = {gc.bounds.size.y:F2} 米（玩家高 1.8 → {(gc.bounds.size.y > 1.8 ? "可通过" : "无法通过")}）");
            }

            // 门北侧是什么？
            sb.AppendLine();
            sb.AppendLine("  门北侧 (z > 25) 的对象：");
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                Vector3 p = t.position;
                if (p.z > 25f && p.z < 45f && Mathf.Abs(p.x) < 12f)
                {
                    var c = t.GetComponent<Collider>();
                    string ci = "";
                    if (c is BoxCollider bc && !c.isTrigger)
                    {
                        Vector3 s = Vector3.Scale(bc.size, t.lossyScale);
                        ci = $" {s.x:F1}×{s.y:F1}×{s.z:F1}";
                    }
                    sb.AppendLine($"    {t.name,-28} @ ({p.x,6:F1},{p.y,5:F1},{p.z,6:F1}){(c != null ? " 碰撞" + ci : "")}");
                }
            }

            // ══ 怪物相对位置分析 ══
            sb.AppendLine();
            sb.AppendLine("──── 怪物与主要走廊的关系 ────");
            var playerStart = AbyssEcho.LevelGen.ChapterLayout.GetPlayerStart(
                AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter4);
            sb.AppendLine($"  玩家起点 = {playerStart}");
            sb.AppendLine();

            foreach (var m in Object.FindObjectsByType<AbyssEcho.Horror.MimicStalker>(FindObjectsSortMode.None))
            {
                Vector3 p = m.transform.position;
                float d = Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(playerStart.x, 0, playerStart.z));

                // 从怪物到玩家的视线遮挡
                Vector3 from = p + Vector3.up * 1.2f;
                Vector3 to = playerStart + Vector3.up * 1.2f;
                Vector3 dir = to - from;
                string vis = "视线通畅（玩家一出生就能看到）";
                if (Physics.Raycast(from, dir.normalized, out var hit, dir.magnitude, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider != null && !hit.collider.transform.IsChildOf(m.transform))
                        vis = $"被 '{hit.collider.gameObject.name}' 遮挡";
                }

                sb.AppendLine($"  {m.gameObject.name} @ ({p.x:F1},{p.z:F1})");
                sb.AppendLine($"    距玩家起点 = {d:F1}m");
                sb.AppendLine($"    出生点视线 = {vis}");
            }

            // ══ G5 的具体出口位置 ══
            sb.AppendLine();
            sb.AppendLine("──── G5 东墙门洞精确位置 ────");
            sb.AppendLine("  扫描 G5 东边界 x=-13 处，看哪个 z 是空的：");
            for (float z = -5f; z <= 10f; z += 0.5f)
            {
                // 用玩家胶囊检测宽度内是否被墙占
                bool wall = Physics.CheckCapsule(
                    new Vector3(-13f, 0.1f + radius, z), new Vector3(-13f, 0.1f + radius + sphereOffset, z),
                    radius, ~0, QueryTriggerInteraction.Ignore);
                string mark = wall ? "■" : "□";
                if (!wall || Mathf.Abs(z - Mathf.Round(z)) < 0.01f)
                    sb.AppendLine($"    z={z,5:F1}  {mark}");
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(Application.dataPath), "../Reports/Ch4_RouteExact.txt")),
                text, new UTF8Encoding(false));
        }

        private static void CheckPath(StringBuilder sb, string name, List<Vector3> pts,
                                       float radius, float sphereOffset, float footY)
        {
            sb.AppendLine($"  ── {name} ──");
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector3 a = pts[i], b = pts[i + 1];
                Vector3 aTop = new Vector3(a.x, footY + radius + sphereOffset, a.z);
                Vector3 aBot = new Vector3(a.x, footY + radius, a.z);
                Vector3 dir = (b - a).normalized;
                dir.y = 0;
                float dist = Vector3.Distance(new Vector3(a.x, 0, a.z), new Vector3(b.x, 0, b.z));

                bool blocked = Physics.CapsuleCast(aBot, aTop, radius, dir, out var hit, dist,
                                                    ~0, QueryTriggerInteraction.Ignore);
                string hn = blocked && hit.collider != null ? hit.collider.gameObject.name : "";
                sb.AppendLine($"    [{i,2}] ({a.x,6:F1},{a.z,5:F1}) → ({b.x,6:F1},{b.z,5:F1})  {dist,4:F1}m  " +
                              (blocked ? $"❌ '{hn}'" : "✅"));
            }
        }
    }
}