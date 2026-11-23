using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S3A 第一章灰盒场景构建。
    /// 菜单：ABYSS ECHO/S3A 构建第一章灰盒
    ///
    /// 产出：
    ///  - Assets/_Project/Scenes/Level_Chapter1_Graybox.unity
    ///  - Assets/_Project/Art/Materials/（灰盒材质）
    ///
    /// 同时把布局校验结果写入 Reports/S3A_LayoutValidation.txt。
    /// </summary>
    public static class ChapterOneSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";
        private const string MatFolder = "Assets/_Project/Art/Materials";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player/PF_Player.prefab";
        private const string ReportPath = "../Reports/S3A_LayoutValidation.txt";

        [MenuItem("ABYSS ECHO/S3A 构建第一章灰盒")]
        public static void Build()
        {
            EnsureFolder(MatFolder);

            var rooms = AbyssEcho.LevelGen.ChapterLayout.GetRooms();

            // 1) 先做纯数据校验
            var check = AbyssEcho.LevelGen.LayoutValidator.Validate(rooms);
            string report = AbyssEcho.LevelGen.LayoutValidator.Format(check);
            Debug.Log("[ABYSS ECHO] " + report);
            WriteReport(report);

            if (!check.Passed)
            {
                Debug.LogError("[ABYSS ECHO] 布局校验未通过，已停止生成场景。请先修正布局数据。");
                return;
            }

            // 2) 创建场景
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 环境光（灰盒阶段亮度足够看清空间）
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.26f, 0.29f, 0.33f);

            // 方向光（S3B 起只作为微弱环境反射，不作为主光源）
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.08f;
            light.color = new Color(0.45f, 0.60f, 0.85f);
            light.shadows = LightShadows.None;
            lightGo.transform.rotation = Quaternion.Euler(55f, -40f, 0f);

            // 材质（S3B：优先使用 ambientCG 的真实 PBR 材质，缺失则回退纯色）
            var wallMatFallback = GetOrCreateMat("M_Graybox_Wall", new Color(0.34f, 0.36f, 0.39f), 0.15f, 0.25f);
            var floorMatFallback = GetOrCreateMat("M_Graybox_Floor", new Color(0.20f, 0.22f, 0.24f), 0.10f, 0.30f);
            var ceilMat = GetOrCreateMat("M_Graybox_Ceiling", new Color(0.42f, 0.45f, 0.50f), 0.0f, 0.15f);

            var wallMetal = LoadMat("M_Wall_Metal");
            var wallConcrete = LoadMat("M_Concrete");
            var wallTiled = LoadMat("M_Tiles");
            var floorDiamond = LoadMat("M_Floor_Industrial");
            var floorConcrete = LoadMat("M_Concrete");

            bool usingPbr = wallMetal != null && floorDiamond != null;
            Debug.Log("[ABYSS ECHO] 材质来源：" + (usingPbr ? "ambientCG 真实 PBR" : "纯色回退（请先执行 S3B 构建材质库）"));

            // 3) 生成几何
            var genGo = new GameObject("--- LEVEL_GENERATOR ---");
            var gen = genGo.AddComponent<AbyssEcho.LevelGen.GrayboxBuilder>();
            gen.wallMaterial = wallMatFallback;
            gen.floorMaterial = floorMatFallback;
            gen.ceilingMaterial = ceilMat;
            gen.wallMaterialMetal = wallMetal;
            gen.wallMaterialConcrete = wallConcrete;
            gen.wallMaterialTiled = wallTiled;
            gen.floorMaterialDiamond = floorDiamond;
            gen.floorMaterialConcrete = floorConcrete;
            gen.buildCeilings = true;

            var envRoot = gen.Build(rooms, genGo.transform);

            // ⚠️ 重要（S3A 实测踩坑）：
            //   几何是作为 genGo 的子物体生成的，绝不能 genGo.SetActive(false)
            //   —— 那会让整个关卡的 Collider 全部失活，玩家会直接穿过地板无限下坠
            //   （实测 y 从 0 掉到 -4380）。
            //   正确做法：保留 genGo 激活，仅销毁生成器组件本身（它没有运行时代码）。
            UnityEngine.Object.DestroyImmediate(gen);

            // 4) 生成统计（便于验收时核对）
            int floorCount = 0, wallCount = 0, ceilCount = 0;
            foreach (Transform room in envRoot)
                foreach (Transform child in room)
                {
                    var n = child.name;
                    if (n.StartsWith("Floor_")) floorCount++;
                    else if (n.StartsWith("Ceiling_")) ceilCount++;
                    else wallCount++;
                }
            Debug.Log($"[ABYSS ECHO] 几何统计：房间 {rooms.Count}，地板 {floorCount}，天花板 {ceilCount}，墙体段 {wallCount}");

            // 4) 放置玩家
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            GameObject playerGo = null;
            if (playerPrefab != null)
            {
                playerGo = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                if (playerGo == null)
                {
                    Debug.LogError("[ChapterOneSceneBuilder] PrefabUtility.InstantiatePrefab 返回 null");
                }
                else
                {
                    playerGo.name = "Player";
                    playerGo.transform.position = AbyssEcho.LevelGen.ChapterOneLayout.PlayerStart;
                    playerGo.transform.rotation = Quaternion.identity;

                    // ⚠️ S3B 踩坑：曾经出现"场景里没有 Player / 没有 Camera"，导致截图全黑。
                    //   原因是实例化后未显式加入当前场景（某些重建流程下会落到未激活场景）。
                    //   这里强制移入当前场景，并验证相机存在。
                    var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                    if (playerGo.scene != activeScene)
                    {
                        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(playerGo, activeScene);
                        Debug.LogWarning("[ChapterOneSceneBuilder] 玩家不在活动场景，已强制移动到：" + activeScene.name);
                    }

                    // 验证相机与组件完整性
                    var cam = playerGo.GetComponentInChildren<Camera>(true);
                    var ccCheck = playerGo.GetComponent<CharacterController>();
                    var motorCheck = playerGo.GetComponent<AbyssEcho.Player.PlayerMotor>();
                    Debug.Log($"[ChapterOneSceneBuilder] 玩家校验：Camera={(cam != null ? cam.name : "缺失")}, " +
                              $"CharacterController={(ccCheck != null ? "有" : "缺失")}, " +
                              $"PlayerMotor={(motorCheck != null ? "有" : "缺失")}");

                    if (cam == null)
                        Debug.LogError("[ChapterOneSceneBuilder] 玩家没有相机，画面将全黑！");

                    // ⚠️ 关键修复（S3B 实测）：
                    //   以 Prefab 实例形式存在时，批处理重建场景后组件数据不会完整写回场景文件，
                    //   导致运行时场景里没有相机/没有 PlayerMotor（画面全黑）。
                    //   解决办法：解包成普通对象（UnpackPrefabInstance），组件数据直接写入场景。
                    //   代价：玩家不再是 Prefab 链接；但这不影响功能，且本阶段就在反复重建场景。
                    if (PrefabUtility.IsPartOfPrefabInstance(playerGo))
                    {
                        PrefabUtility.UnpackPrefabInstance(
                            playerGo,
                            PrefabUnpackMode.Completely,
                            InteractionMode.AutomatedAction);
                        Debug.Log("[ChapterOneSceneBuilder] 玩家已解包为普通对象，确保组件写入场景文件");
                    }
                }
            }
            else
            {
                Debug.LogError("[ChapterOneSceneBuilder] 找不到玩家 Prefab：" + PlayerPrefabPath);
            }

            // 4.1 灰盒阶段给玩家常亮手电筒，便于行走测试与人工查看
            if (playerGo != null)
            {
                var flash = playerGo.GetComponent<AbyssEcho.Tools.FlashlightSystem>();
                if (flash != null)
                {
                    var so = new SerializedObject(flash);
                    var p = so.FindProperty("batteryLifetimeSeconds");
                    if (p != null) p.floatValue = 99999f;   // 灰盒阶段不消耗电量
                    so.ApplyModifiedPropertiesWithoutUndo();
                    var onProp = so.FindProperty("isOn");
                    if (onProp != null) onProp.boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                // 4.2 灰盒流程测试组件（默认关闭自动运行；构建测试包时由脚本开启）
                var walkTest = playerGo.AddComponent<AbyssEcho.DebugTools.ChapterWalkTest>();
                walkTest.runOnStart = false;

                // 4.3 调试 HUD
                var debugGo = new GameObject("--- DEBUG ---");
                debugGo.AddComponent<AbyssEcho.DebugTools.PlayerDebugHUD>();

                // ⚠️ S3B 实测：AddComponent 之后必须显式标记所有改动对象为 dirty，
                //   否则 SaveScene 可能不把这些新组件写入场景文件
                //   （表现为场景里 runOnStart 有值但组件丢失）。
                EditorUtility.SetDirty(playerGo);
                EditorUtility.SetDirty(debugGo);
                EditorUtility.SetDirty(walkTest);
                EditorUtility.SetDirty(flash != null ? flash : playerGo);
            }

            // 5) 出生点与终点标记
            var spawnRoot = new GameObject("--- SPAWN_POINTS ---");
            var sp = new GameObject("SP_Player_Start");
            sp.transform.SetParent(spawnRoot.transform);
            sp.transform.position = AbyssEcho.LevelGen.ChapterOneLayout.PlayerStart;

            var endRoot = new GameObject("--- CHAPTER_END ---");
            var ep = new GameObject("CH1_EndPoint");
            ep.transform.SetParent(endRoot.transform);
            ep.transform.position = AbyssEcho.LevelGen.ChapterOneLayout.ChapterEndPoint;

            // 6) 保存并加入 Build Settings
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuild(ScenePath);

            Debug.Log($"<b>[ABYSS ECHO] S3A 灰盒构建完成</b>\n场景：{ScenePath}\n房间数：{rooms.Count}\n" +
                      $"校验：{(check.Passed ? "通过" : "未通过")}");
        }

        private static void WriteReport(string text)
        {
            try
            {
                string projectRoot = Path.GetDirectoryName(Path.GetFullPath(Application.dataPath));
                string path = Path.GetFullPath(Path.Combine(projectRoot, ReportPath));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, text, new UTF8Encoding(false));
                Debug.Log("[ABYSS ECHO] 布局校验报告已写入：" + path);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("写入校验报告失败：" + e.Message);
            }
        }

        private static Material LoadMat(string name)
        {
            string path = $"Assets/_Project/Art/Materials/Level/{name}.mat";
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        private static Material GetOrCreateMat(string name, Color c, float metallic, float smooth)
        {
            string path = MatFolder + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                // 每次重建都强制刷新参数（避免复用旧颜色导致改不动）
                if (existing.HasProperty("_BaseColor")) existing.SetColor("_BaseColor", c);
                existing.color = c;
                if (existing.HasProperty("_Metallic")) existing.SetFloat("_Metallic", metallic);
                if (existing.HasProperty("_Smoothness")) existing.SetFloat("_Smoothness", smooth);

                // 天花板等"受光不足"的表面加微弱自发光，保证不会全黑
                // （室内场景常用手法：环境光 + 自发光兜底，避免顶面死黑）
                if (name.Contains("Ceiling") && existing.HasProperty("_EmissionColor"))
                {
                    existing.EnableKeyword("_EMISSION");
                    existing.SetColor("_EmissionColor", c * 0.35f);
                    existing.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }

                EditorUtility.SetDirty(existing);
                return existing;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader) { name = name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            mat.color = c;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);
            if (name.Contains("Ceiling") && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", c * 0.35f);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void AddSceneToBuild(string scenePath)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (list.Exists(s => s.path == scenePath)) return;
            list.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}