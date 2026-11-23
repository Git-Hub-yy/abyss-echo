using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// 精确检查 A0→A1 通道：沿 z 轴逐点列出"玩家胶囊会撞到什么"。
    ///
    /// 与之前工具的区别：这次直接看**每个 z 位置上，玩家胶囊（半径 0.32、高 1.78）
    /// 会与哪些碰撞体重叠**，模拟真实的移动阻挡。
    /// </summary>
    public static class CorridorProbe
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";

        [MenuItem("ABYSS ECHO/工具：探测走廊通行")]
        public static void Probe()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var sb = new StringBuilder();
            sb.AppendLine("=== A0→A1 走廊通行探测 ===");
            sb.AppendLine("玩家胶囊：半径 0.32，高 1.78，中心 y=0.89");
            sb.AppendLine();

            // 收集所有实心 BoxCollider
            var all = Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None);
            var solids = new System.Collections.Generic.List<BoxCollider>();
            foreach (var bc in all)
                if (!bc.isTrigger && bc.gameObject.activeInHierarchy) solids.Add(bc);

            sb.AppendLine($"场景中共 {all.Length} 个 BoxCollider，其中实心 {solids.Count} 个");

            // 沿 z 轴扫描
            sb.AppendLine();
            sb.AppendLine("z 位置    | x=-0.6  x=0  x=+0.6 | 说明");
            sb.AppendLine("----------|---------------------|------------------");

            for (float z = -42.5f; z <= -33f; z += 0.5f)
            {
                var blocked = new bool[3];
                var names = new string[3];
                float[] xs = { -0.6f, 0f, 0.6f };

                for (int i = 0; i < 3; i++)
                {
                    foreach (var bc in solids)
                    {
                        if (IsCapsuleOverlapping(new Vector3(xs[i], 0f, z), 0.32f, 1.78f, bc))
                        {
                            blocked[i] = true;
                            names[i] = bc.gameObject.name;
                            break;
                        }
                    }
                }

                string cell = $"{(blocked[0] ? "X" : ".")}         {(blocked[1] ? "X" : ".")}    {(blocked[2] ? "X" : ".")}";
                string note = "";
                if (blocked[1]) note = "中间被挡: " + names[1];
                else if (!blocked[0] && !blocked[1] && !blocked[2]) note = "全通";
                else note = "部分可通";

                sb.AppendLine($"z={z,7:F1} | {cell} | {note}");
            }

            sb.AppendLine();
            sb.AppendLine("图例：X = 该位置胶囊会被碰撞体阻挡，. = 可通行");

            // 详细列出挡在门洞里的物体
            sb.AppendLine();
            sb.AppendLine("=== 门洞区域（z ∈ [-41, -35]，x ∈ [-1.2, 1.2]）内的实心碰撞体 ===");
            bool any = false;
            foreach (var bc in solids)
            {
                Vector3 c = bc.transform.TransformPoint(bc.center);
                Vector3 s = Vector3.Scale(bc.size, bc.transform.lossyScale);

                bool inZ = c.z + s.z * 0.5f > -41f && c.z - s.z * 0.5f < -35f;
                bool inX = c.x + s.x * 0.5f > -1.2f && c.x - s.x * 0.5f < 1.2f;
                bool blocksY = c.y - s.y * 0.5f < 1.7f;

                if (inZ && inX && blocksY)
                {
                    sb.AppendLine($"  {bc.gameObject.name}");
                    sb.AppendLine($"    中心=({c.x:F2},{c.y:F2},{c.z:F2}) 尺寸=({s.x:F2},{s.y:F2},{s.z:F2})");
                    any = true;
                }
            }
            if (!any) sb.AppendLine("  （无）");

            string text = sb.ToString();
            Debug.Log("<b>[CorridorProbe]</b>\n" + text);

            try
            {
                string dir = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "CorridorProbe.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }

        /// <summary>近似判断：竖直胶囊（中心在 pos + (0, h/2, 0)）是否与 BoxCollider 重叠。</summary>
        private static bool IsCapsuleOverlapping(Vector3 footPos, float radius, float height, BoxCollider bc)
        {
            Vector3 c = bc.transform.TransformPoint(bc.center);
            Vector3 s = Vector3.Scale(bc.size, bc.transform.lossyScale);

            // 胶囊：从 footPos + (0, radius, 0) 到 footPos + (0, height - radius, 0)
            float capBottom = footPos.y + radius;
            float capTop = footPos.y + height - radius;

            float boxBottom = c.y - s.y * 0.5f;
            float boxTop = c.y + s.y * 0.5f;

            // Y 轴重叠
            if (capTop < boxBottom || capBottom > boxTop) return false;

            // XZ 平面：圆与矩形相交
            float closestX = Mathf.Clamp(footPos.x, c.x - s.x * 0.5f, c.x + s.x * 0.5f);
            float closestZ = Mathf.Clamp(footPos.z, c.z - s.z * 0.5f, c.z + s.z * 0.5f);

            float dx = footPos.x - closestX;
            float dz = footPos.z - closestZ;

            return (dx * dx + dz * dz) < radius * radius;
        }
    }
}