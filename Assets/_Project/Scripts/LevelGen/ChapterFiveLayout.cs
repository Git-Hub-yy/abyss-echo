using System.Collections.Generic;
using UnityEngine;
using static AbyssEcho.LevelGen.WallSide;

namespace AbyssEcho.LevelGen
{
    /// <summary>
    /// 第五章「出口」H 区布局 —— **第一版本的终章**。
    ///
    /// ══════════════════════ 叙事定位 ══════════════════════
    ///
    ///  第四章结尾玩家知道了真相：这里不是基地，是某种东西的内部。
    ///
    ///  第五章的问题变成：**你要带走什么？**
    ///
    ///  最后一章要做两件事：
    ///    ① 给一个"像出口"的地方（哪怕它可能不是）
    ///    ② 留下悬念 —— 第一版到此结束，但不把故事讲死
    ///
    /// ══════════════════════ 空间结构 ══════════════════════
    ///
    ///  与前四章不同，H 区是**上行**的：
    ///    前四章一路往下（D → E → F → G）
    ///    第五章第一次开始往上爬 —— 这是"出口"的物理暗示
    ///
    ///              ┌──────────────────────┐
    ///              │  H7 出口舱（终点）     │
    ///              └──────────┬───────────┘
    ///                         │  ↑ 上升
    ///              ┌──────────┴───────────┐
    ///              │  H6 升降井             │
    ///              └──────────┬───────────┘
    ///                         │
    ///      ┌──────────┬───────┴────────┬──────────┐
    ///      │ H4 值班室 │  H3 中转平台    │ H5 仓库  │
    ///      └────┬─────┴───────┬────────┴────┬─────┘
    ///           │             │             │
    ///      ┌────┴─────┐  ┌────┴─────┐  ┌────┴─────┐
    ///      │ H8 旧舱室 │  │ H1 底层  │  │ H9 封存区 │
    ///      └──────────┘  └────┬─────┘  └──────────┘
    ///                         │
    ///                    H2 上行廊
    ///
    /// ══════════════════════ 布局方法 ══════════════════════
    ///
    ///  严格复用前三章验证过的规则：
    ///    ① 只用 LinkNS / LinkEW / AttachOutside
    ///    ② 所有支线中心线对齐，门偏移一致
    ///    ③ AttachOutside 前必须给 parent 开 offset 对应的门
    /// </summary>
    public static class ChapterFiveLayout
    {
        public const float H = 3.8f;
        private const float DOOR_W = 2.6f;

        public static List<RoomDef> Build()
        {
            var rooms = new List<RoomDef>();

            // ══════════════════ H1 底层（起点，从第四章 G7 下来）══════════════════

            var h1 = new RoomDef("H1", "底层平台", new Vector3(0f, 0f, 0f), 10f, 8f, 5f);
            LayoutHelper.AddDoor(h1, North, 0f, DOOR_W, 3.4f);   // → H2
            rooms.Add(h1);

            // ══════════════════ H2 上行廊（主轴，南北向）══════════════════

            var h2 = new RoomDef("H2", "上行廊", new Vector3(0f, 0f, 13f), 5f, 14f, 4f);
            LayoutHelper.AddDoor(h2, South, 0f, DOOR_W, 3.2f);   // → H1
            LayoutHelper.AddDoor(h2, North, 0f, DOOR_W, 3.2f);   // → H3
            LayoutHelper.AddDoor(h2, West, 0f, 2.2f, 3.0f);      // → H8
            LayoutHelper.AddDoor(h2, East, 0f, 2.2f, 3.0f);      // → H9
            rooms.Add(h2);

            rooms.Add(LayoutHelper.LinkNS("H1_H2_Link", "底层甬道", h1, h2, DOOR_W));

            // ══════════════════ H3 中转平台（中枢）══════════════════

            var h3 = new RoomDef("H3", "中转平台", new Vector3(0f, 0f, 29f), 14f, 10f, 4.5f);
            LayoutHelper.AddDoor(h3, South, 0f, DOOR_W, 3.2f);   // → H2
            LayoutHelper.AddDoor(h3, North, 0f, DOOR_W, 3.2f);   // → H6
            LayoutHelper.AddDoor(h3, West, 0f, 2.2f, 3.0f);      // → H4
            LayoutHelper.AddDoor(h3, East, 0f, 2.2f, 3.0f);      // → H5
            rooms.Add(h3);

            rooms.Add(LayoutHelper.LinkNS("H2_H3_Link", "平台甬道", h2, h3, DOOR_W));

            // ══════════════════ H4 值班室（西）══════════════════

            var h4 = new RoomDef("H4", "值班室", new Vector3(-16f, 0f, 29f), 10f, 8f, 3.4f);
            LayoutHelper.AddDoor(h4, East, 0f, 2.2f, 3.0f);
            rooms.Add(h4);

            rooms.Add(LayoutHelper.LinkEW("H3_H4_Link", "值班廊", h4, h3));

            // ══════════════════ H5 仓库（东）══════════════════

            var h5 = new RoomDef("H5", "仓库", new Vector3(16f, 0f, 29f), 10f, 8f, 3.4f);
            LayoutHelper.AddDoor(h5, West, 0f, 2.2f, 3.0f);
            rooms.Add(h5);

            rooms.Add(LayoutHelper.LinkEW("H3_H5_Link", "仓库廊", h3, h5));

            // ══════════════════ H6 升降井（向上）══════════════════

            var h6 = new RoomDef("H6", "升降井", new Vector3(0f, 0f, 40f), 8f, 8f, 5.5f);
            LayoutHelper.AddDoor(h6, South, 0f, DOOR_W, 3.4f);
            LayoutHelper.AddDoor(h6, North, 0f, DOOR_W, 3.4f);   // → H7
            rooms.Add(h6);

            rooms.Add(LayoutHelper.LinkNS("H3_H6_Link", "升降甬道", h3, h6, DOOR_W));

            // ══════════════════ H7 出口舱（终点）══════════════════

            var h7 = new RoomDef("H7", "出口舱", new Vector3(0f, 0f, 54f), 12f, 12f, 9f);
            LayoutHelper.AddDoor(h7, South, 0f, DOOR_W, 3.4f);
            rooms.Add(h7);

            rooms.Add(LayoutHelper.LinkNS("H6_H7_Link", "出口甬道", h6, h7, DOOR_W));

            // ══════════════════ 附属房间 ══════════════════

            // H8 旧舱室（H2 西墙）
            rooms.Add(LayoutHelper.AttachOutside("H8", "旧舱室", h2, West, 8f, 7f, 0f, 3.4f));

            // H9 封存区（H2 东墙）
            rooms.Add(LayoutHelper.AttachOutside("H9", "封存区", h2, East, 8f, 7f, 0f, 3.4f));

            // H10 工具间（H4 北墙）
            LayoutHelper.AddDoor(h4, North, 0f, 2.0f, 3.0f);
            rooms.Add(LayoutHelper.AttachOutside("H10", "工具间", h4, North, 6f, 6f, 0f, 3.2f));

            // H11 医疗舱（H5 北墙）
            LayoutHelper.AddDoor(h5, North, 0f, 2.0f, 3.0f);
            rooms.Add(LayoutHelper.AttachOutside("H11", "医疗舱", h5, North, 6f, 6f, 0f, 3.2f));

            return rooms;
        }

        /// <summary>玩家起点：H1 底层平台。</summary>
        public static Vector3 PlayerStart => new Vector3(0f, 0.05f, -2.0f);

        /// <summary>第五章终点：H7 出口舱中央。</summary>
        public static Vector3 ChapterEndPoint => new Vector3(0f, 0.05f, 54f);
    }
}