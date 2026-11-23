using System.Collections.Generic;
using AbyssEcho.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S12 第二章交互布置：抽水泵、录音机、频率终端、笔记。
    ///
    /// 布置原则（与第一章 InteractablePlacementBuilder 一致）：
    ///   · 坐标全部由房间数据计算，房间改动后自动跟随
    ///   · 幂等：先删旧根节点再重建
    ///   · 不挡主通路
    ///
    /// 谜题链条：
    ///   D3 抽水泵 → 抽干 D2 大厅的水
    ///   D4 档案室 → 读到频率记录（220 Hz）
    ///   D6 卧房   → 两段录音（补充真相）
    ///   E2 混响舱 → 能"听到"回声（听觉线索）
    ///   E3 控制室 → 调到 220 Hz → 解锁 E4 的门
    /// </summary>
    public static class ChapterTwoContentBuilder
    {
        private const string RootName = "--- CH2_CONTENT ---";
        private const string MatFolder = "Assets/_Project/Art/Materials/Interactable";

        [MenuItem("ABYSS ECHO/S12 布置第二章交互物")]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[Ch2Content] 请先打开场景");
                return;
            }

            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(RootName);
            var rooms = CollectRooms();
            int created = 0;

            // ══════════════ ① D2 大厅：水面 ══════════════
            GameObject waterSurface = null;
            if (rooms.TryGetValue("D2", out var d2))
            {
                waterSurface = CreateWaterSurface(root.transform, d2);
                created++;
            }

            // ══════════════ ② D3 泵房：抽水泵 ══════════════
            //
            // ⚠️ S26：先把 D2→L2 的门建好，再建抽水泵 ——
            //   水泵要拿到这扇门的引用才能在抽水后解锁它。
            DoorInteractable d2l2Door = null;
            if (rooms.TryGetValue("D2", out var d2Room))
            {
                // D2 北门（z=18.0）通往 L2 生活区甬道
                d2l2Door = CreateLockDoor(root.transform, "Door_D2_L2",
                    new Vector3(0f, 1.3f, d2Room.MaxZ - 0.3f),
                    new Vector3(2.4f, 2.6f, 0.14f),
                    prompt: "通道被水压封死 —— 需要先抽干 D2 大厅的积水");
                created++;
            }

            if (rooms.TryGetValue("D3", out var d3))
            {
                CreatePumpSwitch(root.transform, d3, waterSurface, d2l2Door);
                created++;
            }

            // ══════════════ ③ D4 档案室：频率记录笔记 ══════════════
            if (rooms.TryGetValue("D4", out var d4))
            {
                CreateFrequencyNote(root.transform, d4);
                created++;
            }

            // ══════════════ ④ D6 卧房：两台录音机 ══════════════
            if (rooms.TryGetValue("D6_1", out var d61))
            {
                CreateTape(root.transform, d61, new Vector3(0f, 0.85f, -1.2f), 0f,
                    "tape.1", "录音 01 · 第一次下潜",
                    "记录：第二小组进入 D 区第十二天。水一直没退。这栋楼比图纸上深得多——地面上是七层，地下还有十一层。我们数过。");
                created++;
            }

            if (rooms.TryGetValue("D6_2", out var d62))
            {
                CreateTape(root.transform, d62, new Vector3(0f, 0.85f, -1.2f), 0f,
                    "tape.2", "录音 02 · 回声",
                    "记录：声学组说，E 区那个舱会回放人声。不是录音，是活的回声——它会重复你刚才说的最后一句，然后用你的声音说别的东西。我们把它调到了 220。它安静了。");
                created++;
            }

            // ══════════════ ⑤ E3 控制室：频率终端 ══════════════
            DoorInteractable e4Door = null;
            if (rooms.TryGetValue("E1", out var e1) && rooms.TryGetValue("E4", out var e4))
            {
                // 在 E1 北门处建一道锁门，由频率终端解锁
                e4Door = CreateLockDoor(root.transform, "Door_E4",
                    new Vector3(e1.Center.x, 1.3f, e1.MaxZ - 0.3f),
                    new Vector3(2.2f, 2.6f, 0.14f));
                created++;
            }

            if (rooms.TryGetValue("E3", out var e3))
            {
                CreateFrequencyConsole(root.transform, e3, e4Door);
                created++;
            }

            Debug.Log($"[Ch2Content] 布置完成，共创建 {created} 个物件");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        // ────────────────────────── 水面 ──────────────────────────

        private static GameObject CreateWaterSurface(Transform parent, RoomInfo r)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Water_D2";
            go.transform.SetParent(parent, false);

            // 水面：覆盖大半个大厅，高度 0.5 米（过膝）
            go.transform.position = new Vector3(r.Center.x, 0.25f, r.Center.z);
            go.transform.localScale = new Vector3(r.SizeX - 1.0f, 0.5f, r.SizeZ - 1.0f);

            var mat = MakeWaterMaterial();
            go.GetComponent<Renderer>().sharedMaterial = mat;

            // 水面不阻挡通行（玩家要能走进去）
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            return go;
        }

        private static Material MakeWaterMaterial()
        {
            EnsureFolder(MatFolder);
            string path = MatFolder + "/M_Water_D2.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader) { name = "M_Water_D2" };

            var c = new Color(0.10f, 0.22f, 0.28f, 1f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            mat.color = c;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.2f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.92f);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.05f, 0.14f, 0.18f));
            }

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // ────────────────────────── 抽水泵 ──────────────────────────

        private static void CreatePumpSwitch(Transform parent, RoomInfo r, GameObject water, DoorInteractable pumpDoor)
        {
            // 泵体
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Pump_Body";
            body.transform.SetParent(parent, false);
            body.transform.position = r.Center + new Vector3(-2f, 0.6f, 0f);
            body.transform.localScale = new Vector3(1.4f, 1.2f, 1.0f);
            body.GetComponent<Renderer>().sharedMaterial = MakeLitMaterial("M_Pump_Body",
                new Color(0.42f, 0.46f, 0.52f), 0.15f);

            // 控制面板
            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "Pump_Panel";
            panel.transform.SetParent(parent, false);
            panel.transform.position = r.Center + new Vector3(-2f, 1.05f, -0.54f);
            panel.transform.localScale = new Vector3(0.9f, 0.6f, 0.08f);
            panel.GetComponent<Renderer>().sharedMaterial = MakeLitMaterial("M_Pump_Panel",
                new Color(0.16f, 0.18f, 0.22f), 0.05f);

            // 指示灯
            var lamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lamp.name = "Pump_Indicator";
            lamp.transform.SetParent(parent, false);
            lamp.transform.position = r.Center + new Vector3(-2f, 1.22f, -0.59f);
            lamp.transform.localScale = new Vector3(0.12f, 0.06f, 0.03f);
            var lampCol = lamp.GetComponent<Collider>();
            if (lampCol != null) Object.DestroyImmediate(lampCol);
            lamp.GetComponent<Renderer>().sharedMaterial = MakeEmissiveMaterial("M_Pump_Lamp",
                new Color(0.9f, 0.25f, 0.15f));

            // 交互组件
            var sw = body.AddComponent<AbyssEcho.Puzzle.WaterPumpSwitch>();
            sw.waterSurface = water;
            sw.indicatorRenderers = new Renderer[] { lamp.GetComponent<Renderer>() };
            sw.pumpDuration = 3.5f;
            sw.customPrompt = "启动抽水泵";

            // ⚠️ S26 补充（原实现漏了）：抽水必须解锁 D2→L2 的通道，
            //   否则抽水在玩法上没有任何意义。
            if (pumpDoor != null)
            {
                sw.doorsToUnlock = new DoorInteractable[] { pumpDoor };
                Debug.Log("[Ch2Content] 抽水泵已关联 Door_D2_L2");
            }
            else
            {
                Debug.LogError("[Ch2Content] 抽水泵没有关联门 —— 抽水将不会解锁任何东西！");
            }

            // 让泵体和面板都能交互（面板撞到检测）
            var panelCol = panel.AddComponent<AbyssEcho.Interaction.InteractableProxy>();
            if (panelCol != null) panelCol.target = sw;

            EditorUtility.SetDirty(sw);
        }

        // ────────────────────────── 录音机 ──────────────────────────

        private static void CreateTape(Transform parent, RoomInfo r, Vector3 offset, float yaw,
                                       string id, string title, string text)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Tape_" + id.Replace('.', '_');
            go.transform.SetParent(parent, false);
            go.transform.position = r.Center + offset;
            go.transform.localScale = new Vector3(0.34f, 0.12f, 0.24f);
            go.GetComponent<Renderer>().sharedMaterial = MakeLitMaterial("M_Tape_Body",
                new Color(0.22f, 0.20f, 0.18f), 0.1f);

            // 驱动指示灯
            var lamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lamp.name = "Tape_Indicator";
            lamp.transform.SetParent(parent, false);
            lamp.transform.position = r.Center + offset + new Vector3(0.10f, 0.07f, 0f);
            lamp.transform.localScale = new Vector3(0.05f, 0.03f, 0.05f);
            var lampCol = lamp.GetComponent<Collider>();
            if (lampCol != null) Object.DestroyImmediate(lampCol);
            lamp.GetComponent<Renderer>().sharedMaterial = MakeEmissiveMaterial("M_Tape_Lamp",
                new Color(0.5f, 0.16f, 0.08f));

            var tape = go.AddComponent<AbyssEcho.Puzzle.TapeRecorder>();
            tape.tapeId = id;
            tape.tapeTitle = title;
            tape.transcript = text;
            tape.indicatorRenderers = new Renderer[] { lamp.GetComponent<Renderer>() };
            tape.addToJournal = true;
            tape.displayDuration = 14f;

            EditorUtility.SetDirty(tape);
        }

        // ────────────────────────── 频率终端 ──────────────────────────

        private static void CreateFrequencyConsole(Transform parent, RoomInfo r, DoorInteractable door)
        {
            // 操作台
            var desk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            desk.name = "FreqConsole_Desk";
            desk.transform.SetParent(parent, false);
            desk.transform.position = r.Center + new Vector3(1.6f, 0.75f, 0f);
            desk.transform.localScale = new Vector3(1.0f, 0.1f, 2.4f);
            desk.GetComponent<Renderer>().sharedMaterial = MakeLitMaterial("M_Console_Desk",
                new Color(0.30f, 0.34f, 0.40f), 0.2f);

            // 支撑
            var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leg.name = "FreqConsole_Leg";
            leg.transform.SetParent(parent, false);
            leg.transform.position = r.Center + new Vector3(1.6f, 0.37f, 0f);
            leg.transform.localScale = new Vector3(0.9f, 0.74f, 2.2f);
            leg.GetComponent<Renderer>().sharedMaterial = MakeLitMaterial("M_Console_Leg",
                new Color(0.14f, 0.16f, 0.19f), 0.05f);

            // 屏幕
            var screen = GameObject.CreatePrimitive(PrimitiveType.Cube);
            screen.name = "FreqConsole_Screen";
            screen.transform.SetParent(parent, false);
            screen.transform.position = r.Center + new Vector3(1.6f, 0.86f, 0f);
            screen.transform.localScale = new Vector3(0.7f, 0.05f, 1.6f);
            var screenCol = screen.GetComponent<Collider>();
            if (screenCol != null) Object.DestroyImmediate(screenCol);
            screen.GetComponent<Renderer>().sharedMaterial = MakeEmissiveMaterial("M_Console_Screen",
                new Color(0.12f, 0.30f, 0.42f));

            // 交互
            var console = desk.AddComponent<AbyssEcho.Puzzle.FrequencyConsole>();
            console.correctFrequency = 220f;
            console.minFrequency = 100f;
            console.maxFrequency = 400f;
            console.stepSize = 10f;
            console.tolerance = 5f;
            console.doorsToUnlock = door != null ? new[] { door } : new DoorInteractable[0];
            console.indicatorRenderers = new Renderer[] { screen.GetComponent<Renderer>() };

            EditorUtility.SetDirty(console);
        }

        // ────────────────────────── 频率记录笔记 ──────────────────────────

        private static void CreateFrequencyNote(Transform parent, RoomInfo r)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Note_Frequency";
            go.transform.SetParent(parent, false);
            go.transform.position = r.Center + new Vector3(-1.5f, 0.88f, 1.5f);
            go.transform.localScale = new Vector3(0.30f, 0.02f, 0.42f);
            go.GetComponent<Renderer>().sharedMaterial = MakeLitMaterial("M_Note_Paper",
                new Color(0.95f, 0.92f, 0.80f), 0f);

            var note = go.AddComponent<AbyssEcho.Narrative.ReadableNote>();
            note.title = "声学组备忘 · 0312";
            note.journalTitle = "声学组备忘 · 0312";
            note.body = "残响实验记录：把终端调到 220 Hz，舱内的回声会与人声同步。其他频率只能得到延迟的重复。负责人：林";
            note.addToJournal = true;

            EditorUtility.SetDirty(note);
        }

        // ────────────────────────── 门 ──────────────────────────

        private static DoorInteractable CreateLockDoor(Transform parent, string name,
                                                       Vector3 pos, Vector3 size,
                                                       string prompt = "门锁着（需要解除频率锁定）")
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = MakeLitMaterial("M_Door_Deep",
                new Color(0.26f, 0.30f, 0.36f), 0.2f);

            var door = go.AddComponent<DoorInteractable>();
            door.locked = true;
            door.lockedPrompt = prompt;
            door.swingAngle = 0f;

            // ⚠️ S26 修复：
            //   ① unlockableByInteraction 必须为 false ——
            //      否则玩家对着锁门按 E 会直接解锁，谜题形同虚设。
            //   ② slideDistance 按门宽算（不是按 X 尺寸），
            //      并且开启 autoSlideAxis 让门沿自己的宽度方向滑开。
            float doorWidth = Mathf.Max(size.x, size.z);
            door.slideDistance = doorWidth * 0.98f;
            door.speed = 2.0f;
            door.unlockableByInteraction = false;
            door.autoSlideAxis = true;

            EditorUtility.SetDirty(door);
            return door;
        }

        // ────────────────────────── 材质工具 ──────────────────────────

        private static Material MakeLitMaterial(string name, Color color, float metallic)
        {
            EnsureFolder(MatFolder);
            string path = $"{MatFolder}/{name}.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader) { name = name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.color = color;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.4f);

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static Material MakeEmissiveMaterial(string name, Color color)
        {
            EnsureFolder(MatFolder);
            string path = $"{MatFolder}/{name}.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader) { name = name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.color = color;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.6f);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 2.0f);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // ────────────────────────── 房间数据 ──────────────────────────

        private struct RoomInfo
        {
            public string Id;
            public Vector3 Center;
            public float SizeX;
            public float SizeZ;
            public float MinZ => Center.z - SizeZ * 0.5f;
            public float MaxZ => Center.z + SizeZ * 0.5f;
        }

        private static Dictionary<string, RoomInfo> CollectRooms()
        {
            var result = new Dictionary<string, RoomInfo>();
            foreach (var r in AbyssEcho.LevelGen.ChapterTwoLayout.Build())
            {
                result[r.Id] = new RoomInfo
                {
                    Id = r.Id,
                    Center = new Vector3(r.Center.x, 0f, r.Center.z),
                    SizeX = r.SizeX,
                    SizeZ = r.SizeZ
                };
            }
            return result;
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
    }
}
