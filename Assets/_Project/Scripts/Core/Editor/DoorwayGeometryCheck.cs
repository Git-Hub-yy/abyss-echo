using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    public static class DoorwayGeometryCheck
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter1_Graybox.unity", OpenSceneMode.Single);

            var rooms = AbyssEcho.LevelGen.ChapterOneLayout.Build();
            sb.AppendLine("=== 门洞与门板位置对照 ===");

            foreach (var id in new[] { "A3_B_Link", "B1", "C2", "C1", "C_EW_Corridor", "ELEV_C_Link" })
            {
                AbyssEcho.LevelGen.RoomDef r = null;
                foreach (var x in rooms) if (x.Id == id) { r = x; break; }
                if (r == null) continue;
                sb.AppendLine();
                sb.AppendLine($"{r.Id}: x[{r.MinX:F2},{r.MaxX:F2}] z[{r.MinZ:F2},{r.MaxZ:F2}]");
                foreach (var d in r.Doorways)
                {
                    bool ns = d.Side == AbyssEcho.LevelGen.WallSide.North || d.Side == AbyssEcho.LevelGen.WallSide.South;
                    float wx, wz;
                    if (ns) { wx = r.Center.x + d.Offset; wz = (d.Side == AbyssEcho.LevelGen.WallSide.North) ? r.MaxZ : r.MinZ; }
                    else { wx = (d.Side == AbyssEcho.LevelGen.WallSide.East) ? r.MaxX : r.MinX; wz = r.Center.z + d.Offset; }
                    sb.AppendLine($"   {d.Side,-6} 中心世界坐标=({wx,7:F2},{wz,7:F2})  宽={d.Width:F2}  高={d.Height:F2}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("=== 结论：门洞的通行方向 ===");
            sb.AppendLine("A3_B_Link 与 B1 之间：墙在 Z 边界 → 通行方向沿 Z");
            sb.AppendLine("C1 与 C2 之间：墙在 X 边界 → 通行方向沿 X");

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports/DoorwayGeometry.txt")), text, new UTF8Encoding(false));
        }
    }
}