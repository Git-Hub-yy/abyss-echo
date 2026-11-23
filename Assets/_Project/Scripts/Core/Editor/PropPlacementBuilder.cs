using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S6 场景道具布置器：按房间功能生成家具与杂物。
    ///
    /// 设计原则：
    ///   · 全部程序化生成（Cube/Cylinder），无需外部模型
    ///   · 位置由代码计算，房间改动后重建即自动跟随
    ///   · 幂等：每次先删旧根节点再重建
    ///   · 道具**不挡路**：主要动线（门→门）上不放东西
    ///
    /// 各房间道具：
    ///   A3 接待室   —— 沙发、茶几、告示板
    ///   B1 餐厅     —— 长餐桌 ×3、椅子、餐台、周转箱
    ///   B2 宿舍走廊 —— 储物柜、应急箱、手推车
    ///   B3 医疗室   —— 病床 ×2、药品柜、仪器台
    ///   B5 洗手间   —— 洗手台、隔断、水桶
    ///   C1 主厅     —— 控制台、立柱、长桌
    ///   C2 监控室   —— 显示器墙、操作台、座椅
    ///   C3 服务器机房 —— 服务器机柜 ×6、线缆槽
    ///   C5 广播室   —— 广播台、麦克风架、档案柜
    /// </summary>
    public static class PropPlacementBuilder
    {
        private const string RootName = "--- PROPS ---";
        private const string MatFolder = "Assets/_Project/Art/Materials/Props";

        [MenuItem("ABYSS ECHO/S6 布置场景道具")]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[PropPlacement] 请先打开 Level_Chapter1_Graybox 场景");
                return;
            }

            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(RootName);
            var rooms = CollectRooms();
            int total = 0;

            total += BuildA3(root.transform, rooms);
            total += BuildB1(root.transform, rooms);
            total += BuildB2(root.transform, rooms);
            total += BuildB3(root.transform, rooms);
            total += BuildB5(root.transform, rooms);
            total += BuildC1(root.transform, rooms);
            total += BuildC2(root.transform, rooms);
            total += BuildC3(root.transform, rooms);
            total += BuildC5(root.transform, rooms);

            Debug.Log($"[PropPlacement] 布置完成，共 {total} 件道具");

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        // ────────────────────────── A3 接待休息室 ──────────────────────────
        // 尺寸 8×6，中心 (0,0,-12)，范围 x[-4,4] z[-15,-9]
        private static int BuildA3(Transform parent, Dictionary<string, RoomInfo> rooms)
        {
            if (!rooms.TryGetValue("A3", out var r)) return 0;

            var root = MakeRoomRoot(parent, "A3");
            int n = 0;

            // 沙发（靠西墙）
            n += Sofa(root, r.Center + new Vector3(-2.9f, 0f, 0.6f), 180f);
            // 茶几
            n += Box(root, "茶几", r.Center + new Vector3(-2.6f, 0.42f, -0.9f),
                     new Vector3(1.1f, 0.06f, 0.7f), "Wood");
            n += Box(root, "茶几腿A", r.Center + new Vector3(-3.05f, 0.21f, -1.15f),
                     new Vector3(0.06f, 0.42f, 0.06f), "Metal");
            n += Box(root, "茶几腿B", r.Center + new Vector3(-2.15f, 0.21f, -1.15f),
                     new Vector3(0.06f, 0.42f, 0.06f), "Metal");
            n += Box(root, "茶几腿C", r.Center + new Vector3(-3.05f, 0.21f, -0.65f),
                     new Vector3(0.06f, 0.42f, 0.06f), "Metal");
            n += Box(root, "茶几腿D", r.Center + new Vector3(-2.15f, 0.21f, -0.65f),
                     new Vector3(0.06f, 0.42f, 0.06f), "Metal");

            // 告示板（北墙）
            n += Box(root, "告示板", r.Center + new Vector3(2.2f, 1.6f, r.SizeZ * 0.5f - 0.18f),
                     new Vector3(1.6f, 1.0f, 0.06f), "Board");

            // 角落箱子
            n += Box(root, "储物箱A", r.Center + new Vector3(3.2f, 0.3f, -2.2f),
                     new Vector3(0.7f, 0.6f, 0.7f), "Crate");
            n += Box(root, "储物箱B", r.Center + new Vector3(3.2f, 0.85f, -2.2f),
                     new Vector3(0.55f, 0.5f, 0.55f), "Crate");

            return n;
        }

        // ────────────────────────── B1 餐厅 ──────────────────────────
        // 尺寸 12×10，中心 (0,0,3)，范围 x[-6,6] z[-2,8]
private static int BuildB1(Transform parent, Dictionary<string, RoomInfo> rooms)
        {
            if (!rooms.TryGetValue("B1", out var r)) return 0;

            var root = MakeRoomRoot(parent, "B1");
            int n = 0;

            // ⚠️ S20 修复：餐桌不再压住门洞。
            //
            //   原本 z 相对中心取 {-0.6, 2.4, 5.4}，
            //   配合 B1 中心 z=3 和房间半长 5（z ∈ [-2, 8]），
            //   第三张桌子落在 z=8.4 —— 而北墙在 z=8.00，
            //   桌子直接横在门洞中间（y=0.71~0.79 的桌面正好在腰部），
            //   玩家从 B1 去 B2 时被一张"悬空桌面"挡住。
            //
            //   新规则：
            //     ① 桌子南北两端各让出 ≥2 米（门洞宽度 2 米，留缓冲）
            //     ② 桌子不跨越中轴通道（中间留 1.6 米走道）
            //   于是 B1（z ∈ [-2, 8]）可用区间是 z ∈ [0, 6]，
            //   两张桌子放在 z = 1.0 和 z = 4.2，中间留过道。

            // 两张长餐桌（东西向摆放，桌心偏离中轴，留出中间走道）
            var tableOffset = new[]
            {
                new Vector3(-2.4f, 0f, -2.0f),   // 偏西
                new Vector3( 2.4f, 0f,  1.2f),   // 偏东
            };

            for (int i = 0; i < tableOffset.Length; i++)
            {
                var tpos = r.Center + tableOffset[i];
                n += LongTable(root, tpos, $"餐桌{i + 1}");

                // 每张桌南北各两把椅子
                n += Chair(root, tpos + new Vector3(-1.2f, 0f, -0.85f), 0f);
                n += Chair(root, tpos + new Vector3(1.2f, 0f, -0.85f), 0f);
                n += Chair(root, tpos + new Vector3(-1.2f, 0f, 0.85f), 180f);
                n += Chair(root, tpos + new Vector3(1.2f, 0f, 0.85f), 180f);
            }

            // 餐台（靠东墙，避开门口）
            n += Box(root, "餐台", r.Center + new Vector3(4.6f, 0.5f, -0.5f),
                     new Vector3(0.8f, 1.0f, 3.2f), "Metal");

            // 周转箱堆（靠西墙）
            n += Box(root, "周转箱1", r.Center + new Vector3(-5.0f, 0.25f, -1.0f),
                     new Vector3(0.9f, 0.5f, 0.7f), "Crate");
            n += Box(root, "周转箱2", r.Center + new Vector3(-5.0f, 0.25f, 0.1f),
                     new Vector3(0.9f, 0.5f, 0.7f), "Crate");
            n += Box(root, "周转箱3", r.Center + new Vector3(-5.0f, 0.8f, -0.45f),
                     new Vector3(0.85f, 0.5f, 0.7f), "Crate");

        return n;
        }

        private static int BuildB2(Transform parent, Dictionary<string, RoomInfo> rooms)
        {
            if (!rooms.TryGetValue("B2", out var r)) return 0;

            var root = MakeRoomRoot(parent, "B2");
            int n = 0;

            // 储物柜（贴南墙，均匀分布）
            //
            // ⚠️ S44 修复（走查发现真实卡关）：
            //   B2 南门洞在 x=0（通 B1/B3 连线），原来柜子放在 x=0.5 和 x=4.5，
            //   1.6 米宽的柜体直接压住了门洞 → 玩家过不去。
            //   现在位置改为远离 x=0（门洞中心）±2.5 米以外。
            float[] lockerX = { -12.0f, -8.5f, -5.0f, 5.0f, 8.5f, 12.0f };
            for (int i = 0; i < lockerX.Length; i++)
            {
                // 门洞在 x=0，柜体半宽 0.8 → 至少留 2.5 米间隙
                if (Mathf.Abs(lockerX[i]) < 2.5f) continue;

                n += Box(root, $"储物柜{i + 1}",
                         new Vector3(r.Center.x + lockerX[i], 1.1f, r.Center.z - r.SizeZ * 0.5f + 0.28f),
                         new Vector3(1.6f, 2.2f, 0.5f), "Locker");
            }

            // 应急箱（贴北墙）
            //
            // ⚠️ S44 修复：B2 北门洞在 x=-8.5（通 ELEV），
            //   原来应急箱正好放在 x=-9 → 挡住门洞。改到 ±4 以外的位置。
            n += Box(root, "应急箱", r.Center + new Vector3(-3.0f, 1.5f, r.SizeZ * 0.5f - 0.25f),
                     new Vector3(0.6f, 0.8f, 0.35f), "Red");
            n += Box(root, "应急箱2", r.Center + new Vector3(7f, 1.5f, r.SizeZ * 0.5f - 0.25f),
                     new Vector3(0.6f, 0.8f, 0.35f), "Red");
            // 手推车（走廊中间偏东，不挡主通路）
            n += Box(root, "推车台", r.Center + new Vector3(11f, 0.85f, -0.6f),
                     new Vector3(1.2f, 0.08f, 0.7f), "Metal");
            n += Box(root, "推车架", r.Center + new Vector3(11f, 1.5f, -0.6f),
                     new Vector3(1.2f, 1.2f, 0.06f), "Metal");

            return n;
        }

        // ────────────────────────── B3 医疗室 ──────────────────────────
        // 尺寸 6×8
        private static int BuildB3(Transform parent, Dictionary<string, RoomInfo> rooms)
        {
            if (!rooms.TryGetValue("B3", out var r)) return 0;

            var root = MakeRoomRoot(parent, "B3");
            int n = 0;

            // 两张病床
            n += Bed(root, r.Center + new Vector3(-1.6f, 0f, -1.8f), "病床1");
            n += Bed(root, r.Center + new Vector3(-1.6f, 0f, 0.8f), "病床2");

            // 药品柜（靠东墙）
            n += Box(root, "药品柜", r.Center + new Vector3(r.SizeX * 0.5f - 0.35f, 1.0f, 1.5f),
                     new Vector3(0.6f, 2.0f, 1.4f), "Locker");

            // 仪器台
            n += Box(root, "仪器台", r.Center + new Vector3(1.2f, 0.45f, 2.8f),
                     new Vector3(1.4f, 0.08f, 0.7f), "Metal");
            n += Box(root, "仪器", r.Center + new Vector3(1.2f, 0.75f, 2.8f),
                     new Vector3(0.5f, 0.35f, 0.4f), "Dark");

            return n;
        }

        // ────────────────────────── B5 洗手间 ──────────────────────────
        // 尺寸 4×4
        private static int BuildB5(Transform parent, Dictionary<string, RoomInfo> rooms)
        {
            if (!rooms.TryGetValue("B5", out var r)) return 0;

            var root = MakeRoomRoot(parent, "B5");
            int n = 0;

            // 洗手台（靠北墙）
            n += Box(root, "洗手台", r.Center + new Vector3(0f, 0.85f, r.SizeZ * 0.5f - 0.3f),
                     new Vector3(2.2f, 0.12f, 0.5f), "White");
            n += Box(root, "洗手台支架", r.Center + new Vector3(0f, 0.4f, r.SizeZ * 0.5f - 0.3f),
                     new Vector3(2.0f, 0.8f, 0.4f), "Metal");

            // 隔断
            n += Box(root, "隔断1", r.Center + new Vector3(-1.2f, 1.0f, -0.8f),
                     new Vector3(0.06f, 2.0f, 1.6f), "White");
            n += Box(root, "隔断2", r.Center + new Vector3(1.2f, 1.0f, -0.8f),
                     new Vector3(0.06f, 2.0f, 1.6f), "White");

            // 水桶
            n += Cylinder(root, "水桶", r.Center + new Vector3(-1.4f, 0.22f, 1.2f),
                          new Vector3(0.32f, 0.22f, 0.32f), "Metal");

            return n;
        }

        // ────────────────────────── C1 主厅 ──────────────────────────
        // 尺寸 14×10，层高 4
        private static int BuildC1(Transform parent, Dictionary<string, RoomInfo> rooms)
        {
            if (!rooms.TryGetValue("C1", out var r)) return 0;

            var root = MakeRoomRoot(parent, "C1");
            int n = 0;

            // 四根立柱（对称分布，增强空间感）
            var pillarX = new[] { -4f, 4f };
            var pillarZ = new[] { -2.5f, 2.5f };
            int idx = 1;
            foreach (var px in pillarX)
                foreach (var pz in pillarZ)
                {
                    n += Cylinder(root, $"立柱{idx++}",
                                  r.Center + new Vector3(px, 2f, pz),
                                  new Vector3(0.5f, 2f, 0.5f), "Metal");
                }

            // 中央长桌
            n += Box(root, "主控桌", r.Center + new Vector3(0f, 0.78f, 0f),
                     new Vector3(4.5f, 0.1f, 1.6f), "Metal");
            n += Box(root, "主控桌腿A", r.Center + new Vector3(-2f, 0.39f, -0.6f),
                     new Vector3(0.12f, 0.78f, 0.12f), "Metal");
            n += Box(root, "主控桌腿B", r.Center + new Vector3(2f, 0.39f, -0.6f),
                     new Vector3(0.12f, 0.78f, 0.12f), "Metal");
            n += Box(root, "主控桌腿C", r.Center + new Vector3(-2f, 0.39f, 0.6f),
                     new Vector3(0.12f, 0.78f, 0.12f), "Metal");
            n += Box(root, "主控桌腿D", r.Center + new Vector3(2f, 0.39f, 0.6f),
                     new Vector3(0.12f, 0.78f, 0.12f), "Metal");

            // 桌上的控制面板
            n += Box(root, "控制面板", r.Center + new Vector3(0f, 0.88f, 0f),
                     new Vector3(1.8f, 0.08f, 0.6f), "Screen");

            return n;
        }

        // ────────────────────────── C2 监控室 ──────────────────────────
        // 尺寸 8×6
        private static int BuildC2(Transform parent, Dictionary<string, RoomInfo> rooms)
        {
            if (!rooms.TryGetValue("C2", out var r)) return 0;

            var root = MakeRoomRoot(parent, "C2");
            int n = 0;

            // 显示器墙（贴东墙，2×3 排列）
            for (int row = 0; row < 2; row++)
                for (int col = 0; col < 3; col++)
                {
                    n += Box(root, $"显示器_{row}_{col}",
                             r.Center + new Vector3(r.SizeX * 0.5f - 0.25f,
                                                    1.2f + row * 0.85f,
                                                    -1.5f + col * 1.5f),
                             new Vector3(0.08f, 0.7f, 1.2f), "Screen");
                }

            // 操作台
            n += Box(root, "操作台", r.Center + new Vector3(1.6f, 0.75f, 0f),
                     new Vector3(0.9f, 0.08f, 4.0f), "Metal");
            n += Box(root, "操作台支撑", r.Center + new Vector3(1.6f, 0.37f, 0f),
                     new Vector3(0.85f, 0.74f, 3.8f), "Dark");

            // 座椅 ×2
            n += Chair(root, r.Center + new Vector3(0.6f, 0f, -1.0f), 90f);
            n += Chair(root, r.Center + new Vector3(0.6f, 0f, 1.0f), 90f);

            return n;
        }

        // ────────────────────────── C3 服务器机房 ──────────────────────────
        // 尺寸 8×8
        private static int BuildC3(Transform parent, Dictionary<string, RoomInfo> rooms)
        {
            if (!rooms.TryGetValue("C3", out var r)) return 0;

            var root = MakeRoomRoot(parent, "C3");
            int n = 0;

            // 服务器机柜（两排，各 3 台）
            for (int i = 0; i < 3; i++)
            {
                float z = -2f + i * 2f;
                n += Box(root, $"机柜_左{i + 1}",
                         r.Center + new Vector3(-2.2f, 1.1f, z),
                         new Vector3(0.9f, 2.2f, 0.8f), "Server");
                n += Box(root, $"机柜_右{i + 1}",
                         r.Center + new Vector3(2.2f, 1.1f, z),
                         new Vector3(0.9f, 2.2f, 0.8f), "Server");
            }

            // 线缆槽（天花下方）
            n += Box(root, "线缆槽", r.Center + new Vector3(0f, 2.9f, 0f),
                     new Vector3(6f, 0.12f, 0.3f), "Dark");

            return n;
        }

        // ────────────────────────── C5 广播室 ──────────────────────────
        // 尺寸 5×4
        private static int BuildC5(Transform parent, Dictionary<string, RoomInfo> rooms)
        {
            if (!rooms.TryGetValue("C5", out var r)) return 0;

            var root = MakeRoomRoot(parent, "C5");
            int n = 0;

            // 广播台
            n += Box(root, "广播台", r.Center + new Vector3(-1.4f, 0.75f, 0f),
                     new Vector3(0.9f, 0.08f, 2.2f), "Metal");
            n += Box(root, "广播台支撑", r.Center + new Vector3(-1.4f, 0.37f, 0f),
                     new Vector3(0.85f, 0.74f, 2.0f), "Dark");

            // 麦克风架
            n += Cylinder(root, "麦克风架", r.Center + new Vector3(-1.4f, 0.9f, 0.4f),
                          new Vector3(0.04f, 0.15f, 0.04f), "Dark");
            n += Box(root, "麦克风", r.Center + new Vector3(-1.4f, 1.08f, 0.4f),
                     new Vector3(0.08f, 0.12f, 0.08f), "Dark");

            // 档案柜（靠北墙）
            n += Box(root, "档案柜", r.Center + new Vector3(1.2f, 1.0f, r.SizeZ * 0.5f - 0.3f),
                     new Vector3(1.6f, 2.0f, 0.5f), "Locker");

            return n;
        }

        // ────────────────────────── 家具构件 ──────────────────────────

        private static int LongTable(Transform parent, Vector3 basePos, string name)
        {
            int n = 0;
            n += Box(parent, name, basePos + new Vector3(0f, 0.75f, 0f),
                     new Vector3(3.6f, 0.08f, 1.0f), "Wood");
            // 四条腿
            foreach (var dx in new[] { -1.55f, 1.55f })
                foreach (var dz in new[] { -0.38f, 0.38f })
                    n += Box(parent, name + "_腿", basePos + new Vector3(dx, 0.36f, dz),
                             new Vector3(0.08f, 0.72f, 0.08f), "Metal");
            return n;
        }

        private static int Chair(Transform parent, Vector3 basePos, float yaw)
        {
            var root = new GameObject("椅");
            root.transform.SetParent(parent, false);
            root.transform.position = basePos;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            int n = 0;
            n += Box(root.transform, "椅面", new Vector3(0f, 0.45f, 0f),
                     new Vector3(0.45f, 0.06f, 0.45f), "Wood");
            n += Box(root.transform, "椅背", new Vector3(0f, 0.75f, -0.2f),
                     new Vector3(0.45f, 0.55f, 0.05f), "Wood");
            foreach (var dx in new[] { -0.18f, 0.18f })
                foreach (var dz in new[] { -0.18f, 0.18f })
                    n += Box(root.transform, "椅腿", new Vector3(dx, 0.22f, dz),
                             new Vector3(0.05f, 0.44f, 0.05f), "Metal");
            return n;
        }

        private static int Sofa(Transform parent, Vector3 basePos, float yaw)
        {
            var root = new GameObject("沙发");
            root.transform.SetParent(parent, false);
            root.transform.position = basePos;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            int n = 0;
            n += Box(root.transform, "坐垫", new Vector3(0f, 0.35f, 0f),
                     new Vector3(2.0f, 0.35f, 0.8f), "Fabric");
            n += Box(root.transform, "靠背", new Vector3(0f, 0.72f, -0.33f),
                     new Vector3(2.0f, 0.75f, 0.18f), "Fabric");
            n += Box(root.transform, "扶手左", new Vector3(-0.95f, 0.55f, 0f),
                     new Vector3(0.16f, 0.45f, 0.8f), "Fabric");
            n += Box(root.transform, "扶手右", new Vector3(0.95f, 0.55f, 0f),
                     new Vector3(0.16f, 0.45f, 0.8f), "Fabric");
            return n;
        }

        private static int Bed(Transform parent, Vector3 basePos, string name)
        {
            int n = 0;
            n += Box(parent, name + "_床架", basePos + new Vector3(0f, 0.3f, 0f),
                     new Vector3(0.95f, 0.12f, 2.0f), "Metal");
            n += Box(parent, name + "_床垫", basePos + new Vector3(0f, 0.42f, 0f),
                     new Vector3(0.9f, 0.14f, 1.9f), "White");
            n += Box(parent, name + "_床头", basePos + new Vector3(0f, 0.6f, -1.0f),
                     new Vector3(0.95f, 0.6f, 0.08f), "Metal");
            foreach (var dx in new[] { -0.4f, 0.4f })
                foreach (var dz in new[] { -0.9f, 0.9f })
                    n += Box(parent, name + "_腿", basePos + new Vector3(dx, 0.12f, dz),
                             new Vector3(0.06f, 0.24f, 0.06f), "Metal");
            return n;
        }

        // ────────────────────────── 基础构件 ──────────────────────────

        private static Transform MakeRoomRoot(Transform parent, string roomId)
        {
            var go = new GameObject("Props_" + roomId);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static int Box(Transform parent, string name, Vector3 pos, Vector3 size, string matKey)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = GetMaterial(matKey);

            // 家具保持碰撞（可以挡路）但不参与导航烘焙的代价由 Agent 半径承担
            return 1;
        }

        private static int Cylinder(Transform parent, string name, Vector3 pos, Vector3 size, string matKey)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = GetMaterial(matKey);
            return 1;
        }

        // ────────────────────────── 材质 ──────────────────────────

        private static readonly Dictionary<string, Material> _matCache = new Dictionary<string, Material>();

        private static Material GetMaterial(string key)
        {
            if (_matCache.TryGetValue(key, out var cached) && cached != null) return cached;

            EnsureFolder(MatFolder);
            string path = $"{MatFolder}/M_Prop_{key}.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) { _matCache[key] = existing; return existing; }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader) { name = "M_Prop_" + key };

            Color c;
            float metallic = 0f, smooth = 0.3f;
            switch (key)
            {
                case "Wood":   c = new Color(0.78f, 0.58f, 0.40f); smooth = 0.25f; break;
                case "Fabric": c = new Color(0.48f, 0.52f, 0.60f); smooth = 0.15f; break;
                case "Metal":  c = new Color(0.72f, 0.76f, 0.84f); metallic = 0.15f; smooth = 0.45f; break;
                case "Locker": c = new Color(0.55f, 0.62f, 0.72f); metallic = 0.10f; smooth = 0.40f; break;
                case "Crate":  c = new Color(0.70f, 0.60f, 0.42f); smooth = 0.20f; break;
                case "Red":    c = new Color(0.85f, 0.22f, 0.18f); smooth = 0.50f; break;
                case "White":  c = new Color(0.92f, 0.94f, 0.96f); smooth = 0.35f; break;
                case "Dark":   c = new Color(0.28f, 0.30f, 0.34f); smooth = 0.30f; break;
                case "Board":  c = new Color(0.82f, 0.78f, 0.66f); smooth = 0.20f; break;
                case "Server": c = new Color(0.40f, 0.45f, 0.52f); metallic = 0.20f; smooth = 0.50f; break;
                case "Screen":
                    c = new Color(0.25f, 0.32f, 0.42f);
                    smooth = 0.75f;
                    break;
                default:       c = new Color(0.5f, 0.5f, 0.5f); break;
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            mat.color = c;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);

            // 自发光：保证在昏暗环境中也能看清道具轮廓
            //   S6 实测：关卡环境光很暗（0.10），纯靠环境光家具几乎不可见，
            //   因此给所有道具加一层很弱的自发光（屏幕更亮）。
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                float em;
                if (key == "Screen") em = 1.6f;
                else if (key == "Red") em = 0.35f;
                else if (key == "White") em = 0.30f;
                else em = 0.22f;

                var emColor = key == "Screen"
                    ? new Color(0.10f, 0.22f, 0.35f) * em
                    : new Color(c.r, c.g, c.b) * em;
                mat.SetColor("_EmissionColor", emColor);
            }

            AssetDatabase.CreateAsset(mat, path);
            _matCache[key] = mat;
            return mat;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int idx = path.LastIndexOf('/');
            string parent = path.Substring(0, idx);
            string leaf = path.Substring(idx + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        // ────────────────────────── 房间信息 ──────────────────────────

        private struct RoomInfo
        {
            public string Id;
            public Vector3 Center;
            public float SizeX;
            public float SizeZ;
            public float Height;
        }

        private static Dictionary<string, RoomInfo> CollectRooms()
        {
            var result = new Dictionary<string, RoomInfo>();
            foreach (var r in AbyssEcho.LevelGen.ChapterLayout.GetRooms())
            {
                result[r.Id] = new RoomInfo
                {
                    Id = r.Id,
                    Center = new Vector3(r.Center.x, 0f, r.Center.z),
                    SizeX = r.SizeX,
                    SizeZ = r.SizeZ,
                    Height = r.Height
                };
            }
            return result;
        }
    }
}
