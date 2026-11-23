using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S3B 区域灯光布置：按房间功能放置应急灯 / 顶灯 / 红色警示灯。
    ///
    /// 设计原则（《06 美术与视觉风格》）：
    ///  - 不在每个房间都点灯，而是"稀疏 + 局部" → 制造明暗对比与未知感
    ///  - 红色应急灯是唯一暖色，作为视觉锚点与危险暗示
    ///  - 部分灯故意损坏（不亮）或闪烁（交给 S5 恐怖系统控制，这里先静态）
    ///
    /// 灯光预算（性能）：
    ///  - 每房间 1~3 个实时点光，总计约 40 个
    ///  - 只让最关键的灯投影（阴影很贵）
    /// </summary>
    public static class RoomLightBuilder
    {
        private const string RootName = "--- ROOM_LIGHTS ---";

        private enum LampType { Ceiling, Emergency, Warning }

        private struct LampDef
        {
            public RoomLike Room;
            public Vector3 Offset;     // 相对房间中心
            public LampType Type;
            public bool Broken;        // 损坏（不亮）
            public bool Shadows;
        }

        /// <summary>用于描述房间的轻量结构（避免依赖 LevelGen 的内部状态）。</summary>
        private struct RoomLike
        {
            public string Id;
            public Vector3 Center;
            public float SizeX;
            public float SizeZ;
            public float Height;
        }

        [MenuItem("ABYSS ECHO/S3B 布置区域灯光")]
        public static void Build()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.name) || !scene.isLoaded)
            {
                Debug.LogError("[RoomLightBuilder] 请先打开 Level_Chapter1_Graybox 场景");
                return;
            }

            // 清理旧灯（幂等重建）
            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(RootName);
            var rooms = CollectRooms();
            int placed = 0, broken = 0, shadowed = 0, skipped = 0;

            foreach (var lamp in BuildLampList(rooms))
            {
                // S60：跳过"找不到房间"的灯具（Center 被标成 NaN），
                //      避免灯堆在原点造成"整章无光"的假象。
                if (float.IsNaN(lamp.Room.Center.x))
                {
                    skipped++;
                    continue;
                }

                var go = new GameObject($"Light_{lamp.Room.Id}_{lamp.Type}");
                go.transform.SetParent(root.transform, false);
                go.transform.position = lamp.Room.Center + lamp.Offset;

                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.shadows = lamp.Shadows ? LightShadows.Soft : LightShadows.None;
                l.range = lamp.Type == LampType.Ceiling ? 18f : 12f;
                l.intensity = 0f;
                l.enabled = false;

                switch (lamp.Type)
                {
                    case LampType.Ceiling:
                        l.color = new Color(0.72f, 0.84f, 1.0f);   // 冷白
                        l.intensity = lamp.Broken ? 0f : 9.0f;   // S5: 环境光压低后，点光需更强才够亮
                        break;
                    case LampType.Emergency:
                        l.color = new Color(1.0f, 0.22f, 0.12f);   // 应急红
                        l.intensity = lamp.Broken ? 0f : 6.0f;
                        break;
                    case LampType.Warning:
                        l.color = new Color(1.0f, 0.45f, 0.1f);    // 琥珀警示
                        l.intensity = lamp.Broken ? 0f : 5.0f;
                        break;
                }

                if (lamp.Broken)
                {
                    l.enabled = false;
                    broken++;
                }
                else
                {
                    l.enabled = true;
                    placed++;
                    if (lamp.Shadows) shadowed++;
                }
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);

            Debug.Log($"[RoomLightBuilder] 灯光布置完成：房间 {rooms.Count}，点亮 {placed}，损坏 {broken}，投影灯 {shadowed}，跳过(无此房间) {skipped}");
        }

        // ────────────────────────── 灯光方案 ──────────────────────────

        private static IEnumerable<LampDef> BuildLampList(Dictionary<string, RoomLike> rooms)
        {
            // ── A 区：深潜入口（最暗，玩家刚来，安全感低）──
            yield return Make(rooms, "A0", new Vector3(0f, 1.9f, 0f), LampType.Ceiling, false);
            yield return Make(rooms, "A0", new Vector3(0f, 1.2f, -0.8f), LampType.Warning, false);

            yield return Make(rooms, "A1", new Vector3(-3f, 2.8f, 0f), LampType.Ceiling, false);
            yield return Make(rooms, "A1", new Vector3(3f, 2.8f, 0f), LampType.Ceiling, true);   // 损坏：走廊半暗，恐怖
            yield return Make(rooms, "A1", new Vector3(0f, 2.2f, 1.2f), LampType.Emergency, false);

            // A2 气密闸室：全黑，只有门缝红光
            yield return Make(rooms, "A2", new Vector3(0f, 2.2f, 2.6f), LampType.Emergency, false);
            yield return Make(rooms, "A2", new Vector3(0f, 1.0f, -2.6f), LampType.Emergency, true);

            yield return Make(rooms, "A3", new Vector3(0f, 3.0f, 0f), LampType.Ceiling, false, true);
            yield return Make(rooms, "A3", new Vector3(-3f, 1.8f, 2f), LampType.Warning, false);

            yield return Make(rooms, "A0_A1_Link", new Vector3(0f, 2.0f, 0f), LampType.Ceiling, true);
            yield return Make(rooms, "A1_A2_Link", new Vector3(0f, 2.8f, 0f), LampType.Ceiling, true);
            yield return Make(rooms, "A2_A3_Link", new Vector3(0f, 2.8f, 0f), LampType.Ceiling, false);
            yield return Make(rooms, "A3_B_Link", new Vector3(0f, 2.8f, 1.5f), LampType.Emergency, false);

            // ── B 区：生活区（有生活感，比 A 区亮一些）──
            yield return Make(rooms, "B1", new Vector3(-3f, 3.1f, 2f), LampType.Ceiling, false, true);
            yield return Make(rooms, "B1", new Vector3(3f, 3.1f, 2f), LampType.Ceiling, false);
            yield return Make(rooms, "B1", new Vector3(0f, 3.1f, -3f), LampType.Ceiling, true);

            yield return Make(rooms, "B2", new Vector3(-10f, 2.8f, 0f), LampType.Ceiling, false);
            yield return Make(rooms, "B2", new Vector3(0f, 2.8f, 0f), LampType.Ceiling, false);
            yield return Make(rooms, "B2", new Vector3(10f, 2.8f, 0f), LampType.Ceiling, true);
            yield return Make(rooms, "B2", new Vector3(-5f, 2.2f, 1.2f), LampType.Emergency, false);

            for (int i = 1; i <= 5; i++)
                yield return Make(rooms, $"B4_{i}", new Vector3(0f, 2.5f, 0f), LampType.Ceiling, i == 4 || i == 2);

            yield return Make(rooms, "B5", new Vector3(0f, 2.6f, 0f), LampType.Ceiling, false);
            yield return Make(rooms, "B3", new Vector3(0f, 2.8f, 1.5f), LampType.Ceiling, false, true);
            yield return Make(rooms, "B3", new Vector3(0f, 1.4f, -2.5f), LampType.Warning, false);

            yield return Make(rooms, "B1_B2_Link", new Vector3(0f, 2.8f, 0f), LampType.Ceiling, false);
            yield return Make(rooms, "B2_B3_Link", new Vector3(0f, 2.8f, 0f), LampType.Ceiling, true);
            yield return Make(rooms, "B2_B5_Link", new Vector3(0f, 2.6f, 0f), LampType.Ceiling, true);

            yield return Make(rooms, "ELEV", new Vector3(0f, 2.6f, 0f), LampType.Warning, false);
            yield return Make(rooms, "ELEV_C_Link", new Vector3(0f, 2.8f, 4f), LampType.Ceiling, true);
            yield return Make(rooms, "ELEV_C_Link", new Vector3(0f, 2.8f, -4f), LampType.Emergency, false);

            // ── C 区：中央控制室（主厅高挑，灯更多）──
            yield return Make(rooms, "C1", new Vector3(-4f, 3.6f, 0f), LampType.Ceiling, false, true);
            yield return Make(rooms, "C1", new Vector3(4f, 3.6f, 0f), LampType.Ceiling, false, true);
            yield return Make(rooms, "C1", new Vector3(0f, 3.6f, -3f), LampType.Ceiling, false);
            yield return Make(rooms, "C1", new Vector3(0f, 1.6f, 4f), LampType.Emergency, false);

            yield return Make(rooms, "C2", new Vector3(0f, 2.6f, 0f), LampType.Ceiling, false, true);
            yield return Make(rooms, "C2", new Vector3(0f, 1.6f, 2f), LampType.Warning, false);

            yield return Make(rooms, "C3", new Vector3(-2f, 2.8f, 0f), LampType.Ceiling, false);
            yield return Make(rooms, "C3", new Vector3(2f, 2.8f, 0f), LampType.Ceiling, true);

            yield return Make(rooms, "C5", new Vector3(0f, 2.6f, 0f), LampType.Warning, false);

            yield return Make(rooms, "C1_C2_Link", new Vector3(0f, 2.8f, 0f), LampType.Ceiling, true);
            yield return Make(rooms, "C1_C3_Link", new Vector3(0f, 2.8f, 0f), LampType.Ceiling, false);
            yield return Make(rooms, "C3_C5_Link", new Vector3(0f, 2.8f, 0f), LampType.Ceiling, true);
            yield return Make(rooms, "C_EW_Corridor", new Vector3(0f, 2.8f, 0f), LampType.Ceiling, false);
            yield return Make(rooms, "C1_D_Gate", new Vector3(0f, 3.4f, 0f), LampType.Emergency, false, true);

// ── 第二章 D/E 区：更深处，灯更少、更冷 ──
            //   第二章整体比第一章暗一档：只有关键房间有主灯，
            //   其余靠应急灯和警示灯，强化「被淹没的旧设施」的衰败感。
            yield return Make(rooms, "D1", new Vector3(0f, 2.4f, 0f), LampType.Emergency, false);

            yield return Make(rooms, "L1", new Vector3(0f, 2.2f, 0f), LampType.Ceiling, true);

            // D2 大厅：最高挑，但只有两盏主灯（大面积黑暗）
            yield return Make(rooms, "D2", new Vector3(-5f, 4.0f, 0f), LampType.Ceiling, false);
            yield return Make(rooms, "D2", new Vector3(5f, 4.0f, 0f), LampType.Ceiling, true);
            yield return Make(rooms, "D2", new Vector3(0f, 1.6f, -5f), LampType.Emergency, false);
            yield return Make(rooms, "D2", new Vector3(0f, 1.6f, 5f), LampType.Emergency, true);

            yield return Make(rooms, "L5", new Vector3(0f, 2.4f, 0f), LampType.Ceiling, true);
            yield return Make(rooms, "D3", new Vector3(0f, 2.6f, 0f), LampType.Warning, false);

            yield return Make(rooms, "L8", new Vector3(0f, 2.4f, 0f), LampType.Ceiling, true);
            yield return Make(rooms, "D4", new Vector3(0f, 2.6f, 1f), LampType.Ceiling, false);
            yield return Make(rooms, "D4", new Vector3(0f, 1.6f, -2f), LampType.Warning, false);

            yield return Make(rooms, "L2", new Vector3(0f, 2.2f, 0f), LampType.Emergency, false);

            // D5 生活区：有人住过，灯相对完整
            yield return Make(rooms, "D5", new Vector3(-3f, 2.9f, 0f), LampType.Ceiling, false);
            yield return Make(rooms, "D5", new Vector3(3f, 2.9f, 0f), LampType.Ceiling, false);
            yield return Make(rooms, "D5", new Vector3(0f, 1.5f, 3f), LampType.Emergency, true);

            // D6 卧房：每间一盏，其中一间坏掉
            yield return Make(rooms, "L6_1", new Vector3(0f, 2.4f, 0f), LampType.Ceiling, true);
            yield return Make(rooms, "D6_1", new Vector3(0f, 2.2f, 0f), LampType.Ceiling, false);
            yield return Make(rooms, "L6_2", new Vector3(0f, 2.4f, 0f), LampType.Ceiling, true);
            yield return Make(rooms, "D6_2", new Vector3(0f, 2.2f, 0f), LampType.Ceiling, true);

            yield return Make(rooms, "L9", new Vector3(0f, 2.4f, 0f), LampType.Emergency, true);
            yield return Make(rooms, "D7", new Vector3(0f, 2.8f, 0f), LampType.Warning, false);

            yield return Make(rooms, "L3", new Vector3(0f, 2.2f, 0f), LampType.Ceiling, true);

            // E1 声学实验室：最重要的一间，灯最多
            yield return Make(rooms, "E1", new Vector3(-4f, 4.2f, 0f), LampType.Ceiling, false);
            yield return Make(rooms, "E1", new Vector3(4f, 4.2f, 0f), LampType.Ceiling, false);
            yield return Make(rooms, "E1", new Vector3(0f, 2.0f, -4f), LampType.Emergency, true);
            yield return Make(rooms, "E1", new Vector3(0f, 2.0f, 4f), LampType.Emergency, false);

            yield return Make(rooms, "L7", new Vector3(0f, 2.4f, 0f), LampType.Ceiling, true);
            yield return Make(rooms, "E2", new Vector3(0f, 2.8f, 0f), LampType.Warning, false);

            yield return Make(rooms, "L10", new Vector3(0f, 2.4f, 0f), LampType.Ceiling, true);
            yield return Make(rooms, "E3", new Vector3(0f, 2.6f, 0f), LampType.Ceiling, false);

            yield return Make(rooms, "L4", new Vector3(0f, 2.2f, 0f), LampType.Emergency, false);

            // E4 观景台：只有警示灯，面向深渊
            yield return Make(rooms, "E4", new Vector3(0f, 3.4f, 0f), LampType.Warning, false);
            yield return Make(rooms, "E4", new Vector3(0f, 1.6f, -2f), LampType.Emergency, true);

            // ══════════════════ S60：第四章 G 区 ══════════════════
            //
            // ⚠️ 为什么必须补：
            //   这张灯具表原来只有 A0~A3 / B1~B5 / C1~C5 / D1~D7 / E1~E4 / L1~L10，
            //   **一个 G 区都没有**。
            //   G 区（第四章）调用 RoomLightBuilder.Build() 时，
            //   Make(rooms, "G5", ...) 查不到键 → Room.Center 退化成 zero
            //   → 全部灯堆在原点 (0,0,0)，只有 G1 附近有光。
            //
            //   实测后果：G2 环形廊、G3 消化囊、G5 组织层、G7 核心腔
            //   **一格光都没有**，玩家在纯黑里走，根本看不见门洞在哪。
            //   （玩家反馈"找不到通往 G7 的门"根因就在这里）
            //
            // 风格：G 区是"某种东西的内部"，用暗红/琥珀，比前几章更压抑。
            // 密度：每房间 2~4 盏，重点照亮**各出口门洞附近**，让玩家能看见路。

            // G1 入口腔：刚掉下来的地方
            yield return Make(rooms, "G1", new Vector3(0f, 3.0f, 0f), LampType.Ceiling, false);
            yield return Make(rooms, "G1", new Vector3(0f, 1.8f, -2.5f), LampType.Warning, false);

            // G1_G2_Link 入口甬道
            yield return Make(rooms, "G1_G2_Link", new Vector3(0f, 2.4f, -1f), LampType.Emergency, false);

            // G2 环形廊：长条形，3 盏均匀照亮（玩家要在这里找方向）
            yield return Make(rooms, "G2", new Vector3(-7f, 2.6f, 0f), LampType.Ceiling, false);
            yield return Make(rooms, "G2", new Vector3(0f, 2.6f, 0f), LampType.Warning, false);
            yield return Make(rooms, "G2", new Vector3(7f, 2.6f, 0f), LampType.Ceiling, false);

            // G2_G3_Link 消化甬道
            yield return Make(rooms, "G2_G3_Link", new Vector3(0f, 2.4f, 0f), LampType.Emergency, false);

            // G3 消化囊：北侧靠近去 G5 的甬道口，重点照亮
            yield return Make(rooms, "G3", new Vector3(0f, 3.2f, 0f), LampType.Ceiling, false);
            yield return Make(rooms, "G3", new Vector3(0f, 2.6f, 3.5f), LampType.Warning, false);
            yield return Make(rooms, "G3", new Vector3(-3f, 2.4f, -3f), LampType.Emergency, false);

            // G3_G5_Link 组织甬道
            yield return Make(rooms, "G3_G5_Link", new Vector3(0f, 2.6f, 0f), LampType.Emergency, false);

            // G5 组织层：**关键**——北墙有通往 G7 的门洞，必须在门洞前照亮
            yield return Make(rooms, "G5", new Vector3(0f, 3.4f, 0f), LampType.Ceiling, false);
            yield return Make(rooms, "G5", new Vector3(0f, 3.4f, -3f), LampType.Warning, false);
            yield return Make(rooms, "G5", new Vector3(0f, 2.6f, 4f), LampType.Emergency, false);   // 门洞前
            yield return Make(rooms, "G5", new Vector3(-4f, 2.4f, 0f), LampType.Emergency, true);

            // G5_G7_Link 核心西门
            yield return Make(rooms, "G5_G7_Link", new Vector3(0f, 2.6f, 0f), LampType.Warning, false);

            // G7 核心腔：大房间，北侧门洞前也要照亮
            yield return Make(rooms, "G7", new Vector3(0f, 4.5f, -5f), LampType.Ceiling, false);
            yield return Make(rooms, "G7", new Vector3(0f, 4.0f, 0f), LampType.Warning, false);
            yield return Make(rooms, "G7", new Vector3(5f, 3.5f, 0f), LampType.Emergency, false);
            yield return Make(rooms, "G7", new Vector3(-5f, 3.5f, 0f), LampType.Emergency, false);
            yield return Make(rooms, "G7", new Vector3(0f, 3.5f, 7.5f), LampType.Emergency, false);   // 北门洞前

            // G4 膜层 / G6 神经束（东侧支线）
            yield return Make(rooms, "G4", new Vector3(0f, 3.2f, 0f), LampType.Ceiling, false);
            yield return Make(rooms, "G4", new Vector3(3f, 2.6f, 3f), LampType.Emergency, false);
            yield return Make(rooms, "G6", new Vector3(0f, 3.4f, 0f), LampType.Ceiling, false);
            yield return Make(rooms, "G6", new Vector3(-3f, 2.6f, 3f), LampType.Emergency, false);
            yield return Make(rooms, "G4_G6_Link", new Vector3(0f, 2.8f, 0f), LampType.Emergency, false);
            yield return Make(rooms, "G7_G6_Link", new Vector3(0f, 2.8f, 0f), LampType.Emergency, false);
        }

        private static LampDef Make(Dictionary<string, RoomLike> rooms, string id, Vector3 offset,
                                    LampType type, bool broken, bool shadows = false)
        {
            if (!rooms.TryGetValue(id, out var room))
            {
                // ⚠️ S60：查不到房间时**必须标记为无效**，否则 Room.Center 变成 (0,0,0)，
                //   灯会堆在原点，看起来像"整个章节没灯"。
                //   之前的写法正是这样把第四章 77 盏灯全堆到 G1 的。
                room = new RoomLike { Id = "", Center = new Vector3(float.NaN, 0f, float.NaN) };
            }
            return new LampDef { Room = room, Offset = offset, Type = type, Broken = broken, Shadows = shadows };
        }

        // ────────────────────────── 从场景读取房间 ──────────────────────────

        /// <summary>
        /// 直接使用关卡布局数据（ChapterOneLayout）获取房间，而不是从场景对象名反推。
        ///
        /// ⚠️ S3B 实测根因：此前从场景对象名 "Room_A1_A2_Link_气闸前廊" 反推，
        ///   按第一个下划线切分会把连接廊误识别成 "A1"，并覆盖真正的 A1 走廊坐标，
        ///   结果灯被放到错误位置（实测 A1 的灯出现在 Z=-30.25 而不是 -34.5），
        ///   表现为灯光几乎照不到任何可见表面。
        /// </summary>
        private static Dictionary<string, RoomLike> CollectRooms()
        {
            var result = new Dictionary<string, RoomLike>();

            foreach (var room in AbyssEcho.LevelGen.ChapterLayout.GetRooms())
            {
                result[room.Id] = new RoomLike
                {
                    Id = room.Id,
                    Center = new Vector3(room.Center.x, 0f, room.Center.z),
                    SizeX = room.SizeX,
                    SizeZ = room.SizeZ,
                    Height = room.Height
                };
            }

            Debug.Log($"[RoomLightBuilder] 从布局数据读取房间 {result.Count} 个");
            return result;
        }
    }
}
