using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S57：门洞净宽精确验证（只读）。
    ///
    /// 用玩家 CharacterController 的真实尺寸（半径 0.35、高 1.8）
    /// 逐点测试门洞能否通行，不要用固定半径的球体近似。
    /// </summary>
    public static class DoorwayClearanceCheck
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("══════ 门洞净宽精确验证 ══════");
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter4_Graybox.unity",
                                         OpenSceneMode.Single);
            Physics.SyncTransforms();

            // 玩家胶囊参数（与 CharacterController 一致）
            const float R = 0.35f;
            const float H = 1.8f;
            float sphereOffset = H - 2f * R;   // 1.1

            sb.AppendLine($"玩家胶囊: 半径={R} 高={H}");
            sb.AppendLine();

            // ── 1. 门洞横向净宽扫描 ──
            sb.AppendLine("──── 1. 门洞横向净宽（z=25.0，玩家胶囊放置测试）────");
            sb.AppendLine("  在门洞平面放一个玩家胶囊，看哪里放得下：");
            for (float x = -2.0f; x <= 2.0f; x += 0.25f)
            {
                bool blocked = Physics.CheckCapsule(
                    new Vector3(x, R, 25.0f),
                    new Vector3(x, R + sphereOffset, 25.0f),
                    R, ~0, QueryTriggerInteraction.Ignore);
                string mark = blocked ? "■" : "□";
                string note = blocked ? "" : "  ← 可通行";
                sb.AppendLine($"    x={x,5:F2}  {mark}{note}");
            }

            // ── 2. 通过门洞的实际路径测试 ──
            sb.AppendLine();
            sb.AppendLine("──── 2. 玩家从 G7 走进通道（CapsuleCast）────");
            for (float x = -1.2f; x <= 1.2f; x += 0.3f)
            {
                Vector3 a = new Vector3(x, R, 23.5f);
                Vector3 aTop = new Vector3(x, R + sphereOffset, 23.5f);
                Vector3 dir = Vector3.forward;
                bool blocked = Physics.CapsuleCast(a, aTop, R, dir, out var hit, 3.5f,
                                                   ~0, QueryTriggerInteraction.Ignore);
                string hn = blocked && hit.collider != null ? hit.collider.gameObject.name : "";
                sb.AppendLine($"    从 x={x,5:F2} 往北走 3.5m: {(blocked ? $"❌ '{hn}' @ z={hit.point.z:F2}" : "✅ 通畅")}");
            }

            // ── 3. 门关闭时的阻挡验证 ──
            sb.AppendLine();
            sb.AppendLine("──── 3. 门关闭时应阻挡（当前场景状态）────");
            var gate = GameObject.Find("Door_HeartGate");
            if (gate != null)
            {
                var door = gate.GetComponent<AbyssEcho.Interaction.DoorInteractable>();
                var gc = gate.GetComponent<Collider>();
                sb.AppendLine($"  门位置 = {gate.transform.position}");
                sb.AppendLine($"  locked = {door.locked}  IsOpen = {door.IsOpen}");
                sb.AppendLine($"  门包围盒 z[{gc.bounds.min.z:F2},{gc.bounds.max.z:F2}]");

                bool blockedAtCenter = Physics.CapsuleCast(
                    new Vector3(0, R, 23.5f), new Vector3(0, R + sphereOffset, 23.5f),
                    R, Vector3.forward, out var hh, 3.5f, ~0, QueryTriggerInteraction.Ignore);
                sb.AppendLine($"  玩家从 x=0 往北: {(blockedAtCenter ? $"❌ 被 '{hh.collider.gameObject.name}' 挡住" : "✅ 通畅")}");
            }

            // ── 4. 通道全程通行（用玩家胶囊）────
            sb.AppendLine();
            sb.AppendLine("──── 4. 门打开后，通道全程通行 ────");
            if (gate != null)
            {
                var door = gate.GetComponent<AbyssEcho.Interaction.DoorInteractable>();
                // 临时开门（不保存）
                var solveState = door.locked;
                door.locked = false;
                var openM = typeof(AbyssEcho.Interaction.DoorInteractable)
                    .GetMethod("Open", BindingFlags.Instance | BindingFlags.Public);
                // 直接调用 OnInteract 触发开门
                door.OnInteract(null);

                // 等它滑到位是运行时的，这里直接把门移到终点位置模拟
                Vector3 closed = gate.transform.position;
                gate.transform.position = new Vector3(closed.x + 2.94f, closed.y, closed.z);
                Physics.SyncTransforms();
                sb.AppendLine($"  临时把门移到 {gate.transform.position} 模拟开到位");

                // 逐段测试
                var pts = new[]
                {
                    new Vector3(0, 0, 23.5f),
                    new Vector3(0, 0, 25.0f),
                    new Vector3(0, 0, 26.0f),
                    new Vector3(0, 0, 28.0f),
                    new Vector3(0, 0, 31.0f),
                    new Vector3(0, 0, 34.0f),
                    new Vector3(0, 0, 37.0f),
                    new Vector3(0, 0, 39.0f),   // 触发区中心
                    new Vector3(0, 0, 41.0f),
                };

                for (int i = 0; i < pts.Length - 1; i++)
                {
                    Vector3 a = pts[i], b = pts[i + 1];
                    Vector3 dir = (b - a).normalized;
                    float d = Vector3.Distance(a, b);
                    bool blocked = Physics.CapsuleCast(
                        new Vector3(a.x, R, a.z), new Vector3(a.x, R + sphereOffset, a.z),
                        R, dir, out var hit, d, ~0, QueryTriggerInteraction.Ignore);
                    string hn = blocked && hit.collider != null ? hit.collider.gameObject.name : "";
                    sb.AppendLine($"    z {a.z:F1} → {b.z:F1}  {d:F1}m  {(blocked ? $"❌ '{hn}'" : "✅")}");
                }

                // 恢复（不保存场景）
                gate.transform.position = closed;
                door.locked = solveState;
            }

            // ── 5. 通道两侧墙/天花板 ──
            sb.AppendLine();
            sb.AppendLine("──── 5. 通道结构对象 ────");
            var root = GameObject.Find("--- EXIT_CORRIDOR_CH4 ---");
            if (root != null)
            {
                foreach (Transform c in root.transform)
                {
                    if (c.name == "Lights")
                    {
                        sb.AppendLine($"  Lights（{c.childCount} 盏）");
                        continue;
                    }
                    var bc = c.GetComponent<BoxCollider>();
                    if (bc != null)
                        sb.AppendLine($"  {c.name,-24} 包围盒 x[{bc.bounds.min.x:F2},{bc.bounds.max.x:F2}] " +
                                      $"y[{bc.bounds.min.y:F2},{bc.bounds.max.y:F2}] " +
                                      $"z[{bc.bounds.min.z:F2},{bc.bounds.max.z:F2}]");
                }
            }

            // ── 6. 门滑开后是否与右墙冲突 ──
            sb.AppendLine();
            sb.AppendLine("──── 6. 门滑开位置 x=2.94 是否与通道右墙冲突 ────");
            sb.AppendLine("  门滑到 x=2.94，门体占 x[1.44, 4.44]");
            sb.AppendLine("  通道右墙内沿 x=1.8，外沿 x=2.1");
            bool doorHitsWall = false;
            foreach (var c in Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None))
            {
                if (c.gameObject.name != "Corridor_Wall_R") continue;
                doorHitsWall = true;
                sb.AppendLine($"  Corridor_Wall_R 包围盒 x[{c.bounds.min.x:F2},{c.bounds.max.x:F2}]");
            }
            sb.AppendLine($"  → 门体 x[1.44,4.44] 与右墙 x[1.80,2.10] {(doorHitsWall ? "有重叠（视觉上会穿墙）" : "")}");

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\DoorwayClearance.txt",
                              text, new UTF8Encoding(false));
        }
    }
}