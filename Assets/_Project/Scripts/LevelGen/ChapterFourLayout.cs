using System.Collections.Generic;
using UnityEngine;
using static AbyssEcho.LevelGen.WallSide;

namespace AbyssEcho.LevelGen
{
    /// <summary>
    /// 第四章「心腔」G 区布局。
    ///
    /// ══════════════════════ 叙事定位 ══════════════════════
    ///
    ///  第三章结尾（结局 C）露出更深的通道 —— 玩家继续往下。
    ///
    ///  第四章的核心是**认知崩塌**：
    ///    前几章玩家以为自己在"基地"里探索，
    ///    到了 G 区才会发现——这里不是建筑，是**某种东西的内部**。
    ///
    ///    证据：
    ///      · 墙壁在缓慢地收缩、舒张（像呼吸）
    ///      · 地面是软的、有温度
    ///      · 走廊会自己改变（同一个地方走两次，路不一样）
    ///
    /// ══════════════════════ 空间结构 ══════════════════════
    ///
    ///  与前三章的"房间 + 走廊"不同，G 区是**环形 + 放射**：
    ///
    ///               ┌──────────────────────┐
    ///               │   G7 核心腔（终点）    │
    ///               └──────────┬───────────┘
    ///                          │
    ///      ┌──────────┬────────┴────────┬──────────┐
    ///      │ G5 组织层 │                │ G6 神经束 │
    ///      └────┬─────┴────────┬───────┴────┬─────┘
    ///           │              │             │
    ///     ┌─────┴──────┐  ┌────┴─────┐  ┌───┴──────┐
    ///     │ G3 消化囊  │  │ G1 入口  │  │ G4 膜层  │
    ///     └─────┬──────┘  └────┬─────┘  └───┬──────┘
    ///           └──────────────┼─────────────┘
    ///                          │
    ///                    G2 环形廊（主循环）
    ///
    /// ══════════════════════ 布局方法 ══════════════════════
    ///
    ///  严格复用第三章验证过的教训：
    ///    ① 只用 LinkNS / LinkEW / AttachOutside 三种构件
    ///    ② 所有支线中心线对齐，门偏移一律 0
    ///    ③ AttachOutside 必须让 parent 侧也有 offset=0 的门
    /// </summary>
    public static class ChapterFourLayout
    {
        public const float H = 3.6f;
        private const float DOOR_W = 2.6f;

        /// <summary>G5 北墙出门洞的世界 X 中心（G5 房间中心 x=-19）。</summary>
        private const float DOOR_CX_G5 = -19f;

        public static List<RoomDef> Build()
        {
            var rooms = new List<RoomDef>();

            // ══════════════════ G1 入口（从第三章 F7 下来）══════════════════

            var g1 = new RoomDef("G1", "入口腔", new Vector3(0f, 0f, 0f), 8f, 8f, 5f);
            // ⚠️ G1 北侧不放门：那是"从第三章 F7 掉下来"的方向，
            //    这个场景里没有对应房间，开门会导致校验器报"门洞无对接"。
            LayoutHelper.AddDoor(g1, South, 0f, 2.4f, 3.0f);   // → G2 环形廊
            rooms.Add(g1);

            // ══════════════════ G2 环形廊（主循环，东西向长廊）══════════════════

            var g2 = new RoomDef("G2", "环形廊", new Vector3(0f, 0f, -12f), 24f, 4f, 3.4f);
            LayoutHelper.AddDoor(g2, North, 0f, DOOR_W, 3.2f);      // → G1
            LayoutHelper.AddDoor(g2, West, 0f, 2.4f, 3.0f);         // → G3
            LayoutHelper.AddDoor(g2, East, 0f, 2.4f, 3.0f);         // → G4
            rooms.Add(g2);

            rooms.Add(LayoutHelper.LinkNS("G1_G2_Link", "入口甬道", g2, g1, DOOR_W));

            // ══════════════════ G3 消化囊（西侧）══════════════════

            var g3 = new RoomDef("G3", "消化囊", new Vector3(-19f, 0f, -12f), 10f, 10f, 4.5f);
            LayoutHelper.AddDoor(g3, East, 0f, 2.4f, 3.0f);
            LayoutHelper.AddDoor(g3, North, 0f, 2.2f, 3.0f);   // → G5
            rooms.Add(g3);

            rooms.Add(LayoutHelper.LinkEW("G2_G3_Link", "消化甬道", g3, g2));

            // ══════════════════ G4 膜层（东侧）══════════════════

            var g4 = new RoomDef("G4", "膜层", new Vector3(19f, 0f, -12f), 10f, 10f, 4.5f);
            LayoutHelper.AddDoor(g4, West, 0f, 2.4f, 3.0f);
            LayoutHelper.AddDoor(g4, North, 0f, 2.2f, 3.0f);   // → G6
            rooms.Add(g4);

            rooms.Add(LayoutHelper.LinkEW("G2_G4_Link", "膜层甬道", g2, g4));

            // ══════════════════ G5 组织层（G3 北侧）══════════════════

            var g5 = new RoomDef("G5", "组织层", new Vector3(-19f, 0f, 0f), 12f, 10f, 5f);
            // ⚠️ S59 修复（真实走查发现：玩家在 G5 里出不去，找不到通往 G7 的口）
            //
            //   S44 原来写的是：
            //       AddDoor(g5, East, 8f, 2.4f, 3.0f);   // → G7（世界 z=8）
            //
            //   注释说「G5 中心 z=0，而 G5_G7_Link 在 z=8，所以偏移必须是 +8」，
            //   但**没注意到 G5 的房间范围只有 z[-5, 5]，装不下 z=8**。
            //
            //   实际后果（已用玩家胶囊实测确认）：
            //     · 门洞被切在 z[6.8, 9.2] —— 完全在 G5 房间外面
            //     · G5 东墙变成一整条实心 Seg0（z[-5, 6.8]），没有开口
            //     · G5 北墙 North_G5_Full（z[4.9, 5.2]）也是整条封死
            //     · G5 地板止于 z=5，连廊地板从 z=6.9 开始 → 中间 1.9 米是虚空
            //   结果：**G5 四面全封，玩家根本走不出去**，自然"找不到去 G7 的门"。
            //
            //   修复方式（最小改动，不动房间尺寸、不动 G7）：
            //     ① 把本该在 East 墙上的门改到 **North 墙**（世界 z=5），
            //        门洞宽 2.4，落在 G5 房间边界内 → 真正切出开口。
            //     ② 连廊 G5_G7_Link 从 z[7,9] 向北延伸到 z[5,9]（见下方 LinkEW 之后的手动修正），
            //        让连廊地板与 G5 地板在 z=5 处直接相接，不留虚空。
            //
            //   为什么选 North 墙而不是把 G5 加长：
            //     G5 里有 Mimic_G5 怪物(-15, 2)和已有的灯/触发区，
            //     改房间尺寸会牵动它们的相对位置；在北墙开口只多一个洞。
            LayoutHelper.AddDoor(g5, South, 0f, 2.2f, 3.0f);
            LayoutHelper.AddDoor(g5, North, 0f, 2.4f, 3.0f);    // → 连廊（世界 z=5）
            rooms.Add(g5);

            rooms.Add(LayoutHelper.LinkNS("G3_G5_Link", "组织甬道", g3, g5, 2.2f));

            // ══════════════════ G6 神经束（G4 北侧）══════════════════

            var g6 = new RoomDef("G6", "神经束", new Vector3(19f, 0f, 0f), 12f, 10f, 5f);
            LayoutHelper.AddDoor(g6, South, 0f, 2.2f, 3.0f);
            LayoutHelper.AddDoor(g6, West, 8f, 2.4f, 3.0f);    // → G7（世界 z=8）
            rooms.Add(g6);

            rooms.Add(LayoutHelper.LinkNS("G4_G6_Link", "神经甬道", g4, g6, 2.2f));

            // ══════════════════ G7 核心腔（终点）══════════════════

            // ══════════════════ G7 核心腔（终点）══════════════════
            //
            //   注意：G7 同时从西（G5）和东（G6）接入，形成环形闭合。
            //
            // ⚠️ S44 修复（走查发现真实卡关）：
            //
            //   G5 中心 z=0，G6 中心 z=0，而 G7 中心 z=16。
            //   LinkEW 的连接廊 z 取两端中心的中点 = Lerp(0,16,0.5) = 8。
            //   但 G7 的门偏移原来写的是 0 → 世界 z = 16，
            //   而连接廊的门在 z=8 —— **差了 8 米，根本接不上**。
            //
            //   结果：G5→G7 和 G7→G6 两段都不通，玩家走不到核心腔。
            //
            //   修复：G7 的东/西门偏移改为 -8（相对中心 z=16），
            //        世界坐标 = 16 - 8 = 8，与连接廊对齐。
            var g7 = new RoomDef("G7", "核心腔", new Vector3(0f, 0f, 16f), 20f, 18f, 8f);
            LayoutHelper.AddDoor(g7, West, -8f, 2.4f, 3.0f);   // → G5（世界 z=8）
            LayoutHelper.AddDoor(g7, East, -8f, 2.4f, 3.0f);   // → G6（世界 z=8）
            rooms.Add(g7);

            // G5 → G7（东西向）
            var g5g7 = LayoutHelper.LinkEW("G5_G7_Link", "核心西门", g5, g7, 2.0f, 3.4f);
            //
            // ⚠️ S59：LinkEW 把连廊 z 放在两端中心的中点 = (0+16)/2 = 8，进深 2.0 → 覆盖 z[7,9]。
            //   但 G5 的北边界是 z=5，两者之间露出 z[5,7] 的 2 米虚空，
            //   玩家即使走出 G5 北门也会掉下去。
            //
            //   修正：把连廊向南延伸到与过渡区重合。
            //     新范围 z[5, 9]（进深 4 米，中心 z=7）。
            //
            //   门洞配置（关键）：
            //     · 东门（East）→ 对上 G7 西门洞（世界 z=8）
            //         偏移 = 8 - 7 = +1
            //     · 南门（South）→ 对上 G5 北墙的过渡区（世界 x=-19）
            //         偏移 = -19 - 连廊中心 x
            //       **不再设 West 门**：
            //         玩家实际是从 G5 北墙出门、经过渡区、再从连廊**南侧**进入，
            //         西侧是 North_G5_Right 实墙，没有门可对。
            //         之前误设 West 门（世界 (-13,5)）正好压在墙边界上，
            //         被走查校验器判为"门洞被堵"，现改为 South 门。
            float linkCenterX = g5g7.Center.x;
            float linkSizeX = g5g7.SizeX;
            g5g7 = new RoomDef("G5_G7_Link", "核心西门",
                               new Vector3(linkCenterX, 0f, 7f),
                               linkSizeX, 4f, 3.4f);
            float southDoorOffset = DOOR_CX_G5 - linkCenterX;   // 对上 G5 北墙门洞
            g5g7 = LayoutHelper.AddDoor(g5g7, WallSide.South, southDoorOffset, 2.4f, 3.0f);
            g5g7 = LayoutHelper.AddDoor(g5g7, WallSide.East, 1f, 2.4f, 3.0f);    // → G7（世界 z=8）
            rooms.Add(g5g7);

            // G7 → G6（东西向，方向相反）
            var g7g6 = LayoutHelper.LinkEW("G7_G6_Link", "核心东门", g7, g6, 2.0f, 3.4f);
            rooms.Add(g7g6);

            // ══════════════════ 附属房间 ══════════════════

            // G8 淋巴池（G5 西墙）
            LayoutHelper.AddDoor(g5, West, 0f, 2.0f, 3.0f);
            rooms.Add(LayoutHelper.AttachOutside("G8", "淋巴池", g5, West, 6f, 6f, 0f, 3.4f));

            // G9 骨壁（G6 东墙）
            LayoutHelper.AddDoor(g6, East, 0f, 2.0f, 3.0f);
            rooms.Add(LayoutHelper.AttachOutside("G9", "骨壁", g6, East, 6f, 6f, 0f, 3.4f));

            // G10 旧观测点（G2 南墙）
            LayoutHelper.AddDoor(g2, South, -8f, 2.0f, 3.0f);   // 宽度需与 G10 那侧一致
            // ⚠️ S44：doorOffset 必须是 0 —— 门放在 G10 自己中心，
            //   而 G10 中心正好是 G2 南墙 offset -8 的位置，天然对齐。
            //   原来传 doorOffset: -8f 让门落到了世界 -16，完全错位。
            rooms.Add(LayoutHelper.AttachOutside("G10", "旧观测点", g2, South, 6f, 5f, -8f, 3.2f));

            // G11 样本库（G3 西墙）
            LayoutHelper.AddDoor(g3, West, 0f, 2.0f, 3.0f);
            rooms.Add(LayoutHelper.AttachOutside("G11", "样本库", g3, West, 6f, 6f, 0f, 3.2f));

            return rooms;
        }

        /// <summary>玩家起点：G1 入口腔（从第三章 F7 下来）。</summary>
        public static Vector3 PlayerStart => new Vector3(0f, 0.05f, -2.0f);

        /// <summary>第四章终点：G7 核心腔中央。</summary>
        public static Vector3 ChapterEndPoint => new Vector3(0f, 0.05f, 16f);
    }
}
