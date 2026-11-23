using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S5 恐怖系统装配：音频管理器、恐惧系统、脚步声、灯光闪烁、恐怖触发器。
    ///
    /// 与其他 Builder 一致：幂等、代码布置、场景重建后自动恢复。
    /// </summary>
    public static class HorrorSetupBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";
        private const string RootName = "--- HORROR ---";

        [MenuItem("ABYSS ECHO/S5 装配恐怖系统")]
        public static void Setup()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[HorrorSetup] 请先打开 Level_Chapter1_Graybox 场景");
                return;
            }

            // ① 音频管理器（常驻）
            var audioGo = GameObject.Find("AudioManager");
            if (audioGo == null) audioGo = new GameObject("AudioManager");
            var audio = audioGo.GetComponent<AbyssEcho.Audio.AudioManager>();
            if (audio == null) audio = audioGo.AddComponent<AbyssEcho.Audio.AudioManager>();
            audio.masterVolume = 0.8f;
            audio.sfxVolume = 0.85f;
            audio.ambientVolume = 0.32f;

            var player = GameObject.Find("Player");
            if (player == null) { Debug.LogError("[HorrorSetup] 找不到 Player"); return; }

            var cam = player.GetComponentInChildren<Camera>(true);

            // ② 恐惧系统（挂在玩家上）
            var fear = player.GetComponent<AbyssEcho.Horror.FearSystem>();
            if (fear == null) fear = player.AddComponent<AbyssEcho.Horror.FearSystem>();
            fear.input = player.GetComponent<AbyssEcho.Player.PlayerInputReader>();
            fear.flashlight = player.GetComponent<AbyssEcho.Tools.FlashlightSystem>();

            // ③ 恐惧视觉（挂在相机上）
            if (cam != null)
            {
                var visuals = cam.GetComponent<AbyssEcho.Horror.FearVisuals>();
                if (visuals == null) visuals = cam.gameObject.AddComponent<AbyssEcho.Horror.FearVisuals>();
                visuals.fear = fear;
                visuals.targetCamera = cam;
                EditorUtility.SetDirty(visuals);
            }

            // ④ 脚步声（挂在玩家上）
            var steps = player.GetComponent<AbyssEcho.Horror.PlayerFootsteps>();
            if (steps == null) steps = player.AddComponent<AbyssEcho.Horror.PlayerFootsteps>();
            steps.motor = player.GetComponent<AbyssEcho.Player.PlayerMotor>();
            steps.onMetalSurface = true;

            // ④b 死亡与重开系统（挂在玩家上）
            var death = player.GetComponent<AbyssEcho.Horror.PlayerDeathSystem>();
            if (death == null) death = player.AddComponent<AbyssEcho.Horror.PlayerDeathSystem>();

            // ⑤ 给应急灯添加闪烁组件 + 布置恐怖触发器
            var horrorRoot = GameObject.Find(RootName);
            if (horrorRoot != null) Object.DestroyImmediate(horrorRoot);
            horrorRoot = new GameObject(RootName);

            int flickerCount = AttachFlickerToLights();
            int triggerCount = CreateHorrorTriggers(horrorRoot.transform);

            EditorUtility.SetDirty(audioGo);
            EditorUtility.SetDirty(player);
            if (cam != null) EditorUtility.SetDirty(cam.gameObject);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log("<b>[HorrorSetup] 恐怖系统已装配</b>\n" +
                      $"  · AudioManager（程序化音效）\n" +
                      $"  · FearSystem → Player\n" +
                      $"  · FearVisuals → Main Camera\n" +
                      $"  · PlayerFootsteps → Player\n" +
                      $"  · PlayerDeathSystem → Player\n" +
                      $"  · 闪烁灯光：{flickerCount} 盏\n" +
                      $"  · 恐怖触发器：{triggerCount} 个");
        }

        // ────────────────────────── 灯光闪烁 ──────────────────────────

        /// <summary>给 ROOM_LIGHTS 下的部分灯添加闪烁组件（约 40%）。</summary>
        private static int AttachFlickerToLights()
        {
            var root = GameObject.Find("--- ROOM_LIGHTS ---");
            if (root == null) return 0;

            int added = 0;
            var lights = root.GetComponentsInChildren<Light>(true);

            // 用固定步长挑选，保证每次重建结果一致（便于复现问题）
            for (int i = 0; i < lights.Length; i++)
            {
                if (i % 5 != 0 && i % 7 != 0) continue;   // 约 40% 的灯会闪

                var l = lights[i];
                if (l == null || l.intensity <= 0.01f) continue;   // 跳过已损坏的灯

                var fl = l.GetComponent<AbyssEcho.Horror.FlickerLight>();
                if (fl == null) fl = l.gameObject.AddComponent<AbyssEcho.Horror.FlickerLight>();

                fl.mode = AbyssEcho.Horror.FlickerLight.Mode.Flicker;
                fl.baseIntensity = l.intensity;
                fl.minInterval = 1.5f;
                fl.maxInterval = 5.5f;
                fl.fearReactive = true;
                EditorUtility.SetDirty(fl);
                added++;
            }

            return added;
        }

        // ────────────────────────── 恐怖触发器 ──────────────────────────

        private static int CreateHorrorTriggers(Transform parent)
        {
            var rooms = CollectRooms();
            int created = 0;

            // 触发器 1：A1 入口走廊 —— 玩家第一次走进走廊时，远端传来讲究的脚步声
            if (rooms.TryGetValue("A1", out var a1))
            {
                CreateTrigger(parent, "Horror_A1_Footsteps",
                    a1.Center + new Vector3(0f, 1f, -a1.SizeZ * 0.3f),
                    new Vector3(a1.SizeX * 0.8f, 2.4f, 2f),
                    delay: 1.4f,
                    stinger: false,
                    fearJump: 0.18f,
                    footstepAt: a1.Center + new Vector3(0f, 0f, a1.SizeZ * 0.4f),
                    footstepCount: 4);
                created++;
            }

            // 触发器 2：A2 气密闸室 —— 进入后灯全灭 + 惊吓音
            if (rooms.TryGetValue("A2", out var a2))
                if (rooms.TryGetValue("A1_A2_Link", out var a2Link))
                {
                    CreateTrigger(parent, "Horror_A2_Blackout",
                        a2.Center + new Vector3(0f, 1f, 0f),
                        new Vector3(a2.SizeX * 0.7f, 2.4f, a2.SizeZ * 0.7f),
                        delay: 0.8f,
                        stinger: true,
                        fearJump: 0.32f,
                        footstepAt: null,
                        footstepCount: 0,
                        killLightsInRoom: "A2");
                    created++;
                }

            // 触发器 3：B2 宿舍走廊 —— 远处脚步声 + 恐惧上升
            if (rooms.TryGetValue("B2", out var b2))
            {
                CreateTrigger(parent, "Horror_B2_DistantSteps",
                    b2.Center + new Vector3(-b2.SizeX * 0.35f, 1f, 0f),
                    new Vector3(3f, 2.4f, b2.SizeZ * 0.8f),
                    delay: 1.0f,
                    stinger: false,
                    fearJump: 0.22f,
                    footstepAt: b2.Center + new Vector3(b2.SizeX * 0.45f, 0f, 0f),
                    footstepCount: 5);
                created++;
            }

            // 触发器 4：C1 主厅 —— 最强的一次惊吓（灯闪 + 音效 + 恐惧跳升）
            if (rooms.TryGetValue("C1", out var c1))
            {
                CreateTrigger(parent, "Horror_C1_Climax",
                    c1.Center + new Vector3(0f, 1.5f, 0f),
                    new Vector3(c1.SizeX * 0.6f, 3f, c1.SizeZ * 0.5f),
                    delay: 1.2f,
                    stinger: true,
                    fearJump: 0.45f,
                    footstepAt: null,
                    footstepCount: 0,
                    flickerLightsInRoom: "C1");
                created++;
            }

            return created;
        }

        private static void CreateTrigger(Transform parent, string name, Vector3 pos, Vector3 size,
                                          float delay, bool stinger, float fearJump,
                                          Vector3? footstepAt, int footstepCount,
                                          string killLightsInRoom = null,
                                          string flickerLightsInRoom = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = size;

            var trigger = go.AddComponent<AbyssEcho.Horror.HorrorTrigger>();
            trigger.delay = delay;
            trigger.playStinger = stinger;
            trigger.fearJump = fearJump;
            trigger.footstepCount = footstepCount;
            trigger.oneShot = true;

            // 脚步声源位置
            if (footstepAt.HasValue)
            {
                var fs = new GameObject("StepSource");
                fs.transform.SetParent(go.transform, false);
                fs.transform.position = footstepAt.Value;
                trigger.footstepSource = fs.transform;
            }

            // 关联灯光
            if (!string.IsNullOrEmpty(killLightsInRoom))
                trigger.lightsToKill = CollectRoomLights(killLightsInRoom, true);

            if (!string.IsNullOrEmpty(flickerLightsInRoom))
                trigger.lightsToFlicker = CollectFlickers(flickerLightsInRoom);

            // ⚠️ S20：环境光压暗。
            //    以前只关灯，但场景环境光 (0.26, 0.29, 0.33) 本身就够亮，
            //    玩家灭灯后完全感觉不到"变暗"。
            //    凡是"灭灯"类事件，都必须同时压环境光。
            bool isBlackout = !string.IsNullOrEmpty(killLightsInRoom);
            trigger.dimAmbient = isBlackout;
            trigger.dimAmbientTo = 0.12f;   // 只剩 12% 亮度
            trigger.dimDuration = 1.0f;

            EditorUtility.SetDirty(trigger);
            EditorUtility.SetDirty(go);
        }

        private static Light[] CollectRoomLights(string roomId, bool onlyEnabled)
        {
            var result = new List<Light>();
            var root = GameObject.Find("--- ROOM_LIGHTS ---");
            if (root == null) return result.ToArray();

            foreach (Transform child in root.transform)
            {
                if (!child.name.Contains(roomId)) continue;
                var l = child.GetComponent<Light>();
                if (l == null) continue;
                if (onlyEnabled && !l.enabled) continue;
                result.Add(l);
            }
            return result.ToArray();
        }

        private static AbyssEcho.Horror.FlickerLight[] CollectFlickers(string roomId)
        {
            var result = new List<AbyssEcho.Horror.FlickerLight>();
            var root = GameObject.Find("--- ROOM_LIGHTS ---");
            if (root == null) return result.ToArray();

            foreach (Transform child in root.transform)
            {
                if (!child.name.Contains(roomId)) continue;
                var f = child.GetComponent<AbyssEcho.Horror.FlickerLight>();
                if (f != null) result.Add(f);
            }
            return result.ToArray();
        }

        // ────────────────────────── 房间坐标 ──────────────────────────

        private struct RoomInfo
        {
            public string Id;
            public Vector3 Center;
            public float SizeX;
            public float SizeZ;
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
                    SizeZ = r.SizeZ
                };
            }
            return result;
        }
    }
}