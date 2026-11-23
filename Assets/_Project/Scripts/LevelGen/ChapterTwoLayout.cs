using System.Collections.Generic;
using UnityEngine;

namespace AbyssEcho.LevelGen
{
    /// <summary>
    /// 第二章「回声」关卡布局。
    ///
    /// 空间脉络（Z 轴由南向北）：
    ///   D1 闸门过渡舱  →  D2 被水淹的大厅（核心）
    ///     ├ 西：D3 泵房
    ///     └ 东：D4 档案室
    ///   → D5 生活区
    ///     ├ 西：D6 卧房 ×3
    ///     └ 东：D7 供奉间
    ///   → E1 声学实验室
    ///     ├ 西：E2 混响舱
    ///     └ 东：E3 控制室
    ///   → E4 深渊观景台（章节终点）
    ///
    /// ⚠️ 坐标铁律（前一版踩过的坑）：
    ///   两个房间要连通，**墙面必须贴合**（缝隙 ≤ 0.6 米）。
    ///   尺寸 X 的房间，若南边界在 z0，中心必须在 z0 + X/2。
    ///   主房间之间一律插入连接廊，不直接对贴，避免尺寸算错。
    ///
    /// Z 轴排布（尺寸 → 中心 → 范围）：
    ///   D1  4  → 0      → [-2, 2]
    ///   L1  2  → 3      → [2, 4]        （D1→D2）
    ///   D2  14 → 11     → [4, 18]
    ///   L2  2  → 19     → [18, 20]      （D2→D5）
    ///   D5  8  → 24     → [20, 28]
    ///   L3  2  → 29     → [28, 30]      （D5→E1）
    ///   E1  10 → 35     → [30, 40]
    ///   L4  2  → 41     → [40, 42]      （E1→E4）
    ///   E4  6  → 45     → [42, 48]
    ///
    /// ⚠️ 本文件是 S11 骨架：只定义空房间，交互/怪物/谜题在后续阶段加入。
    /// </summary>
    public static class ChapterTwoLayout
    {
        private const float H = 3.0f;

        // ── 方向简写 ──
        private const WallSide North = WallSide.North;
        private const WallSide South = WallSide.South;
        private const WallSide East = WallSide.East;
        private const WallSide West = WallSide.West;

        public static List<RoomDef> Build()
        {
            var rooms = new List<RoomDef>();

            // ══════════ 主通道（自南向北）══════════

            // D1 闸门过渡舱：玩家从第一章进来
            var d1 = new RoomDef("D1", "闸门过渡舱", new Vector3(0f, 0f, 0f), 4f, 4f, 2.8f);
            // ⚠️ 不设南门：第二章是独立场景，D1 南墙只是实墙。
            //    之前设了 South 门，但它通向"不存在的房间"，
            //    布局校验报「门洞无对接」。
            LayoutHelper.AddDoor(d1, North, 0f);   // → L1
            rooms.Add(d1);

            // L1 入口甬道 D1 → D2
            var l1 = new RoomDef("L1", "入口甬道", new Vector3(0f, 0f, 3f), 3f, 2f, 2.6f);
            LayoutHelper.AddDoor(l1, South, 0f);
            LayoutHelper.AddDoor(l1, North, 0f);
            rooms.Add(l1);

            // D2 被水淹的大厅（核心场景）
            var d2 = new RoomDef("D2", "被水淹的大厅", new Vector3(0f, 0f, 11f), 18f, 14f, 4.5f);
            LayoutHelper.AddDoor(d2, South, 0f);
            LayoutHelper.AddDoor(d2, West, 0f);    // → L5 → D3
            LayoutHelper.AddDoor(d2, East, 0f);    // → L8 → D4
            LayoutHelper.AddDoor(d2, North, 0f);   // → L2
            rooms.Add(d2);

            // L2 生活区甬道 D2 → D5
            var l2 = new RoomDef("L2", "生活区甬道", new Vector3(0f, 0f, 19f), 3f, 2f, 2.6f);
            LayoutHelper.AddDoor(l2, South, 0f);
            LayoutHelper.AddDoor(l2, North, 0f);
            rooms.Add(l2);

            // D5 生活区
            var d5 = new RoomDef("D5", "生活区", new Vector3(0f, 0f, 24f), 14f, 8f, 3.4f);
            LayoutHelper.AddDoor(d5, South, 0f);   // → L2
            // ⚠️ 西侧有两个卧房前廊，必须开两个门。
            //    之前只开了一个，导致 L6_2 的门找不到对接、D6_2 不可达。
            //    offset 是相对房间中心的偏移：D5 中心 z=24，
            //    卧房前廊在 z=22 和 z=26 → offset = -2 和 +2
            LayoutHelper.AddDoor(d5, West, -2f);   // → L6_1
            LayoutHelper.AddDoor(d5, West, 2f);    // → L6_2
            LayoutHelper.AddDoor(d5, East, 2f);    // → L9 供奉间
            LayoutHelper.AddDoor(d5, North, 0f);   // → L3
            rooms.Add(d5);

            // L3 实验室甬道 D5 → E1
            var l3 = new RoomDef("L3", "实验室甬道", new Vector3(0f, 0f, 29f), 3f, 2f, 2.6f);
            LayoutHelper.AddDoor(l3, South, 0f);
            LayoutHelper.AddDoor(l3, North, 0f);
            rooms.Add(l3);

            // E1 声学实验室
            var e1 = new RoomDef("E1", "声学实验室", new Vector3(0f, 0f, 35f), 14f, 10f, 5.0f);
            LayoutHelper.AddDoor(e1, South, 0f);   // → L3
            LayoutHelper.AddDoor(e1, West, 0f);    // → L7 → E2
            LayoutHelper.AddDoor(e1, East, 0f);    // → L10 → E3
            LayoutHelper.AddDoor(e1, North, 0f);   // → L4
            rooms.Add(e1);

            // L4 观景甬道 E1 → E4
            var l4 = new RoomDef("L4", "观景甬道", new Vector3(0f, 0f, 41f), 3f, 2f, 2.6f);
            LayoutHelper.AddDoor(l4, South, 0f);
            LayoutHelper.AddDoor(l4, North, 0f);
            rooms.Add(l4);

            // E4 深渊观景台（章节终点）
            var e4 = new RoomDef("E4", "深渊观景台", new Vector3(0f, 0f, 45f), 8f, 6f, 4.2f);
            LayoutHelper.AddDoor(e4, South, 0f);
            rooms.Add(e4);

            // ══════════ 西侧支线 ══════════

            // L5 泵房走廊：D2 西边界 x=-9，走廊 x[-11,-9]，中心 -10
            var d3Link = new RoomDef("L5", "泵房走廊", new Vector3(-10f, 0f, 11f), 2f, 2.4f, H);
            LayoutHelper.AddDoor(d3Link, East, 0f);
            LayoutHelper.AddDoor(d3Link, West, 0f);
            rooms.Add(d3Link);

            // D3 泵房：西边界 -11，尺寸 6 → 中心 -14
            var d3 = new RoomDef("D3", "泵房", new Vector3(-14f, 0f, 11f), 6f, 5f, 3.2f);
            LayoutHelper.AddDoor(d3, East, 0f);
            rooms.Add(d3);

            // D6 卧房 ×2：从 D5 西墙引出
            //   D5 西边界 x = -7，走廊 x[-9,-7] 中心 -8
            //   卧房沿 Z 排布，中心 22 / 26，间距 4.0（> 房间尺寸 3.5，不重叠）
            //   ⚠️ 原设计 3 间间距 2.5 会与尺寸 3.5 冲突导致重叠，改为 2 间
            float[] dormZ = { 22f, 26f };
            for (int i = 0; i < dormZ.Length; i++)
            {
                float cz = dormZ[i];

                var link = new RoomDef($"L6_{i + 1}", $"卧房{i + 1}门口",
                    new Vector3(-8f, 0f, cz), 2f, 1.6f, H);
                LayoutHelper.AddDoor(link, East, 0f);
                LayoutHelper.AddDoor(link, West, 0f);
                rooms.Add(link);

                // 卧房：西边界 -9，尺寸 3.5 → 中心 -10.75
                var dorm = new RoomDef($"D6_{i + 1}", $"卧房{i + 1}",
                    new Vector3(-10.75f, 0f, cz), 3.5f, 3.5f, 2.8f);
                LayoutHelper.AddDoor(dorm, East, 0f);
                rooms.Add(dorm);
            }

            // L7 混响走廊：E1 西边界 x=-7，走廊 x[-9,-7] 中心 -8
            var e2Link = new RoomDef("L7", "混响走廊", new Vector3(-8f, 0f, 35f), 2f, 2.4f, H);
            LayoutHelper.AddDoor(e2Link, East, 0f);
            LayoutHelper.AddDoor(e2Link, West, 0f);
            rooms.Add(e2Link);

            // E2 混响舱：西边界 -9，尺寸 6 → 中心 -12
            var e2 = new RoomDef("E2", "混响舱", new Vector3(-12f, 0f, 35f), 6f, 6f, 3.8f);
            LayoutHelper.AddDoor(e2, East, 0f);
            rooms.Add(e2);

            // ══════════ 东侧支线 ══════════

            // L8 档案走廊：D2 东边界 x=9，走廊 x[9,11] 中心 10
            var d4Link = new RoomDef("L8", "档案走廊", new Vector3(10f, 0f, 11f), 2f, 2.4f, H);
            LayoutHelper.AddDoor(d4Link, West, 0f);
            LayoutHelper.AddDoor(d4Link, East, 0f);
            rooms.Add(d4Link);

            // D4 档案室：东边界 11，尺寸 7 → 中心 14.5
            var d4 = new RoomDef("D4", "档案室", new Vector3(14.5f, 0f, 11f), 7f, 6f, 3.2f);
            LayoutHelper.AddDoor(d4, West, 0f);
            rooms.Add(d4);

            // L9 供奉走廊：D5 东边界 x=7，走廊 x[7,9] 中心 8
            var d7Link = new RoomDef("L9", "供奉走廊", new Vector3(8f, 0f, 26f), 2f, 2.4f, H);
            LayoutHelper.AddDoor(d7Link, West, 0f);
            LayoutHelper.AddDoor(d7Link, East, 0f);
            rooms.Add(d7Link);

            // D7 供奉间：东边界 9，尺寸 6 → 中心 12
            var d7 = new RoomDef("D7", "供奉间", new Vector3(12f, 0f, 26f), 6f, 6f, 3.6f);
            LayoutHelper.AddDoor(d7, West, 0f);
            rooms.Add(d7);

            // L10 控制走廊：E1 东边界 x=7，走廊 x[7,9] 中心 8
            var e3Link = new RoomDef("L10", "控制走廊", new Vector3(8f, 0f, 35f), 2f, 2.4f, H);
            LayoutHelper.AddDoor(e3Link, West, 0f);
            LayoutHelper.AddDoor(e3Link, East, 0f);
            rooms.Add(e3Link);

            // E3 控制室：东边界 9，尺寸 6 → 中心 12
            var e3 = new RoomDef("E3", "控制室", new Vector3(12f, 0f, 35f), 6f, 5f, 3.2f);
            LayoutHelper.AddDoor(e3, West, 0f);
            rooms.Add(e3);

            return rooms;
        }

        // ────────────────────────── 章节属性 ──────────────────────────

        /// <summary>第二章起点：D1 闸门过渡舱中央。</summary>
        public static Vector3 PlayerStart => new Vector3(0f, 0.05f, 0f);

        /// <summary>第二章终点：E4 观景台北端（北边界 z=48，取前方 1.5 米）。</summary>
        public static Vector3 ChapterEndPoint => new Vector3(0f, 0.05f, 46.5f);

        /// <summary>本章启用深水机制（D2 大厅）。</summary>
        public static bool HasDeepWater => true;
    }
}