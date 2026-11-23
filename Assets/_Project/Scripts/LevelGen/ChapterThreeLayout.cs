using System.Collections.Generic;
using UnityEngine;
using static AbyssEcho.LevelGen.WallSide;

namespace AbyssEcho.LevelGen
{
    /// <summary>
    /// 第三章「镜沼」F 区布局。
    ///
    /// ═══════════════════════ 空间结构 ═══════════════════════
    ///
    ///  与前两章的"纵向长廊"不同，F 区是**横向展开**的。
    ///
    ///  主轴（南北）：F1 天井 → F2 楼梯廊 → F6 会客厅 → F14 甬道 → F15 气闸 → F7 镜沼
    ///  支线（东西）：F2 两侧挂 F3 档案室 / F4 工作间 / F5 起居区 / F9 祭室
    ///  附属：挂在支线房间外墙上（卧房、盥洗、图书馆、储藏）
    ///
    /// ═══════════════════════ 布局方法（重要）═══════════════════════
    ///
    ///  ⚠️ 这一版**只用两种可靠构件**，不再手算坐标：
    ///
    ///    ① LinkNS / LinkEW —— 连接两个房间，**自动贴合两端**，
    ///       门偏移永远是 0（因为连接廊的中心自动对齐）。
    ///    ② AttachOutside —— 贴在某个房间外墙上，门偏移默认 0 且 parent 侧也要有 offset 0 的门。
    ///
    ///  教训：前一版给 AttachOutside 传了 doorOffset=-3/5，
    ///  但它的 parent 侧门在 -3/5、连接廊侧门也在 -3/5，
    ///  而连接廊的中心又偏移了——三重偏移叠加后完全对不上。
    ///
    ///  **正确做法：所有支线房间的中心线必须对齐，门偏移一律为 0。**
    ///
    /// ═══════════════════════ 空间示意 ═══════════════════════
    ///
    ///                          F7 镜沼
    ///                             │
    ///                          F15 气闸
    ///                             │
    ///              F5 ── F2 ── F6 会客厅 ── F8
    ///             / │      │      │
    ///          F10 F11     F1    F13
    ///
    /// </summary>
    public static class ChapterThreeLayout
    {
        public const float H = 3.2f;

        public static List<RoomDef> Build()
        {
            var rooms = new List<RoomDef>();

            // ══════════════════ 主轴（全部用 LinkNS 自动贴合）══════════════════

            // F1 坠落天井（起点）—— 从第二章 E4 观景台掉下来
            var f1 = new RoomDef("F1", "坠落天井", new Vector3(0f, 0f, 0f), 8f, 8f, 6f);
            LayoutHelper.AddDoor(f1, North, 0f, 2.6f, 3.2f);
            LayoutHelper.AddDoor(f1, East, 0f, 2.4f, 3.0f);   // → F16
            LayoutHelper.AddDoor(f1, West, 0f, 2.2f, 2.8f);   // → F13
            rooms.Add(f1);

            // F2 中央楼梯廊（主枢纽）
            var f2 = new RoomDef("F2", "中央楼梯廊", new Vector3(0f, 0f, 16f), 4.5f, 16f, 4f);
            LayoutHelper.AddDoor(f2, South, 0f, 2.6f, 3.2f);
            LayoutHelper.AddDoor(f2, North, 0f, 2.6f, 3.2f);
            LayoutHelper.AddDoor(f2, West, 0f, 2.4f, 3.0f);    // → 西侧支线廊
            LayoutHelper.AddDoor(f2, East, 0f, 2.4f, 3.0f);    // → 东侧支线廊
            rooms.Add(f2);

            rooms.Add(LayoutHelper.LinkNS("F1_F2_Link", "天井通道", f1, f2, 2.6f));

            // F6 会客厅（12 副餐具）
            var f6 = new RoomDef("F6", "会客厅", new Vector3(0f, 0f, 30f), 12f, 9f, 3.5f);
            LayoutHelper.AddDoor(f6, South, 0f, 2.6f, 3.2f);
            LayoutHelper.AddDoor(f6, North, 0f, 2.6f, 3.2f);
            LayoutHelper.AddDoor(f6, West, 0f, 2.0f, 2.8f);    // → F13
            LayoutHelper.AddDoor(f6, East, 0f, 2.2f, 3.0f);    // → F8
            rooms.Add(f6);

            rooms.Add(LayoutHelper.LinkNS("F2_F6_Link", "会客前廊", f2, f6, 2.6f));

            // F15 气闸
            var f15 = new RoomDef("F15", "气闸", new Vector3(0f, 0f, 43f), 4.5f, 6f, 4f);
            LayoutHelper.AddDoor(f15, South, 0f, 2.6f, 3.2f);   // → F14
            LayoutHelper.AddDoor(f15, North, 0f, 2.6f, 3.2f);   // → F7
            rooms.Add(f15);

            // F14：F6 与 F15 之间的观察窗廊
            //   ⚠️ LinkNS 自动贴合，F6 的 North 门 / F15 的 South 门必须已存在
            rooms.Add(LayoutHelper.LinkNS("F14", "观察窗廊", f6, f15, 2.6f));

            // F7 镜沼（终点）
            var f7 = new RoomDef("F7", "镜沼", new Vector3(0f, 0f, 60f), 16f, 14f, 7f);
            LayoutHelper.AddDoor(f7, South, 0f, 2.6f, 3.2f);
            rooms.Add(f7);

            rooms.Add(LayoutHelper.LinkNS("F15_F7_Link", "镜沼甬道", f15, f7, 2.6f));

            // ══════════════════ 西侧支线（F2 西门 → 支线廊 → F3 / F5）══════════════════

            // 西侧支线廊：一条东西向长廊，从 F2 西门出去
            //   ⚠️ 用 LinkEW：west 是 F3 用房，east 是 F2
            var f3 = new RoomDef("F3", "旧档案室", new Vector3(-14f, 0f, 16f), 10f, 8f, 3.2f);
            LayoutHelper.AddDoor(f3, East, 0f, 2.4f, 3.0f);
            LayoutHelper.AddDoor(f3, South, 0f, 2.2f, 3.0f);   // → F10
            rooms.Add(f3);

            rooms.Add(LayoutHelper.LinkEW("F2_F3_Link", "档案廊", f3, f2));

            // F10 卧房 A（贴在 F3 北墙）
            rooms.Add(LayoutHelper.AttachOutside("F10", "卧房 A", f3, South, 5f, 4f, 0f, 2.8f));

            // ══════════════════ 东侧支线 ══════════════════

            var f4 = new RoomDef("F4", "工作间", new Vector3(12f, 0f, 16f), 8f, 6f, 3.2f);
            LayoutHelper.AddDoor(f4, West, 0f, 2.4f, 3.0f);
            rooms.Add(f4);

            rooms.Add(LayoutHelper.LinkEW("F2_F4_Link", "工作间廊", f2, f4));

            // ══════════════════ F5 起居区（挂在 F6 西墙，避开 F3）══════════════════

            var f5 = new RoomDef("F5", "起居区", new Vector3(-13f, 0f, 30f), 10f, 8f, 3.2f);
            LayoutHelper.AddDoor(f5, East, 0f, 2.4f, 3.0f);
            LayoutHelper.AddDoor(f5, North, 0f, 2.2f, 3.0f);   // → F11
            LayoutHelper.AddDoor(f5, South, 0f, 2.2f, 3.0f);   // → F12
            rooms.Add(f5);

            rooms.Add(LayoutHelper.LinkEW("F6_F5_Link", "起居廊", f5, f6));

            rooms.Add(LayoutHelper.AttachOutside("F11", "卧房 B", f5, North, 5f, 4f, 0f, 2.8f));
            rooms.Add(LayoutHelper.AttachOutside("F12", "卧房 C", f5, South, 5f, 4f, 0f, 2.8f));

            // ══════════════════ F9 祭室（挂在 F6 东墙）══════════════════

            var f9 = new RoomDef("F9", "祭室", new Vector3(14f, 0f, 30f), 8f, 8f, 3.6f);
            LayoutHelper.AddDoor(f9, West, 0f, 2.4f, 3.0f);
            LayoutHelper.AddDoor(f9, North, 0f, 2.0f, 3.0f);   // → F17
            rooms.Add(f9);

            rooms.Add(LayoutHelper.LinkEW("F6_F9_Link", "祭室廊", f6, f9));

            rooms.Add(LayoutHelper.AttachOutside("F17", "图书馆", f9, North, 8f, 6f, 0f, 3.2f));

            // F8 储藏间（挂在 F9 东墙）
            //   ⚠️ AttachOutside 需要 F9 东墙有 offset=0 的门
            LayoutHelper.AddDoor(f9, East, 0f, 2.2f, 3.0f);
            rooms.Add(LayoutHelper.AttachOutside("F8", "储藏间", f9, East, 8f, 8f, 0f, 3.2f));

            // ══════════════════ 附属房间 ══════════════════

            // F13 盥洗间（贴在 F6 西墙已不行，改贴在 F5 之外）——
            //   实际上直接用 AttachOutside 从 F6 出去会与 F5 冲突，
            //   所以 F13 改为挂在 F1 东侧，与 F16 并列
            rooms.Add(LayoutHelper.AttachOutside("F16", "天井下层", f1, East, 6f, 6f, 0f, 3.0f));
            rooms.Add(LayoutHelper.AttachOutside("F13", "盥洗间", f1, West, 5f, 4f, 0f, 2.8f));

            return rooms;
        }

        /// <summary>玩家起点：F1 天井底部（从观景台掉下来）。</summary>
        public static Vector3 PlayerStart => new Vector3(0f, 0.05f, -2.5f);

        /// <summary>第三章终点：F7 镜沼中央。</summary>
        public static Vector3 ChapterEndPoint => new Vector3(0f, 0.05f, 60f);
    }
}