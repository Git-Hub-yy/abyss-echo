using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace AbyssEcho.LevelGen
{
    /// <summary>
    /// 灰盒布局验证器（S3A）。
    ///
    /// 目的：在生成场景之前，用纯数据检查布局是否存在"玩家走不通"的问题：
    ///  1) 房间之间是否有重叠（重叠会导致墙体互相穿插、看起来像BUG）
    ///  2) 每个门洞是否真的对齐到相邻房间的门洞（错位 = 有门但走不过去）
    ///  3) 从起点能否走到终点（连通性）
    ///
    /// 为什么要在生成场景前做：
    ///  在 Unity 里手工发现"某扇门错位 0.5m"的成本极高（要走 5 分钟才撞上），
    ///  而数据校验是毫秒级。这是 S3A 最重要的一道保险。
    /// </summary>
    public static class LayoutValidator
    {
        public class Result
        {
            public readonly List<string> Errors = new List<string>();
            public readonly List<string> Warnings = new List<string>();
            public readonly List<string> Info = new List<string>();
            public bool Passed => Errors.Count == 0;
        }

        public static Result Validate(List<RoomDef> rooms)
        {
            var r = new Result();
            r.Info.Add($"房间总数：{rooms.Count}");

            // ── 1. 重叠检测 ──
            for (int i = 0; i < rooms.Count; i++)
            {
                for (int j = i + 1; j < rooms.Count; j++)
                {
                    var a = rooms[i];
                    var b = rooms[j];
                    if (Overlaps(a, b))
                        r.Errors.Add($"房间重叠：{a.Id} 与 {b.Id}");
                }
            }

            // ── 2. 门洞对齐检测 ──
            var connections = new Dictionary<string, List<string>>();
            foreach (var room in rooms) connections[room.Id] = new List<string>();

            foreach (var room in rooms)
            {
                foreach (var door in room.Doorways)
                {
                    var partner = FindMatchingDoorway(rooms, room, door, out string partnerId);
                    if (partnerId == null)
                    {
                        r.Errors.Add($"门洞无对接：房间 {room.Id} 的 {door.Side} 墙门洞（偏移 {door.Offset:F1}）找不到相邻房间的门");
                        continue;
                    }
                    if (!connections[room.Id].Contains(partnerId))
                        connections[room.Id].Add(partnerId);
                    if (!connections[partnerId].Contains(room.Id))
                        connections[partnerId].Add(room.Id);
                }
            }

// ── 3. 连通性检测（从当前章节的起点出发）──
            string startId = ChapterLayoutMeta.GetStartRoomId();
            var visited = new HashSet<string> { startId };
            var queue = new Queue<string>();
            queue.Enqueue(startId);
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                if (!connections.TryGetValue(cur, out var neighbors)) continue;
                foreach (var n in neighbors)
                    if (visited.Add(n)) queue.Enqueue(n);
            }

            var unreachable = rooms.Where(x => !visited.Contains(x.Id)).Select(x => x.Id).ToList();
            if (unreachable.Count > 0)
                r.Errors.Add($"从起点 {startId} 无法到达：" + string.Join(", ", unreachable));
            else
                r.Info.Add($"连通性：{startId} 可达全部 {rooms.Count} 个房间 ✅");

            // ── 4. 关键路径检查（按当前章节的必经房间）──
            string[] criticalPath = ChapterLayoutMeta.GetCriticalPath();
            foreach (var id in criticalPath)
                if (!visited.Contains(id))
                    r.Errors.Add($"关键流程房间不可达：{id}");
            // ── 5. 门宽检查（玩家胶囊半径 0.32）──
            foreach (var room in rooms)
                foreach (var door in room.Doorways)
                    if (!LevelSpec.IsDoorWideEnough(door.Width))
                        r.Errors.Add($"门洞过窄：{room.Id} 的 {door.Side} 门宽 {door.Width}（最小 1.5m）");

            // ── 6. 门高检查（S3B 新增：门洞必须高于玩家，且门楣不能太薄）──
            foreach (var room in rooms)
            {
                foreach (var door in room.Doorways)
                {
                    // 实际门洞高度 = min(门定义高度, 层高 - 最小门楣)
                    float effectiveH = Mathf.Min(door.Height, room.Height - LevelSpec.MinLintelHeight);

                    if (effectiveH < LevelSpec.MinDoorHeight)
                        r.Errors.Add($"门洞过矮：{room.Id}(层高 {room.Height}) 的 {door.Side} 门净高 {effectiveH:F2}（最小 {LevelSpec.MinDoorHeight:F2}m，玩家身高 {LevelSpec.PlayerHeight}m）");

                    if (room.Height - door.Height < LevelSpec.MinLintelHeight && room.Height - effectiveH < LevelSpec.MinLintelHeight)
                        r.Warnings.Add($"门楣偏薄：{room.Id} 的 {door.Side} 门楣仅 {room.Height - effectiveH:F2}m（建议 ≥ {LevelSpec.MinLintelHeight}m）");
                }
            }

            return r;
        }

        private static bool Overlaps(RoomDef a, RoomDef b)
        {
            const float eps = 0.01f;
            bool xOverlap = a.MinX < b.MaxX - eps && b.MinX < a.MaxX - eps;
            bool zOverlap = a.MinZ < b.MaxZ - eps && b.MinZ < a.MaxZ - eps;
            return xOverlap && zOverlap;
        }

        /// <summary>
        /// 为房间的某个门洞寻找相邻房间上的对接门洞。
        /// 判定条件：两侧房间在该墙线上紧邻（间隙 ≤ 容差）且门洞中心对齐（偏移 ≤ 容差）。
        /// </summary>
        private static DoorwayDef FindMatchingDoorway(List<RoomDef> rooms, RoomDef room, DoorwayDef door, out string partnerId)
        {
            partnerId = null;
            const float gapTolerance = 0.15f;    // 允许两房间之间最多 15cm 间隙
            const float offsetTolerance = 0.2f;  // 门洞中心允许 20cm 错位

            WallSide opposite = Opposite(door.Side);

            float roomEdge = EdgeCoord(room, door.Side);
            float doorCenterAlong = AlongCoord(room, door.Side) + door.Offset;

            foreach (var other in rooms)
            {
                if (other.Id == room.Id) continue;

                foreach (var od in other.Doorways)
                {
                    if (od.Side != opposite) continue;

                    float otherEdge = EdgeCoord(other, od.Side);
                    if (Mathf.Abs(otherEdge - roomEdge) > gapTolerance) continue;

                    float otherAlong = AlongCoord(other, od.Side) + od.Offset;
                    if (Mathf.Abs(otherAlong - doorCenterAlong) > offsetTolerance) continue;

                    // 还需保证两个房间在垂直于墙的方向上有重叠（横向位置一致）
                    if (!LateralOverlap(room, other, door.Side)) continue;

                    partnerId = other.Id;
                    return od;
                }
            }
            return null;
        }

        private static bool LateralOverlap(RoomDef a, RoomDef b, WallSide side)
        {
            if (side == WallSide.North || side == WallSide.South)
                return a.MinX < b.MaxX && b.MinX < a.MaxX;
            return a.MinZ < b.MaxZ && b.MinZ < a.MaxZ;
        }

        private static float EdgeCoord(RoomDef r, WallSide side) => side switch
        {
            WallSide.North => r.MaxZ,
            WallSide.South => r.MinZ,
            WallSide.East => r.MaxX,
            WallSide.West => r.MinX,
            _ => 0f
        };

        private static float AlongCoord(RoomDef r, WallSide side) => side switch
        {
            WallSide.North => r.Center.x,
            WallSide.South => r.Center.x,
            WallSide.East => r.Center.z,
            WallSide.West => r.Center.z,
            _ => 0f
        };

        private static WallSide Opposite(WallSide s) => s switch
        {
            WallSide.North => WallSide.South,
            WallSide.South => WallSide.North,
            WallSide.East => WallSide.West,
            WallSide.West => WallSide.East,
            _ => s
        };

        public static string Format(Result r)
        {
            var sb = new StringBuilder();
            sb.AppendLine("===== 灰盒布局校验 =====");
            foreach (var i in r.Info) sb.AppendLine("[信息] " + i);
            foreach (var w in r.Warnings) sb.AppendLine("[警告] " + w);
            foreach (var e in r.Errors) sb.AppendLine("[错误] " + e);
            sb.AppendLine(r.Passed ? "结论：✅ 布局校验通过" : $"结论：❌ 发现 {r.Errors.Count} 个错误");
            return sb.ToString();
        }
    }
}
