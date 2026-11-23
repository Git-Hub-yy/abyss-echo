using System.Collections.Generic;
using UnityEngine;

namespace AbyssEcho.LevelGen
{
    /// <summary>
    /// 布局辅助工具（S3A）。
    ///
    /// 背景：手工推算 30 个房间的坐标极易出错（S3A 前三次构建分别报出 67 / 19 / 14 个错误，
    /// 全部来自"人为算错边界"）。这里提供一组**自动吸附**的构造函数，
    /// 让连接廊和相邻房间由代码计算边界，人只负责指定"相对谁、哪一侧"。
    ///
    /// 全部函数只返回数据，不产生几何，便于 LayoutValidator 做纯数据校验。
    /// </summary>
    public static class LayoutHelper
    {
        /// <summary>
        /// 创建一条"南北向"连接廊，自动贴合 south 房间的北墙与 north 房间的南墙。
        /// </summary>
public static RoomDef LinkNS(string id, string name, RoomDef south, RoomDef north,
                                     float width = 2.6f, float height = -1f)
        {
            // ⚠️ S21 修复：走廊从 2.0 加宽到 2.6。
            //    玩家实测"空间太窄了，过不去"。
            //    2.0 米宽的走廊两侧各有一堵 0.3 米厚的墙，净宽只剩 1.4 米，
            //    而门洞本身就是 2.0 米 —— 走廊和门框一样宽，
            //    第一人称走进去像在挤门缝，非常容易卡住。
            //    加宽到 2.6 米后净宽约 2.0 米，与门洞齐平，走起来顺畅。
            float z0 = south.MaxZ;
            float z1 = north.MinZ;
            float centerZ = (z0 + z1) * 0.5f;
            float length = z1 - z0;
            float centerX = Mathf.Lerp(south.Center.x, north.Center.x, 0.5f);

            // ⚠️ 层高默认取两端房间的较小值。
            //    曾经写死 3.2，导致"连接廊比矮的一端高"——
            //    A0 深潜舱只有 2.7 层高，连接廊却是 3.2，
            //    于是连接廊的门楣（y 2.2~3.2）会从 A0 的天花板里伸出来，
            //    玩家抬头看到一块横板，视觉上像"门被封住了"。
            if (height < 0f) height = Mathf.Min(south.Height, north.Height);

            var room = new RoomDef(id, name, new Vector3(centerX, 0f, centerZ), width, Mathf.Max(0.5f, length), height);
            room.WithDoor(new DoorwayDef(WallSide.South, 0f, 2.0f));
            room.WithDoor(new DoorwayDef(WallSide.North, 0f, 2.0f));
            return room;
        }

        /// <summary>
        /// 创建一条"东西向"连接廊，自动贴合 west 房间的东墙与 east 房间的西墙。
        /// </summary>
        public static RoomDef LinkEW(string id, string name, RoomDef west, RoomDef east,
                                     float depth = 1.4f, float height = 3.2f)
        {
            float x0 = west.MaxX;
            float x1 = east.MinX;
            float centerX = (x0 + x1) * 0.5f;
            float length = x1 - x0;
            float centerZ = Mathf.Lerp(west.Center.z, east.Center.z, 0.5f);

            var room = new RoomDef(id, name, new Vector3(centerX, 0f, centerZ), Mathf.Max(0.5f, length), depth, height);
            room.WithDoor(new DoorwayDef(WallSide.West, 0f, 2.0f));
            room.WithDoor(new DoorwayDef(WallSide.East, 0f, 2.0f));
            return room;
        }

        /// <summary>
        /// 在 parent 的指定墙外侧创建一间房，让它的对面墙紧贴 parent。
        /// offset 为沿墙方向的偏移（相对 parent 中心）。
        ///
        /// ⚠️ openBothSides：作为**中转廊**的房间需要两侧都开门
        ///   （贴 parent 的一侧 + 朝外的另一侧），否则后续再贴在外侧的房间无门可对接。
        ///   这是 S3A 实测踩坑：只开单侧门导致 B5/B3/C2/C3/C5 全部报"门洞无对接"。
        /// </summary>
        /// <param name="doorOffset">
        /// 门相对于 parent 中心的沿墙偏移。
        /// ⚠️ S31 新增：AttachOutside 原来把门写死在偏移 0，
        ///   但 parent 侧的门可能开在任意偏移（例如 F2 的 ±3/±5），
        ///   两边对不上就形成"看起来连通、实际走不过去"的死路。
        ///   默认 0 保持与既有章节完全兼容。
        /// </param>
        public static RoomDef AttachOutside(string id, string name, RoomDef parent, WallSide side,
                                            float sizeX, float sizeZ, float offset, float height = 3.2f,
                                            bool openBothSides = false, float doorOffset = 0f)
        {
            Vector3 center;
            WallSide doorSide;
            WallSide farSide;

            switch (side)
            {
                case WallSide.North:
                    center = new Vector3(parent.Center.x + offset, 0f, parent.MaxZ + sizeZ * 0.5f);
                    doorSide = WallSide.South;
                    farSide = WallSide.North;
                    break;
                case WallSide.South:
                    center = new Vector3(parent.Center.x + offset, 0f, parent.MinZ - sizeZ * 0.5f);
                    doorSide = WallSide.North;
                    farSide = WallSide.South;
                    break;
                case WallSide.East:
                    center = new Vector3(parent.MaxX + sizeX * 0.5f, 0f, parent.Center.z + offset);
                    doorSide = WallSide.West;
                    farSide = WallSide.East;
                    break;
                default: // West
                    center = new Vector3(parent.MinX - sizeX * 0.5f, 0f, parent.Center.z + offset);
                    doorSide = WallSide.East;
                    farSide = WallSide.West;
                    break;
            }

            var room = new RoomDef(id, name, center, sizeX, sizeZ, height);
            room.WithDoor(new DoorwayDef(doorSide, doorOffset, 2.0f));
            if (openBothSides)
                room.WithDoor(new DoorwayDef(farSide, doorOffset, 2.0f));
            return room;
        }

        /// <summary>
        /// 在 parent 的指定墙上开门（自动计算沿墙偏移）。
        /// absoluteOffset: 相对 parent 中心的偏移。
        /// </summary>
        public static RoomDef AddDoor(RoomDef room, WallSide side, float absoluteOffset,
                                      float width = 2.4f, float height = 3.2f)
        {
            // ⚠️ S21：门洞从 2.0 加宽到 2.4，让玩家更容易通过。
            //
            // ⚠️ S44 教训（重要）：
            //
            //   走查发现 C_EW_Corridor 的北门偏移 -8.35 正好压在墙角上，
            //   导致门洞有一半开在墙外，室内留下 0.6 米宽的实心墙块挡在门中央。
            //   玩家只能贴着门边挤过去（实测只有 x=±0.6~0.9 通）。
            //
            //   第一次尝试在这里"自动把偏移夹到墙内"——**结果是错的**：
            //   偏移一改，两边门洞就对不上，校验器直接报"门洞无对接"，
            //   整个场景拒绝生成。
            //
            //   结论：**门洞对接优先于"离墙角太近"。**
            //   正确修法是调整房间几何（把墙延长），而不是在这里偷偷改偏移。
            room.WithDoor(new DoorwayDef(side, absoluteOffset, width, height));
            return room;
        }

        /// <summary>
        /// 计算两房间之间是否在沿墙方向上有重叠（用于判断能否对接）。
        /// </summary>
        public static bool LateralOverlap(RoomDef a, RoomDef b, WallSide side)
        {
            if (side == WallSide.North || side == WallSide.South)
                return a.MinX < b.MaxX && b.MinX < a.MaxX;
            return a.MinZ < b.MaxZ && b.MinZ < a.MaxZ;
        }
    }
}
