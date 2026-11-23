using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// 导出第一章所有房间的精确世界包围盒，用于绘制地图。
    /// 数据直接来自 ChapterOneLayout，不做任何推算。
    /// </summary>
    public static class MapDataExport
    {
        [MenuItem("ABYSS ECHO/工具：导出地图数据")]
        public static void Export()
        {
            var rooms = AbyssEcho.LevelGen.ChapterLayout.GetRooms(AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter1);

            // 输出 CSV：id,name,displayName,minX,maxX,minZ,maxZ,height,centerX,centerZ
            var sb = new StringBuilder();
            sb.AppendLine("id,displayName,minX,maxX,minZ,maxZ,height,centerX,centerZ,doorCount");

            foreach (var r in rooms)
            {
                sb.AppendLine(string.Join(",", new[]
                {
                    r.Id,
                    r.DisplayName,
                    r.MinX.ToString("F2"),
                    r.MaxX.ToString("F2"),
                    r.MinZ.ToString("F2"),
                    r.MaxZ.ToString("F2"),
                    r.Height.ToString("F2"),
                    r.Center.x.ToString("F2"),
                    r.Center.z.ToString("F2"),
                    r.Doorways.Count.ToString()
                }));
            }

            // 门洞：door_room,side,offset,width,height,worldX,worldZ
            sb.AppendLine();
            sb.AppendLine("=== DOORS ===");
            sb.AppendLine("room,side,offset,width,height,worldX,worldZ");

            foreach (var r in rooms)
            {
                foreach (var d in r.Doorways)
                {
                    float wx = r.Center.x + d.Offset;
                    float wz = r.Center.z + d.Offset;
                    switch (d.Side)
                    {
                        case AbyssEcho.LevelGen.WallSide.North:
                            wz = r.MaxZ; break;
                        case AbyssEcho.LevelGen.WallSide.South:
                            wz = r.MinZ; break;
                        case AbyssEcho.LevelGen.WallSide.East:
                            wx = r.MaxX; break;
                        case AbyssEcho.LevelGen.WallSide.West:
                            wx = r.MinX; break;
                    }

                    sb.AppendLine(string.Join(",", new[]
                    {
                        r.Id, d.Side.ToString(),
                        d.Offset.ToString("F2"),
                        d.Width.ToString("F2"),
                        d.Height.ToString("F2"),
                        wx.ToString("F2"),
                        wz.ToString("F2")
                    }));
                }
            }

            string text = sb.ToString();

            try
            {
                string dir = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "MapData.csv"), text, new UTF8Encoding(false));
                Debug.Log("[MapData] 已导出 " + rooms.Count + " 个房间");
            }
            catch (System.Exception e)
            {
                Debug.LogError("[MapData] 导出失败: " + e.Message);
            }
        }
    }
}