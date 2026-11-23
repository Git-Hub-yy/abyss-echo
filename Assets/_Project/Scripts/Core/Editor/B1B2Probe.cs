using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// 探测 B1→B2 通道：找出那块"悬空挡板"到底是什么。
    /// 扫描 B1 北墙门洞附近所有碰撞体，列出它们的实际位置和尺寸。
    /// </summary>
    public static class B1B2Probe
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";

        [MenuItem("ABYSS ECHO/工具：探测 B1→B2 通道")]
        public static void Probe()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var sb = new StringBuilder();
            sb.AppendLine("=== B1→B2 通道探测 ===");

            var rooms = AbyssEcho.LevelGen.ChapterLayout.GetRooms(AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter1);
            var b1 = rooms.Find(r => r.Id == "B1");
            var link = rooms.Find(r => r.Id == "B1_B2_Link");
            var b2 = rooms.Find(r => r.Id == "B2");

            if (b1 != null)
            {
                sb.AppendLine($"B1:   中心=({b1.Center.x:F2},{b1.Center.z:F2}) 尺寸={b1.SizeX}x{b1.SizeZ} 层高={b1.Height:F2}");
                sb.AppendLine($"      z[{b1.MinZ:F2}, {b1.MaxZ:F2}]  x[{b1.MinX:F2}, {b1.MaxX:F2}]");
                foreach (var d in b1.Doorways)
                    sb.AppendLine($"      门: {d.Side} offset={d.Offset} w={d.Width} h={d.Height}");
            }

            if (link != null)
            {
                sb.AppendLine($"LINK: 中心=({link.Center.x:F2},{link.Center.z:F2}) 尺寸={link.SizeX}x{link.SizeZ} 层高={link.Height:F2}");
                sb.AppendLine($"      z[{link.MinZ:F2}, {link.MaxZ:F2}]");
                foreach (var d in link.Doorways)
                    sb.AppendLine($"      门: {d.Side} offset={d.Offset} w={d.Width} h={d.Height}");
            }

            if (b2 != null)
            {
                sb.AppendLine($"B2:   中心=({b2.Center.x:F2},{b2.Center.z:F2}) 尺寸={b2.SizeX}x{b2.SizeZ} 层高={b2.Height:F2}");
                sb.AppendLine($"      z[{b2.MinZ:F2}, {b2.MaxZ:F2}]");
                foreach (var d in b2.Doorways)
                    sb.AppendLine($"      门: {d.Side} offset={d.Offset} w={d.Width} h={d.Height}");
            }

            // ── 扫描所有碰撞体 ──
            float zMin = b1 != null ? b1.MaxZ - 3f : 5f;
            float zMax = b2 != null ? b2.MinZ + 3f : 13f;

            sb.AppendLine();
            sb.AppendLine($"=== 扫描 z ∈ [{zMin:F1}, {zMax:F1}]，x ∈ [-3, 3] 的实心碰撞体 ===");

            var cols = Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None);
            int count = 0;

            foreach (var bc in cols)
            {
                if (bc.isTrigger) continue;

                Vector3 c = bc.transform.TransformPoint(bc.center);
                Vector3 s = Vector3.Scale(bc.size, bc.transform.lossyScale);

                bool inZ = c.z + s.z * 0.5f > zMin && c.z - s.z * 0.5f < zMax;
                bool inX = c.x + s.x * 0.5f > -3f && c.x - s.x * 0.5f < 3f;

                if (inZ && inX)
                {
                    float yLo = c.y - s.y * 0.5f;
                    float yHi = c.y + s.y * 0.5f;

                    // 只关注会挡人的（底部 < 1.8）
                    if (yLo < 1.8f)
                    {
                        string kind = "";
                        if (s.y < 0.5f && s.x > 1.5f) kind = "  ★ 横向薄板（疑似挡板）";
                        else if (s.y > 1.5f) kind = "  墙体";

                        sb.AppendLine($"  {bc.gameObject.name}{kind}");
                        sb.AppendLine($"    中心=({c.x:F2},{c.y:F2},{c.z:F2}) 尺寸=({s.x:F2},{s.y:F2},{s.z:F2})");
                        sb.AppendLine($"    y[{yLo:F2},{yHi:F2}]  z[{c.z - s.z * 0.5f:F2},{c.z + s.z * 0.5f:F2}]");
                        count++;
                    }
                }
            }

            sb.AppendLine();
            sb.AppendLine($"共 {count} 个可能挡人的碰撞体");

            string text = sb.ToString();
            Debug.Log("<b>[B1B2Probe]</b>\n" + text);

            try
            {
                string dir = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "B1B2Probe.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}