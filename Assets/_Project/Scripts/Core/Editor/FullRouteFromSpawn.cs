using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S60：从**玩家出生点**到 G7 心瓣的完整路径验证。
    ///
    /// 上一批只验证了 G5 → G7 那一段，
    /// 没有验证 出生点 → G1 → G2 → G3 → G5 这一段。
    /// 玩家反映「没找到去 G7 的门」，很可能卡在前半段。
    ///
    /// 同时输出一张文本俯视地图，标出所有房间和通道。
    /// </summary>
    public static class FullRouteFromSpawn
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter4_Graybox.unity",
                                         OpenSceneMode.Single);
            Physics.SyncTransforms();

            var start = AbyssEcho.LevelGen.ChapterFourLayout.PlayerStart;
            sb.AppendLine("══════ 出生点 → G7 完整路径验证 ══════");
            sb.AppendLine($"出生点 = {start}");
            sb.AppendLine();

            // 完整路线（西侧主线）
            var route = new[]
            {
                ("出生点",            new Vector3(0f, 0, -2f)),
                ("G1 内",             new Vector3(0f, 0, -3.5f)),
                ("G1_G2_Link 入口",   new Vector3(0f, 0, -7f)),
                ("G2 中部",           new Vector3(0f, 0, -12f)),
                ("G2 西行",           new Vector3(-8f, 0, -12f)),
                ("G2_G3_Link 口",     new Vector3(-12.5f, 0, -12f)),
                ("G3 甬道内",         new Vector3(-13.5f, 0, -12f)),
                ("G3 房间",           new Vector3(-16f, 0, -12f)),
                ("G3 北上",           new Vector3(-19f, 0, -10f)),
                ("G3 北边界",         new Vector3(-19f, 0, -6.5f)),
                ("G3_G5_Link",        new Vector3(-19f, 0, -6f)),
                ("G5 南边界",         new Vector3(-19f, 0, -4.5f)),
                ("G5 中心",           new Vector3(-19f, 0, 0f)),
                ("G5 北",             new Vector3(-19f, 0, 3f)),
                ("G5 北门洞",         new Vector3(-19f, 0, 5f)),
                ("过渡区",            new Vector3(-19f, 0, 5.7f)),
                ("过渡区东行",        new Vector3(-16f, 0, 6.0f)),
                ("过渡区东端",        new Vector3(-13.5f, 0, 6.2f)),
                ("进连廊",            new Vector3(-11.8f, 0, 7.2f)),
                ("连廊中线",          new Vector3(-11.0f, 0, 8.0f)),
                ("连廊东门",          new Vector3(-9.5f, 0, 8.0f)),
                ("G7 内",             new Vector3(-7f, 0, 10f)),
                ("G7 深处",           new Vector3(-3f, 0, 12f)),
                ("心瓣前",            new Vector3(0f, 0, 13.5f)),
                ("心瓣",              new Vector3(0f, 0, 16f)),
            };

            int total = 0, bad = 0;
            var badLog = new StringBuilder();

            for (int seg = 0; seg < route.Length - 1; seg++)
            {
                var (nameA, a) = route[seg];
                var (nameB, b) = route[seg + 1];
                float dist = Vector3.Distance(new Vector3(a.x, 0, a.z), new Vector3(b.x, 0, b.z));
                int steps = Mathf.Max(1, Mathf.CeilToInt(dist / 0.12f));
                int segBad = 0;
                string firstBad = "";

                for (int i = 0; i <= steps; i++)
                {
                    Vector3 p = Vector3.Lerp(new Vector3(a.x, 0, a.z), new Vector3(b.x, 0, b.z), (float)i / steps);
                    total++;

                    bool floor = Physics.Raycast(new Vector3(p.x, 2f, p.z), Vector3.down, 4f,
                                                  ~0, QueryTriggerInteraction.Ignore);
                    bool blocked = false;
                    string who = "";
                    if (floor)
                    {
                        foreach (var h in Physics.OverlapCapsule(
                                     new Vector3(p.x, 0.35f, p.z), new Vector3(p.x, 1.45f, p.z),
                                     0.35f, ~0, QueryTriggerInteraction.Ignore))
                        {
                            if (h == null || h.isTrigger) continue;
                            if (h.gameObject.name.Contains("Floor")) continue;
                            if (h.bounds.max.y < 0.2f) continue;
                            blocked = true; who = h.gameObject.name; break;
                        }
                    }

                    if (!floor || blocked)
                    {
                        segBad++; bad++;
                        if (firstBad == "")
                            firstBad = $"({p.x:F1},{p.z:F1}) {(floor ? "被 " + who : "无地板")}";
                    }
                }

                string mark = segBad == 0 ? "✅" : "❌";
                sb.AppendLine($"{mark} {nameA,-16} → {nameB,-16} {dist,5:F1}m  {steps + 1,3} 点" +
                              (segBad > 0 ? $"  ❌{segBad}点  首个: {firstBad}" : ""));
            }

            sb.AppendLine();
            sb.AppendLine($"总采样 {total}，不通 {bad}");
            sb.AppendLine(bad == 0 ? "✅ 全程可走" : $"❌ 有 {bad} 点不通");

            // ── 俯视地图 ──
            sb.AppendLine();
            sb.AppendLine("══════ 第四章俯视地图（·可走 ■障碍 空格无地板）══════");
            sb.AppendLine("     每格 1 米，X: -34 → 34，Z: 46 → -20（上北下南）");
            for (float z = 46f; z >= -20f; z -= 1f)
            {
                var row = new StringBuilder();
                row.Append($"z={z,4:F0} ");
                for (float x = -34f; x <= 34f; x += 1f)
                {
                    bool floor = Physics.Raycast(new Vector3(x, 2f, z), Vector3.down, 4f,
                                                  ~0, QueryTriggerInteraction.Ignore);
                    if (!floor) { row.Append(' '); continue; }
                    bool blocked = Physics.CheckCapsule(
                        new Vector3(x, 0.35f, z), new Vector3(x, 1.45f, z),
                        0.35f, ~0, QueryTriggerInteraction.Ignore);
                    row.Append(blocked ? '■' : '·');
                }
                sb.AppendLine(row.ToString());
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\FullRouteFromSpawn.txt",
                              text, new UTF8Encoding(false));
        }
    }
}