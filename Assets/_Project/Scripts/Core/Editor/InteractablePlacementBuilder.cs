using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using AbyssEcho.Interaction;
using AbyssEcho.Inventory;
using AbyssEcho.Puzzle;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S4 交互物布置器：把保险丝、配电箱、门等交互物按关卡布局自动放到场景里。
    ///
    /// 为什么用代码布置而不是手工拖拽：
    ///   关卡是由 ChapterOneLayout 程序化生成的，房间坐标随时可能调整。
    ///   手工摆好后一旦重建场景就全丢了；用代码布置可以随场景重建自动恢复。
    ///
    /// 幂等：每次运行先删除旧根节点再重建。
    /// </summary>
    public static class InteractablePlacementBuilder
    {
        private const string RootName = "--- INTERACTABLES ---";

        [MenuItem("ABYSS ECHO/S4 布置交互物")]
        public static void Build()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[InteractablePlacement] 请先打开 Level_Chapter1_Graybox 场景");
                return;
            }

            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(RootName);
            var rooms = CollectRooms();

            int created = 0;

            // ① A3 接待室：保险丝（放在桌面上方，便于看见）
            if (rooms.TryGetValue("A3", out var a3))
            {
                CreatePickup(root.transform, "Pickup_Fuse",
                    a3.Center + new Vector3(2.2f, 0.95f, -1.0f),
                    new Vector3(0.28f, 0.28f, 0.28f),
                    ItemIds.Fuse, "保险丝",
                    new Color(1f, 0.72f, 0.18f));
                created++;
            }

            // ② A3 → B 区压力走廊：一扇需要"电力恢复"的门
            //    注意：这里不改布局数据，只在门洞位置放一个可交互门板
            //
            //    ⚠️ 必须先建门、再建配电箱（顺序见 ③）。
            //       配电箱需要拿到这扇门的引用来解锁它，
            //       如果先建配电箱，GameObject.Find("Door_A3_B") 会找不到。
            if (rooms.TryGetValue("A3_B_Link", out var link))
                if (rooms.TryGetValue("B1", out var b1))
                {
                    CreateDoor(root.transform, "Door_A3_B",
                        new Vector3(link.Center.x, 1.15f, link.MaxZ - 0.15f),
                        new Vector3(2.0f, 2.3f, 0.12f),
                        locked: true, swingAngle: 0f);
                    created++;
                }

            // ③ A3 接待室：配电箱（挂在北墙上）
            //    ⚠️ 必须在 Door_A3_B 创建之后，才能按名字找到它并关联解锁。
            if (rooms.TryGetValue("A3", out var a3b))
            {
                CreatePowerPanel(root.transform, "PowerPanel_A3",
                    a3b.Center + new Vector3(-2.6f, 1.35f, a3b.SizeZ * 0.5f - 0.25f));
                created++;
            }

            Debug.Log($"[InteractablePlacement] 布置完成，共创建 {created} 个交互物");

            // ④ C5 广播室：线索纸条（写着门禁密码）
            // ⚠️ 注意：body 里不要用 "\n" 拼接。
            //   实测 YAML 序列化会在转义换行处截断字符串，
            //   导致纸条内容只剩前半句（密码丢失）。改为单行文本，由 UI 自动折行。
            if (rooms.TryGetValue("C5", out var c5))
            {
                CreateNote(root.transform, "Note_KeypadCode",
                    // S22：抬高到 1.25 米（桌面高度），不用低头就能看到和瞄准
                    c5.Center + new Vector3(0.9f, 1.25f, -0.6f),
                    "手写便条",
                    "……监控室的备用门禁还没改。密码是 1 2 1 2。别在走廊里念叨这些数字——我听见它在学我说话。",
                    "门禁密码");
                created++;
            }

            // ⑤ C2 门禁门：必须先建门，键盘才能找到它作引用
            if (rooms.TryGetValue("C1_C2_Link", out var c2Link))
            {
                CreateDoor(root.transform, "Door_C2",
                    new Vector3(c2Link.Center.x, 1.15f, c2Link.Center.z),
                    new Vector3(0.12f, 2.3f, 2.0f),
                    locked: true, swingAngle: 0f);
                created++;
            }

            // ⑥ C2 监控室：门禁键盘面板（内部按名字查找 Door_C2）
            //
            // ⚠️ S22 致命修复（玩家实测"根本没看到过键盘"）：
            //
            //   原位置：c2.Center + (-SizeX/2 + 0.28, 1.35, 0)
            //           = (12.4 - 4 + 0.28) = x 8.68
            //   而 Door_C2 在 x = 7.70（C1 与 C2 之间的监控廊里）。
            //
            //   也就是说键盘在门的**C2 那一侧**，玩家被锁在门外（C1 侧），
            //   永远看不到它 —— 形成死锁：
            //      要看键盘 → 必须先进门
            //      要进门   → 必须先按键盘
            //
            //   修复：把键盘移到门的**C1 侧**，并贴在监控廊墙上，
            //       让玩家走到门前就能看见。
            // ⑦ 压力闸门：第一章终点的唯一入口，**必须锁住**
            //
            //    ⚠️ S25：必须在 CreateKeypad 之前创建 ——
            //       密钥盘要按名字找到它并加入 doorsToUnlock。
            //       （原来这个门在密钥盘之后创建，所以永远关联不上）
            //
            //    S9 修复：之前这里没有任何门，玩家可以从 C1 北门直冲终点，
            //    导致「一进游戏没多久就到结局」。现在必须拿到门禁密码才能通过。
            if (rooms.TryGetValue("C1_D_Gate", out var dGate))
            {
                CreateDoor(root.transform, "Door_D_Gate",
                    new Vector3(dGate.Center.x, 1.3f, dGate.MinZ + 0.4f),
                    new Vector3(3.0f, 2.6f, 0.14f),
                    locked: true, swingAngle: 0f);
                created++;
            }

            // ⑥ C2 监控室：门禁键盘面板
            //    （必须在 Door_C2 和 Door_D_Gate 都创建之后再建）
            if (rooms.TryGetValue("C1_C2_Link", out var keypadLink))
            {
                // ⚠️ S22 二次修正：键盘必须落在监控廊**内部**，且靠 C1 一侧。
                //
                //   监控廊 C1_C2_Link: x[7.0, 8.4], z[34.2, 35.8]
                //   Door_C2 在 x=7.70, z=35.00
                //
                //   第一次放在 z = Center.z - 1.05 = 33.95 —— 已经在廊道南墙之外，
                //   射线直接被墙挡住（实测命中 South_C1_C2_Link_Full）。
                //
                //   正确：x 贴廊道西端（C1 开口侧），z 靠南墙但仍在廊道内。
                float kx = keypadLink.MinX + 0.16f;
                float kz = keypadLink.Center.z - 0.55f;

                CreateKeypad(root.transform, "Keypad_C2", new Vector3(kx, 1.35f, kz));

                Debug.Log($"[Interactable] Keypad_C2 放在 ({kx:F2}, 1.35, {kz:F2})，" +
                          $"廊道 x[{keypadLink.MinX:F2},{keypadLink.MaxX:F2}] " +
                          $"z[{keypadLink.MinZ:F2},{keypadLink.MaxZ:F2}]");
                created++;
            }
            else if (rooms.TryGetValue("C2", out var c2))
            {
                // 兜底：找不到连接廊时仍放在 C2 内（但会打警告）
                Debug.LogWarning("[Interactable] 找不到 C1_C2_Link，键盘退回 C2 内（可能被门挡住）");
                CreateKeypad(root.transform, "Keypad_C2",
                    c2.Center + new Vector3(-c2.SizeX * 0.5f + 0.28f, 1.35f, 0f));
                created++;
            }

            Debug.Log($"[InteractablePlacement] 布置完成，共创建 {created} 个交互物");

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }

        // ────────────────────────── 创建各类交互物 ──────────────────────────

        private static void CreatePickup(Transform parent, string name, Vector3 pos, Vector3 size,
                                         string itemId, string displayName, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;

            var mat = MakeEmissiveMaterial("M_Pickup_" + itemId.Replace('.', '_'), color);
            go.GetComponent<Renderer>().sharedMaterial = mat;

            var pickup = go.AddComponent<PickupItem>();
            pickup.itemId = itemId;
            pickup.displayName = displayName;
            pickup.amount = 1;

            // 拾取物不要挡住玩家移动，做成触发器
            var col = go.GetComponent<BoxCollider>();
            col.isTrigger = true;

            // 轻微旋转，便于在暗处被注意到
            go.AddComponent<PickupSpin>();
        }

        private static void CreatePowerPanel(Transform parent, string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            // 箱体
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(go.transform, false);
            body.transform.localScale = new Vector3(0.75f, 0.95f, 0.18f);
            body.GetComponent<Renderer>().sharedMaterial = MakeLitMaterial("M_PowerPanel_Body",
                new Color(0.28f, 0.30f, 0.33f), 0.05f);

            // 指示灯
            var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lamp.name = "Indicator";
            lamp.transform.SetParent(go.transform, false);
            lamp.transform.localPosition = new Vector3(0f, 0.28f, -0.12f);
            lamp.transform.localScale = new Vector3(0.14f, 0.14f, 0.08f);
            lamp.GetComponent<Renderer>().sharedMaterial = MakeEmissiveMaterial("M_PowerPanel_Lamp",
                new Color(0.9f, 0.2f, 0.15f));

            var panel = go.AddComponent<PowerRestorePanel>();
            panel.indicatorRenderers = new Renderer[] { lamp.GetComponent<Renderer>() };
            panel.requiredItemId = ItemIds.Fuse;
            panel.consumeItem = true;

            // ⚠️ 修复（玩家实测"加了保险丝还是过不去第一道门"）：
            //   这里原本**从未设置 doorsToUnlock**，
            //   导致配电箱即使被保险丝激活，也没有任何门被解锁——
            //   Door_A3_B 的 locked 永远是 true，玩家被彻底挡住。
            //
            //   现在按名字关联 A3→B 的门（与 CreateKeypadPanel 关联 Door_C2 的做法一致）。
            //   调用方必须保证这扇门已经先创建好。
            var doorGo = GameObject.Find("Door_A3_B");
            if (doorGo != null)
            {
                var door = doorGo.GetComponent<DoorInteractable>();
                if (door != null)
                {
                    panel.doorsToUnlock = new DoorInteractable[] { door };
                    Debug.Log("[Interactable] PowerPanel_A3 已关联 Door_A3_B");
                }
                else Debug.LogError("[Interactable] Door_A3_B 上没有 DoorInteractable 组件");
            }
            else
            {
                Debug.LogError("[Interactable] 找不到 Door_A3_B —— 配电箱无法解锁任何门！" +
                               "请确认 CreatePowerPanel 在 CreateDoor(\"Door_A3_B\") 之后调用。");
            }
        }

        private static void CreateNote(Transform parent, string name, Vector3 pos,
                                       string title, string body, string journalTitle)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            // ⚠️ S22 修复（玩家实测"按 E 没反应"）：
            //   原来便条是 (0.32, 0.02, 0.24) 的薄片，平躺在地上。
            //   玩家必须低头到几乎垂直才能把准星压到它上面，
            //   而低头时准星早就越过便条了 → 感觉"按 E 没反应"。
            //   现在改成：
            //     ① 竖起来立在桌上（像一张真的纸），不用低头
            //     ② 放大到 0.5×0.6，准星更容易命中
            //     ③ 碰撞体放大到 1.6 倍，容错更高
            go.transform.localScale = new Vector3(0.50f, 0.60f, 0.02f);
            go.transform.rotation = Quaternion.Euler(74f, 18f, 0f);   // 斜靠在桌面上

            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = MakeEmissiveMaterial("M_Note_Paper", new Color(0.95f, 0.92f, 0.80f));

            var col = go.GetComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(1.6f, 1.3f, 6f);   // 相对 localScale 的倍数，明显放大判定范围

            var note = go.AddComponent<AbyssEcho.Narrative.ReadableNote>();
            note.title = title;
            note.body = body;
            note.journalTitle = journalTitle;
            note.addToJournal = true;
        }

        private static void CreateKeypad(Transform parent, string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            // 面板本体（竖向长条）
            // S22：放大 1.4 倍 + 提高自发光，让玩家在昏暗环境里一眼能看见
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(go.transform, false);
            body.transform.localScale = new Vector3(0.22f, 1.15f, 0.55f);
            body.GetComponent<Renderer>().sharedMaterial = MakeEmissiveMaterial("M_Keypad_Body",
                new Color(0.35f, 0.55f, 0.80f));   // 淡蓝自发光，明显

            // 指示灯（更亮）
            var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lamp.name = "Indicator";
            lamp.transform.SetParent(go.transform, false);
            lamp.transform.localPosition = new Vector3(0.14f, 0.42f, 0f);
            lamp.transform.localScale = new Vector3(0.10f, 0.16f, 0.16f);
            lamp.GetComponent<Renderer>().sharedMaterial = MakeEmissiveMaterial("M_Keypad_Lamp",
                new Color(1f, 0.35f, 0.20f));

            var keypad = go.AddComponent<AbyssEcho.Puzzle.KeypadPanel>();
            keypad.correctCode = "1212";
            keypad.maxDigits = 4;
            keypad.indicatorRenderers = new Renderer[] { lamp.GetComponent<Renderer>() };

            // 关联门（运行时按名字查找，避免编辑器里跨对象引用）
            //
            // ⚠️ S25 修复（玩家实测"压力闸门打不开，提示说要解除频率锁定，
            //    但第一章根本没有频率终端"）：
            //
            //   原来只关联 Door_C2，Door_D_Gate 则完全没有解锁途径 ——
            //   它的提示语指向一个第二章才存在的设备，属于死锁。
            //
            //   方案 A：输入 1212 通过门禁后，同时解除压力闸门的锁定。
            //   叙事上也说得通：C2 是监控室，能控制整层的闸门。
            var doors = new System.Collections.Generic.List<DoorInteractable>();

            var doorGo = GameObject.Find("Door_C2");
            if (doorGo != null)
            {
                var door = doorGo.GetComponent<DoorInteractable>();
                if (door != null) doors.Add(door);
            }
            else Debug.LogError("[Interactable] 找不到 Door_C2");

            var gateGo = GameObject.Find("Door_D_Gate");
            if (gateGo != null)
            {
                var gate = gateGo.GetComponent<DoorInteractable>();
                if (gate != null) doors.Add(gate);
            }
            else Debug.LogError("[Interactable] 找不到 Door_D_Gate");

            keypad.doorsToUnlock = doors.ToArray();
            Debug.Log($"[Interactable] Keypad_C2 关联 {doors.Count} 扇门：" +
                      string.Join("、", doors.ConvertAll(d => d.name)));
        }

        private static void CreateDoor(Transform parent, string name, Vector3 pos, Vector3 size,
                                       bool locked, float swingAngle)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;

            go.GetComponent<Renderer>().sharedMaterial = MakeLitMaterial("M_Door_Metal",
                new Color(0.42f, 0.44f, 0.48f), 0.1f);

            var door = go.AddComponent<DoorInteractable>();
            door.locked = locked;
            door.swingAngle = swingAngle;

            // ⚠️ S23 修复：滑动距离必须按**门宽**算，不是按 X 尺寸。
            //   原来：slideDistance = size.x > size.z ? size.x * 0.95f : 1.1f
            //         Door_C2 尺寸 (0.12, 2.30, 2.00)，X 比 Z 小 → 得到 1.1，
            //         但门宽其实是 2.0（沿 Z），1.1 只挪开一半，门洞还留着一块。
            //   现在：取 max(x,z) 作为门宽，滑动 0.98 倍，保证完全让开。
            float doorWidth = Mathf.Max(size.x, size.z);
            door.slideDistance = doorWidth * 0.98f;
            door.speed = 2.0f;
            door.autoSlideAxis = true;   // 沿门宽方向滑

            // ⚠️ S21 修复：锁着的门必须"只能由谜题解锁"。
            //
            //   玩家实测"第一个门我没拿保险丝都能开"。
            //   根因是 DoorInteractable.OnInteract：
            //       if (locked) { if (requiredItemId 非空) {查背包} locked = false; }
            //   没配 requiredItemId 时直接 locked = false —— 按 E 就开门。
            //
            //   之前靠独立的 DoorLockFixer 事后修正，
            //   但只要重建场景（BatchEntryS3B~S7）就会覆盖丢失。
            //   现在改为**创建时就设对**，不再依赖事后修复。
            if (locked)
            {
                door.unlockableByInteraction = false;
                door.requiredItemId = "";
                door.lockedPrompt = GetPuzzlePrompt(name);
                Debug.Log($"[Interactable] {name} 设为必须解谜");
            }
        }

        /// <summary>谜题门的锁定提示文案。</summary>
        private static string GetPuzzlePrompt(string doorName)
        {
            switch (doorName)
            {
                case "Door_A3_B":
                    return "门锁着 —— 需要先恢复电力（去 A3 接待室找配电箱）";
                case "Door_C2":
                    return "门禁锁着 —— 请对准门旁的键盘输入密码（密码线索在 C5 广播室）";
                case "Door_D_Gate":
                    return "压力闸门锁死 —— 需要先在 C2 监控室解除锁定";
                case "Door_E4":
                    return "需要解除频率锁定";
                default:
                    return "门锁着";
            }
        }

        // ────────────────────────── 材质工具 ──────────────────────────

        private static Material MakeLitMaterial(string name, Color color, float metallic)
        {
            const string folder = "Assets/_Project/Art/Materials/Interactable";
            EnsureFolder(folder);
            string path = $"{folder}/{name}.mat";

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
            const string folder = "Assets/_Project/Art/Materials/Interactable";
            EnsureFolder(folder);
            string path = $"{folder}/{name}.mat";

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
                mat.SetColor("_EmissionColor", color * 2.2f);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            AssetDatabase.CreateAsset(mat, path);
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

        // ────────────────────────── 房间坐标 ──────────────────────────

        private struct RoomInfo
        {
            public string Id;
            public Vector3 Center;
            public float SizeX;
            public float SizeZ;
            public float Height;
            public float MinZ => Center.z - SizeZ * 0.5f;
            public float MaxZ => Center.z + SizeZ * 0.5f;
            public float MinX => Center.x - SizeX * 0.5f;
            public float MaxX => Center.x + SizeX * 0.5f;
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

    /// <summary>拾取物缓慢旋转，提升在暗处的可辨识度。</summary>
    public class PickupSpin : MonoBehaviour
    {
        public float speed = 45f;
        public float bobAmplitude = 0.06f;
        public float bobSpeed = 1.6f;

        private Vector3 _basePos;
        private float _phase;

        private void Start()
        {
            _basePos = transform.position;
            _phase = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            transform.Rotate(Vector3.up, speed * Time.deltaTime, Space.World);
            var p = _basePos;
            p.y += Mathf.Sin(Time.time * bobSpeed + _phase) * bobAmplitude;
            transform.position = p;
        }
    }
}
