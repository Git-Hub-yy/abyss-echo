using System.Collections.Generic;
using UnityEngine;
using static AbyssEcho.LevelGen.WallSide;

namespace AbyssEcho.LevelGen
{
    /// <summary>
    /// 第一章（A 深潜入口 / B 生活区 / C 中央控制室）灰盒布局数据（S3A）。
    ///
    /// ═══════════════════════════ 设计思路 ═══════════════════════════
    ///  所有连接廊都用 <see cref="LayoutHelper.LinkNS"/> / <see cref="LayoutHelper.LinkEW"/> **自动吸附**，
    ///  所有附属房间用 <see cref="LayoutHelper.AttachOutside"/> 自动贴墙。
    ///  人只需要指定"父房间 + 哪一侧 + 沿墙偏移"，坐标边界由代码计算。
    ///
    ///  这是 S3A 第四次重构。前三次手工算坐标分别产生 67 / 19 / 14 个校验错误，
    ///  全部根因是"人算错边界"。改用自动吸附后，边界错误在结构上不可能发生。
    ///
    /// ═══════════════════════════ 空间规划 ═══════════════════════════
    ///  中央主轴（南北）：A0 舱 → A1 走廊 → A2 闸室 → A3 接待室 → B1 餐厅
    ///  B 区（东西向）：B2 宿舍走廊横贯东西，北侧依次为 洗手间 / 5 间宿舍 / 医疗室
    ///  电梯：置于 B 区东端外侧，上行进入 C 区
    ///  C 区（放射状）：C1 主厅 + 四周功能房，最北为通往 D 区的压力闸门
    /// </summary>
    public static class ChapterOneLayout
    {
        public const float H = 3.2f;
        private const float DW = 2.0f;      // 主通道门宽（第一人称通过性：≥1.8m 舒适）
        private const float DOOR_H = 2.3f;  // 门高

        public static List<RoomDef> Build()
        {
            var rooms = new List<RoomDef>();

            // ══════════════════ 区域 A：深潜入口（中央主轴）══════════════════

            var a0 = new RoomDef("A0", "深潜舱", new Vector3(0f, 0f, -41.5f), 2.0f, 2.0f, 2.7f);
            LayoutHelper.AddDoor(a0, North, 0f, 2.0f, 2.4f);
            rooms.Add(a0);

            var a1 = new RoomDef("A1", "入口走廊", new Vector3(0f, 0f, -34.5f), 12f, 3f, H);
            LayoutHelper.AddDoor(a1, South, 0f);
            LayoutHelper.AddDoor(a1, North, 0f);
            rooms.Add(a1);

            var a2 = new RoomDef("A2", "气密闸室", new Vector3(0f, 0f, -24.5f), 6f, 6f, H);
            LayoutHelper.AddDoor(a2, South, 0f);
            LayoutHelper.AddDoor(a2, North, 0f);
            rooms.Add(a2);

            var a3 = new RoomDef("A3", "接待休息室", new Vector3(0f, 0f, -12f), 8f, 6f, H);
            LayoutHelper.AddDoor(a3, South, 0f);
            LayoutHelper.AddDoor(a3, North, 0f);
            rooms.Add(a3);

            // 自动吸附的连接廊（南北向）
            // ⚠️ 不传高度：让 LinkNS 自动取两端房间的较小层高。
            //    曾写死 3.2，导致连接廊比 A0（2.7）高，
            //    门楣从 A0 天花板上伸下来，看起来像"门被封住"。
            rooms.Add(LayoutHelper.LinkNS("A0_A1_Link", "舱门连接廊", a0, a1, 2.0f));
            rooms.Add(LayoutHelper.LinkNS("A1_A2_Link", "气闸前廊", a1, a2));
            rooms.Add(LayoutHelper.LinkNS("A2_A3_Link", "门禁前廊", a2, a3));

            // ══════════════════ 区域 B：生活区 ══════════════════

            var b1 = new RoomDef("B1", "餐厅", new Vector3(0f, 0f, 3f), 12f, 10f, 3.5f);
            LayoutHelper.AddDoor(b1, South, 0f);
            LayoutHelper.AddDoor(b1, North, 0f);
            rooms.Add(b1);

            rooms.Add(LayoutHelper.LinkNS("A3_B_Link", "压力走廊", a3, b1));

            // B2 宿舍走廊：东西向长走廊，南墙接餐厅通道
            var b2 = new RoomDef("B2", "宿舍走廊", new Vector3(0f, 0f, 10.9f), 28f, 3f, H);
            LayoutHelper.AddDoor(b2, South, 0f);
            rooms.Add(b2);

            rooms.Add(LayoutHelper.LinkNS("B1_B2_Link", "餐厅通道", b1, b2));

            // ── 北侧附属：洗手间 + 5 间宿舍 + 医疗室 ──
            //   ⚠️ 关键（S3A 踩坑）：附属房间必须"层层外推"——前廊贴 B2，主房间再贴前廊。
            //      若主房间也直接贴 B2，会与前廊完全重叠（实测报出 B2_B5_Link 与 B5 重叠）。
            //   B2 半宽 = 14，可用 X ∈ [-14, 14]
            //   布局：洗手间(-12.4) | 宿舍1(-8.5) 宿舍2(-5) 宿舍3(-1.5) 宿舍4(2) 宿舍5(5.5) | 医疗室(11.5)
            //   注：洗手间放在 -12.4 是为了让 B5（宽 4）与宿舍 1（X[-10,-7]）不重叠。

            // 洗手间：前廊贴 B2（两侧开门），B5 再贴前廊
            var b5Link = LayoutHelper.AttachOutside("B2_B5_Link", "洗手间前廊", b2, North,
                                                    1.4f, 1.4f, -12.4f, 3.0f, openBothSides: true);
            rooms.Add(b5Link);
            rooms.Add(LayoutHelper.AttachOutside("B5", "洗手间", b5Link, North, 4f, 4f, 0f, 3.0f));

            // 宿舍 ×5：直接贴在 B2 北墙（门就在墙上，不需要前廊）
            float[] dormOffsets = { -8.5f, -5.0f, -1.5f, 2.0f, 5.5f };
            for (int i = 0; i < dormOffsets.Length; i++)
            {
                rooms.Add(LayoutHelper.AttachOutside($"B4_{i + 1}", $"宿舍 {i + 1}", b2, North,
                                                    3f, 3f, dormOffsets[i], 2.8f));
            }

            // 医疗室：前廊贴 B2（两侧开门），B3 再贴前廊
            var b3Link = LayoutHelper.AttachOutside("B2_B3_Link", "医疗室前廊", b2, North,
                                                    1.4f, 1.4f, 11.5f, H, openBothSides: true);
            rooms.Add(b3Link);
            rooms.Add(LayoutHelper.AttachOutside("B3", "医疗室", b3Link, North, 6f, 8f, 0f, H));

            // B2 北墙门洞：与上面每间附属房的门一一对应
            LayoutHelper.AddDoor(b2, North, -12.4f);                                    // 洗手间前廊
            foreach (float off in dormOffsets) LayoutHelper.AddDoor(b2, North, off);    // 5 间宿舍
            LayoutHelper.AddDoor(b2, North, 11.5f);                                     // 医疗室前廊

            // ══════════════════ 电梯：B 区最东侧，绕开 B2 走廊 ══════════════════
            //   问题：B1 东墙 X=6 引出的竖直通道会穿过 B2 走廊（B2 X ∈ [-14,14]）。
            //   解决：电梯放在 B2 走廊**东端之外**（X > 14），由 B1 东侧一段东西向短廊接过去。

            // 1) B1 东侧的横向短廊（X 6→14.6 之外无法直达，改为从 B1 东墙出发、向北走到 B2 东端）
            //    更简洁的做法：电梯井直接贴在 B2 东墙外侧（B2.MaxX = 14），由 B1 经 B2 到达即可。
            //    这符合《03 布局》中"B2 → 电梯井"的描述。
            var elev = LayoutHelper.AttachOutside("ELEV", "电梯井", b2, East, 3.4f, 3.4f, 0f, 3.4f);
            LayoutHelper.AddDoor(elev, North, 0f);
            rooms.Add(elev);
            LayoutHelper.AddDoor(b2, East, 0f);   // B2 东墙开门 → 电梯井

            // 2) 电梯井北侧的竖向通道（X = elev.Center.x ≈ 15.7，已在 B2 范围之外，不会再穿过走廊）
            var elevLink = new RoomDef("ELEV_C_Link", "电梯通道",
                new Vector3(elev.Center.x, 0f, 0f), 1.4f, 1f, 3.4f);
            LayoutHelper.AddDoor(elevLink, South, 0f);
            LayoutHelper.AddDoor(elevLink, North, 0f);
            rooms.Add(elevLink);

            // ══════════════════ 区域 C：中央控制室 ══════════════════

            // C1 主厅：放在 B 区全部房间之北
            //   B 区最北：医疗室 = b2.MaxZ + 1.4(前廊) + 8 = 21.8
            float c1MinZ = 30f;
            var c1 = new RoomDef("C1", "主厅", new Vector3(0f, 0f, c1MinZ + 5f), 14f, 10f, 4.0f);
            LayoutHelper.AddDoor(c1, South, 0f);
            LayoutHelper.AddDoor(c1, North, 0f);
            LayoutHelper.AddDoor(c1, East, 0f);
            LayoutHelper.AddDoor(c1, West, 0f);
            rooms.Add(c1);

            // ── 电梯通道 → 前廊 → C1，分两段，避免重叠 ──
            //   ⚠️ S3A 踩坑：若前廊横跨电梯通道顶部，两者在 X/Z 上重叠，
            //      电梯通道的西墙变成实墙，玩家被挡在通道内（实测 West_ELEV_C_Link_Full）。
            //   正确做法：
            //      ① 电梯通道：X = elev.Center.x，向上到前廊南墙
            //      ② 前廊：从电梯通道顶端的北面开始，向西延伸到 C1 中心 X
            //      ③ 前廊北墙开门接 C1 南墙

            // 前廊：Z 区间 [c1.MinZ-1.6, c1.MinZ]，X 从 C1 中心向西延伸到电梯通道**外侧**
            //   ⚠️ S3A 踩坑：前廊东墙若正好在电梯通道中心线上（X=15.7），
            //     会在门洞正前方立一面实墙，玩家撞墙无法通过（实测 East_C_EW_Corridor_Full）。
            //   解决：前廊向东多延伸 1m，让东墙落在通道门洞之外。
            //
            //   ⚠️ S44 踩坑（走查发现）：西端同理 —— 前廊原来从 X=0 开始，
            //     而"接 C1"的北门中心也正好在 X=0，等于把门开在墙角上。
            //     结果门洞一半在墙外，室内留下 0.6 米实心墙块挡在门中央，
            //     玩家只能贴着门边挤过去。
            //   解决：前廊向**西**再多延伸 1m，让北门离西墙角留出足够距离。
            float ewMinZ = c1.MinZ - 1.6f;
            float ewEastX = elev.Center.x + 1.0f;   // 东端超出电梯通道中心 1m
            float ewWestX = c1.Center.x - 1.0f;     // ★ S44：西端超出 C1 中心 1m
            var ewCorridor = new RoomDef("C_EW_Corridor", "控制区前廊",
                new Vector3((ewEastX + ewWestX) * 0.5f, 0f, ewMinZ + 0.8f),
                ewEastX - ewWestX, 1.6f, 3.4f);
            LayoutHelper.AddDoor(ewCorridor, South, elev.Center.x - ewCorridor.Center.x); // 接电梯通道
            LayoutHelper.AddDoor(ewCorridor, North, c1.Center.x - ewCorridor.Center.x);   // 接 C1
            rooms.Add(ewCorridor);
            // 电梯通道：从电梯井北墙(elev.MaxZ) 直到前廊南墙(ewMinZ)
            elevLink.Center = new Vector3(elev.Center.x, 0f, (elev.MaxZ + ewMinZ) * 0.5f);
            elevLink.SizeZ = ewMinZ - elev.MaxZ;

            // ── C 区附属房间 ──
            //   ⚠️ 与 B 区同理：先建连接廊贴 C1，附属房间再贴连接廊。
            //      若附属房间直接贴 C1，LinkEW/LinkNS 会算出 0 长度并与之重叠（实测报错）。

            // 东侧：监控廊贴 C1（两侧开门），C2 贴监控廊
            var c2Link = LayoutHelper.AttachOutside("C1_C2_Link", "监控廊", c1, East,
                                                    1.4f, 1.6f, 0f, H, openBothSides: true);
            rooms.Add(c2Link);
            rooms.Add(LayoutHelper.AttachOutside("C2", "监控室", c2Link, East, 8f, 6f, 0f, H));

            // 西侧：服务器廊贴 C1（两侧开门），C3 贴服务器廊
            var c3Link = LayoutHelper.AttachOutside("C1_C3_Link", "服务器廊", c1, West,
                                                    1.4f, 1.6f, 0f, H, openBothSides: true);
            rooms.Add(c3Link);
            var c3 = LayoutHelper.AttachOutside("C3", "服务器机房", c3Link, West, 8f, 8f, 0f, H);
            LayoutHelper.AddDoor(c3, North, 0f);
            rooms.Add(c3);

            // C3 北侧：广播前廊贴 C3（两侧开门），C5 贴前廊
            var c5Link = LayoutHelper.AttachOutside("C3_C5_Link", "广播前廊", c3, North,
                                                    1.4f, 1.4f, 0f, H, openBothSides: true);
            rooms.Add(c5Link);
            rooms.Add(LayoutHelper.AttachOutside("C5", "广播室", c5Link, North, 5f, 4f, 0f, H));

            // C3 北墙需要开一个与前廊对接的门
            LayoutHelper.AddDoor(c3, North, 0f);

            // C1 北侧：压力闸门（第一章终点）
            rooms.Add(LayoutHelper.AttachOutside("C1_D_Gate", "压力闸门", c1, North, 3f, 4f, 0f, 4.0f));

            return rooms;
        }

        public static Vector3 PlayerStart => new Vector3(0f, 0.05f, -41.5f);

        /// <summary>第一章终点：C1 北侧压力闸门前（第五章前不可通过）。</summary>
        public static Vector3 ChapterEndPoint => new Vector3(0f, 0.05f, 39f);
    }
}
