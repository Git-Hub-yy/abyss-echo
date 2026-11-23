using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// 编辑器内检查：列出 A0→A1 通道附近所有碰撞体，
    /// 找出挡在门洞里的那一个。
    /// </summary>
    public static class DoorwayInspector
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";

        [MenuItem("ABYSS ECHO/工具：检查门洞阻挡")]
        public static void Inspect()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var sb = new StringBuilder();
            sb.AppendLine("=== 门洞阻挡检查（编辑器内）===");

            var rooms = AbyssEcho.LevelGen.ChapterLayout.GetRooms(AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter1);
            var a0 = rooms.Find(r => r.Id == "A0");
            var link = rooms.Find(r => r.Id == "A0_A1_Link");
            var a1 = rooms.Find(r => r.Id == "A1");

            if (a0 == null || link == null || a1 == null)
            {
                sb.AppendLine("找不到 A0 / A0_A1_Link / A1");
                Finish(sb);
                return;
            }

            sb.AppendLine($"A0:   z[{a0.MinZ:F2} , {a0.MaxZ:F2}]  层高={a0.Height:F2}");
            sb.AppendLine($"LINK: z[{link.MinZ:F2} , {link.MaxZ:F2}]  层高={link.Height:F2}");
            sb.AppendLine($"A1:   z[{a1.MinZ:F2} , {a1.MaxZ:F2}]  层高={a1.Height:F2}");

            float zMin = a0.MinZ - 1f;
            float zMax = a1.MinZ + 1f;

            sb.AppendLine();
            sb.AppendLine($"=== 搜索 z ∈ [{zMin:F1}, {zMax:F1}]，x ∈ [-2, 2]，y < 2.2 的碰撞体 ===");

            var all = Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None);
            int found = 0;

            foreach (var bc in all)
            {
                Vector3 c = bc.transform.TransformPoint(bc.center);
                Vector3 s = Vector3.Scale(bc.size, bc.transform.lossyScale);

                float zLo = c.z - s.z * 0.5f;
                float zHi = c.z + s.z * 0.5f;
                float yLo = c.y - s.y * 0.5f;
                float yHi = c.y + s.y * 0.5f;
                float xLo = c.x - s.x * 0.5f;
                float xHi = c.x + s.x * 0.5f;

                // 是否在门洞区域
                bool inZ = zHi > zMin && zLo < zMax;
                bool inX = xHi > -2f && xLo < 2f;
                bool blocksPlayer = yLo < 1.9f && yHi > 0.1f;

                if (inZ && inX && blocksPlayer)
                {
                    string shape = "";
                    if (s.x > 1f && s.y < 0.6f) shape = "  ← 横向薄板（像挡板）";
                    else if (s.x > 1f && s.y > 1.5f) shape = "  ← 整面墙";
                    else if (s.x < 0.5f) shape = "  ← 竖向柱";

                    sb.AppendLine($"  {bc.gameObject.name}");
                    sb.AppendLine($"    位置=({c.x:F2}, {c.y:F2}, {c.z:F2})  尺寸=({s.x:F2}, {s.y:F2}, {s.z:F2})" + shape);
                    sb.AppendLine($"    y范围=[{yLo:F2}, {yHi:F2}]  z范围=[{zLo:F2}, {zHi:F2}]  isTrigger={bc.isTrigger}");
                    found++;
                }
            }

            if (found == 0) sb.AppendLine("  （没有碰撞体挡在门洞里）");
            else sb.AppendLine($"\n共找到 {found} 个可能的阻挡物");

            Finish(sb);
        }

        private static void Finish(StringBuilder sb)
        {
            string text = sb.ToString();
            Debug.Log("<b>[DoorwayInspector]</b>\n" + text);
            try
            {
                string dir = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "DoorwayInspect.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}