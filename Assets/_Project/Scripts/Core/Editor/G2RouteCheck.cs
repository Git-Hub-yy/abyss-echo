using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class G2RouteCheck {
        public static void Run() {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter4_Graybox.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();

            sb.AppendLine("══════ 玩家出生点到 G5 的完整路径检查 ══════");
            sb.AppendLine("玩家出生点 = (0, 0.05, -2)  [G1 入口腔]");
            sb.AppendLine();

            // G1 → G2
            sb.AppendLine("【G1 → G2 入口甬道】");
            sb.AppendLine("  G1_G2_Link: x[-1.3,1.3] z[-10,-4]");
            bool g1g2 = Dense(sb, "G1→G2", new Vector3(0,0,-2), new Vector3(0,0,-11));

            sb.AppendLine();
            sb.AppendLine("【G2 → G3 消化甬道】");
            sb.AppendLine("  G2_G3_Link: x[-14,-12] z[-12.7,-11.3]");
            bool g2g3 = Dense(sb, "G2中点→G3甬道", new Vector3(0,0,-12), new Vector3(-13,0,-12));
            Dense(sb, "G3甬道→G3", new Vector3(-13,0,-12), new Vector3(-16,0,-12));

            sb.AppendLine();
            sb.AppendLine("【G3 → G5 组织甬道】");
            sb.AppendLine("  G3_G5_Link: x[-20.1,-17.9] z[-7,-5]");
            Dense(sb, "G3内→甬道口", new Vector3(-16,0,-12), new Vector3(-19,0,-6));
            Dense(sb, "甬道→G5", new Vector3(-19,0,-6), new Vector3(-19,0,-2));

            sb.AppendLine();
            sb.AppendLine("══════ G2 环形廊的可通行区域 ══════");
            sb.AppendLine("  G2: x[-12,12] z[-14,-10]");
            sb.AppendLine("  扫描 G2 内 x=-20..12 z=-14..-9");
            for (float z = -14f; z <= -9f; z += 1f) {
                var row = new StringBuilder();
                row.Append($"  z={z,5:F0} ");
                for (float x = -22f; x <= 14f; x += 1.5f) {
                    bool blocked = Physics.CheckCapsule(
                        new Vector3(x,0.35f,z), new Vector3(x,1.45f,z), 0.35f, ~0, QueryTriggerInteraction.Ignore);
                    bool floor = Physics.Raycast(new Vector3(x,2f,z), Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore);
                    if (!floor) row.Append(" ");
                    else row.Append(blocked ? "#" : ".");
                }
                sb.AppendLine(row.ToString());
            }
            sb.AppendLine("  图例: . 可走  # 障碍  (空格) 无地板");
            sb.AppendLine("  X 范围: -22(左) → 14(右)，每 1.5 米一格");

            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\G2RouteCheck.txt", sb.ToString(), new UTF8Encoding(false));
        }

        static bool Dense(StringBuilder sb, string name, Vector3 a, Vector3 b) {
            Vector3 fa = new Vector3(a.x,0,a.z), fb = new Vector3(b.x,0,b.z);
            float dist = Vector3.Distance(fa, fb);
            int steps = Mathf.Max(1, Mathf.CeilToInt(dist/0.15f));
            int bad = 0;
            string firstBad = "";
            for (int i = 0; i <= steps; i++) {
                Vector3 p = Vector3.Lerp(fa, fb, (float)i/steps);
                bool floor = Physics.Raycast(new Vector3(p.x,2f,p.z), Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore);
                bool blocked = false; string who = "";
                if (floor) {
                    foreach (var h in Physics.OverlapCapsule(new Vector3(p.x,0.35f,p.z), new Vector3(p.x,1.45f,p.z), 0.35f, ~0, QueryTriggerInteraction.Ignore)) {
                        if (h == null || h.isTrigger) continue;
                        string n = h.gameObject.name;
                        if (n.Contains("Floor")) continue;
                        if (h.bounds.max.y < 0.2f) continue;
                        blocked = true; who = n; break;
                    }
                }
                if (!floor || blocked) {
                    bad++;
                    if (firstBad == "") firstBad = $"({p.x:F1},{p.z:F1}){(floor ? " 被"+who : " 无地板")}";
                }
            }
            bool ok = bad == 0;
            sb.AppendLine($"  {(ok ? "✅" : "❌")} {name}: {steps+1} 点，不通 {bad}  {(bad>0 ? "首个: "+firstBad : "")}");
            return ok;
        }
    }
}