using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>S32：检查第三章道具是否挡住门洞。</summary>
    public static class ChapterFourPropCheck
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity";

        [MenuItem("ABYSS ECHO/工具：检查第四章道具挡路")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var sb = new StringBuilder();
            sb.AppendLine("=== 第四章道具挡路检查 ===");

            var propRoot = GameObject.Find("--- PROPS_CH4 ---");
            if (propRoot == null)
            {
                sb.AppendLine("❌ 找不到 --- PROPS_CH4 ---");
                Finish(sb);
                return;
            }

            var cols = propRoot.GetComponentsInChildren<Collider>(true);
            sb.AppendLine($"道具碰撞体：{cols.Length}");

            var rooms = AbyssEcho.LevelGen.ChapterLayout.GetRooms(
                AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter4);

            int problems = 0;

            foreach (var r in rooms)
            {
                foreach (var d in r.Doorways)
                {
                    Vector3 doorPos;
                    bool nsDoor;
                    switch (d.Side)
                    {
                        case AbyssEcho.LevelGen.WallSide.North:
                            doorPos = new Vector3(r.Center.x + d.Offset, 0f, r.MaxZ); nsDoor = true; break;
                        case AbyssEcho.LevelGen.WallSide.South:
                            doorPos = new Vector3(r.Center.x + d.Offset, 0f, r.MinZ); nsDoor = true; break;
                        case AbyssEcho.LevelGen.WallSide.East:
                            doorPos = new Vector3(r.MaxX, 0f, r.Center.z + d.Offset); nsDoor = false; break;
                        default:
                            doorPos = new Vector3(r.MinX, 0f, r.Center.z + d.Offset); nsDoor = false; break;
                    }

                    var zone = new Bounds(
                        doorPos + Vector3.up * 1f,
                        new Vector3(nsDoor ? d.Width : 3.0f, 2.0f, nsDoor ? 3.0f : d.Width));

                    foreach (var c in cols)
                    {
                        if (c == null) continue;
                        var b = c.bounds;
                        if (b.max.y < 0.15f || b.min.y > 1.9f) continue;
                        if (zone.Intersects(b))
                        {
                            sb.AppendLine($"❌ {r.Id} 的 {d.Side} 门被 {c.gameObject.name} 挡住");
                            sb.AppendLine($"   门={doorPos} 道具={c.transform.position} 尺寸={b.size}");
                            problems++;
                        }
                    }
                }
            }

            sb.AppendLine();
            sb.AppendLine(problems == 0 ? "✅ 所有门洞都没有被道具挡住" : $"❌ 发现 {problems} 处挡路");

            Finish(sb);
        }

        private static void Finish(StringBuilder sb)
        {
            string text = sb.ToString();
            Debug.Log("<b>[Ch3PropCheck]</b>\n" + text);
            try
            {
                string dir = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "ChapterFourPropCheck.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}