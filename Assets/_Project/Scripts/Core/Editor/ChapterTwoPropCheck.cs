using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S28：检查第二章道具是否挡住了门洞/通道。
    ///
    /// ⚠️ 第一章踩过的坑：餐桌正好摆在门洞中间，玩家被"悬空桌面"挡住。
    ///   这里对第二章做同样检查：每个门洞前后 1.5 米内，是否有道具挡住玩家高度。
    /// </summary>
    public static class ChapterTwoPropCheck
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity";

        [MenuItem("ABYSS ECHO/工具：检查第二章道具挡路")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var sb = new StringBuilder();
            sb.AppendLine("=== 第二章道具挡路检查 ===");
            sb.AppendLine("检测规则：门洞中心前后 1.5 米、玩家高度 0.1~1.9 米范围内是否有道具");
            sb.AppendLine();

            // 收集所有道具碰撞体（父级在 --- PROPS_CH2 --- 下）
            var propRoot = GameObject.Find("--- PROPS_CH2 ---");
            if (propRoot == null)
            {
                sb.AppendLine("❌ 找不到 --- PROPS_CH2 ---");
                Finish(sb);
                return;
            }

            var propColliders = propRoot.GetComponentsInChildren<Collider>(true);
            sb.AppendLine($"道具碰撞体总数：{propColliders.Length}");
            sb.AppendLine();

            // 房间与门洞
            var rooms = AbyssEcho.LevelGen.ChapterLayout.GetRooms(
                AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter2);

            int problems = 0;

            foreach (var r in rooms)
            {
                foreach (var d in r.Doorways)
                {
                    Vector3 doorPos;
                    Vector3 normal;
                    switch (d.Side)
                    {
                        case AbyssEcho.LevelGen.WallSide.North:
                            doorPos = new Vector3(r.Center.x + d.Offset, 0f, r.MaxZ);
                            normal = Vector3.forward; break;
                        case AbyssEcho.LevelGen.WallSide.South:
                            doorPos = new Vector3(r.Center.x + d.Offset, 0f, r.MinZ);
                            normal = Vector3.forward; break;
                        case AbyssEcho.LevelGen.WallSide.East:
                            doorPos = new Vector3(r.MaxX, 0f, r.Center.z + d.Offset);
                            normal = Vector3.right; break;
                        default:
                            doorPos = new Vector3(r.MinX, 0f, r.Center.z + d.Offset);
                            normal = Vector3.right; break;
                    }

                    // 门洞通行区：宽 d.Width，深 3 米（前后各 1.5）
                    Bounds zone = new Bounds(
                        doorPos + Vector3.up * 1f,
                        new Vector3(
                            (d.Side == AbyssEcho.LevelGen.WallSide.North || d.Side == AbyssEcho.LevelGen.WallSide.South) ? d.Width : 3.0f,
                            2.0f,
                            (d.Side == AbyssEcho.LevelGen.WallSide.North || d.Side == AbyssEcho.LevelGen.WallSide.South) ? 3.0f : d.Width));

                    foreach (var c in propColliders)
                    {
                        if (c == null) continue;
                        var b = c.bounds;
                        // 只关心会挡人的高度
                        if (b.max.y < 0.15f || b.min.y > 1.9f) continue;
                        if (zone.Intersects(b))
                        {
                            sb.AppendLine($"❌ {r.Id} 的 {d.Side} 门被 {c.gameObject.name} 挡住");
                            sb.AppendLine($"   门位置={doorPos} 道具位置={c.transform.position} " +
                                          $"道具尺寸={b.size}");
                            problems++;
                        }
                    }
                }
            }

            sb.AppendLine();
            if (problems == 0)
                sb.AppendLine("✅ 所有门洞都没有被道具挡住");
            else
                sb.AppendLine($"❌ 发现 {problems} 处挡路");

            Finish(sb);
        }

        private static void Finish(StringBuilder sb)
        {
            string text = sb.ToString();
            Debug.Log("<b>[Ch2PropCheck]</b>\n" + text);
            try
            {
                string dir = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "ChapterTwoPropCheck.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}