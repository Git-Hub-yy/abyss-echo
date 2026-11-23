using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S2 一键搭建：生成玩家 Prefab 与完整的 TEST_Player 测试场景。
    /// 菜单：ABYSS ECHO/S2 搭建玩家与测试场景
    ///
    /// 幂等：已存在的资源会被替换（本阶段只有测试资源，无正式关卡内容）。
    /// </summary>
    public static class PlayerSceneBuilder
    {
        private const string PrefabFolder = "Assets/_Project/Prefabs/Player";
        private const string PlayerPrefabPath = PrefabFolder + "/PF_Player.prefab";
        private const string ScenePath = "Assets/_Project/Scenes/TEST_Player.unity";
        private const string InputAssetPath = "Assets/_Project/Input/AbyssEchoPlayer.inputactions";
        private const string SettingsFolder = "Assets/_Project/Settings";

        [MenuItem("ABYSS ECHO/S2 搭建玩家与测试场景")]
        public static void BuildAll()
        {
            EnsureFolder(PrefabFolder);
            EnsureFolder(SettingsFolder);

            var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SettingsFolder + "/URP_Pipeline.asset");
            var inputAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(InputAssetPath);

            if (inputAsset == null)
            {
                Debug.LogError("[PlayerSceneBuilder] 找不到输入资源：" + InputAssetPath);
                return;
            }
            if (pipeline == null)
            {
                Debug.LogWarning("[PlayerSceneBuilder] 找不到 URP 管线资源，场景将使用当前默认管线。");
            }

            var playerPrefab = BuildPlayerPrefab(pipeline, inputAsset);
            BuildTestScene(playerPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<b>[ABYSS ECHO] S2 搭建完成</b>\n玩家 Prefab：" + PlayerPrefabPath + "\n测试场景：" + ScenePath);
        }

        // ────────────────────────── 玩家 Prefab ──────────────────────────

        private static GameObject BuildPlayerPrefab(RenderPipelineAsset pipeline, UnityEngine.InputSystem.InputActionAsset inputAsset)
        {
            // 根节点
            var root = new GameObject("PF_Player");
            root.tag = "Player";

            // CharacterController
            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.78f;
            cc.radius = 0.32f;
            cc.center = new Vector3(0f, 0.89f, 0f);
            cc.slopeLimit = 45f;
            cc.stepOffset = 0.35f;
            cc.skinWidth = 0.03f;
            cc.minMoveDistance = 0f;

            // 输入
            var input = root.AddComponent<AbyssEcho.Player.PlayerInputReader>();
            var so = new SerializedObject(input);
            var actionsProp = so.FindProperty("actionsAsset");
            if (actionsProp != null) actionsProp.objectReferenceValue = inputAsset;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 移动
            root.AddComponent<AbyssEcho.Player.PlayerMotor>();

            // 相机枢轴
            var pivot = new GameObject("CameraPivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(0f, 0f, 0f);

            // 相机
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(pivot.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            camGo.transform.localRotation = Quaternion.identity;

            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 220f;
            cam.fieldOfView = 68f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.008f, 0.012f, 0.018f, 1f);
            cam.allowHDR = true;
            cam.allowMSAA = true;

            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camData.antialiasingQuality = AntialiasingQuality.High;

            camGo.AddComponent<AudioListener>();

            // 视角
            var look = pivot.AddComponent<AbyssEcho.Player.PlayerLook>();
            var lookSo = new SerializedObject(look);
            SetRef(lookSo, "playerRoot", root.transform);
            SetRef(lookSo, "playerCamera", cam);
            SetRef(lookSo, "input", input);
            SetRef(lookSo, "motor", root.GetComponent<AbyssEcho.Player.PlayerMotor>());
            lookSo.ApplyModifiedPropertiesWithoutUndo();

            // 手部（挂在相机下）
            var hands = camGo.AddComponent<AbyssEcho.Player.PlayerHands>();
            var handsSo = new SerializedObject(hands);
            SetRef(handsSo, "input", input);
            SetRef(handsSo, "motor", root.GetComponent<AbyssEcho.Player.PlayerMotor>());
            SetRef(handsSo, "playerCamera", cam);
            handsSo.ApplyModifiedPropertiesWithoutUndo();

            // 手电筒系统（挂在根节点，控制相机下的光源）
            var flash = root.AddComponent<AbyssEcho.Tools.FlashlightSystem>();
            var flashSo = new SerializedObject(flash);
            SetRef(flashSo, "input", input);
            flashSo.ApplyModifiedPropertiesWithoutUndo();

            // 保存 Prefab
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void SetRef(SerializedObject so, string propName, Object value)
        {
            var prop = so.FindProperty(propName);
            if (prop != null) prop.objectReferenceValue = value;
            else Debug.LogWarning("[PlayerSceneBuilder] 找不到字段：" + propName);
        }

        // ────────────────────────── 测试场景 ──────────────────────────

        private static void BuildTestScene(GameObject playerPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 灯光
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.35f;
            light.color = new Color(0.62f, 0.76f, 1f);
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(52f, -38f, 0f);

            // 环境（低亮度，便于观察手电筒）
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.05f, 0.065f, 0.08f);

            // 分组
            var env = new GameObject("--- ENVIRONMENT ---");
            var props = new GameObject("--- PROPS ---");
            var spawn = new GameObject("--- SPAWN_POINTS ---");

            var groundMat = CreateMat("M_Test_Ground", new Color(0.16f, 0.17f, 0.18f), 0.1f, 0.25f);
            var wallMat = CreateMat("M_Test_Wall", new Color(0.20f, 0.22f, 0.24f), 0.75f, 0.45f);
            var lowMat = CreateMat("M_Test_Low", new Color(0.30f, 0.28f, 0.10f), 0.2f, 0.3f);
            var rampMat = CreateMat("M_Test_Ramp", new Color(0.14f, 0.24f, 0.30f), 0.3f, 0.35f);
            var markerMat = CreateMat("M_Test_Marker", new Color(0.65f, 0.10f, 0.10f), 0.1f, 0.4f);
            var darkMat = CreateMat("M_Test_DarkRoom", new Color(0.05f, 0.05f, 0.06f), 0.2f, 0.2f);

            // 地面
            Box(env.transform, "Ground_40x40", new Vector3(0f, -0.5f, 0f), new Vector3(40f, 1f, 40f), groundMat);

            // 外墙
            Box(env.transform, "Wall_North", new Vector3(0f, 1.5f, 20f), new Vector3(40f, 3f, 0.4f), wallMat);
            Box(env.transform, "Wall_South", new Vector3(0f, 1.5f, -20f), new Vector3(40f, 3f, 0.4f), wallMat);
            Box(env.transform, "Wall_East", new Vector3(20f, 1.5f, 0f), new Vector3(0.4f, 3f, 40f), wallMat);
            Box(env.transform, "Wall_West", new Vector3(-20f, 1.5f, 0f), new Vector3(0.4f, 3f, 40f), wallMat);

            // ── 测试区 1：低矮通道（蹲下）位于 Z+8，净高 1.4m ──
            Box(env.transform, "Tunnel_L", new Vector3(-1.3f, 1.5f, 8f), new Vector3(0.4f, 3f, 6f), lowMat);
            Box(env.transform, "Tunnel_R", new Vector3( 1.3f, 1.5f, 8f), new Vector3(0.4f, 3f, 6f), lowMat);
            Box(env.transform, "Tunnel_Roof", new Vector3(0f, 1.75f, 8f), new Vector3(2.6f, 0.3f, 6f), lowMat);

            // ── 测试区 2：斜坡 15° 位于 X-8 ──
            var ramp = Box(env.transform, "Ramp_15deg", new Vector3(-8f, 0.8f, 5f), new Vector3(4f, 0.3f, 6f), rampMat);
            ramp.transform.rotation = Quaternion.Euler(15f, 0f, 0f);

            // ── 测试区 3：台阶 18cm 位于 X+8 ──
            for (int i = 0; i < 5; i++)
            {
                Box(env.transform, "Step_" + i,
                    new Vector3(8f, 0.09f + i * 0.18f, 4f + i * 0.6f),
                    new Vector3(3f, 0.18f, 0.6f), rampMat);
            }

            // ── 测试区 4：长走廊（奔跑/体力）位于 Z 轴负方向 ──
            Box(env.transform, "Corridor_L", new Vector3(-2.2f, 1.5f, -10f), new Vector3(0.4f, 3f, 20f), wallMat);
            Box(env.transform, "Corridor_R", new Vector3( 2.2f, 1.5f, -10f), new Vector3(0.4f, 3f, 20f), wallMat);

            // ── 测试区 5：暗室（手电筒）位于 X-12 ──
            var darkRoom = new GameObject("DarkRoom");
            darkRoom.transform.SetParent(env.transform);
            Box(darkRoom.transform, "DR_Wall_A", new Vector3(-12f, 1.5f, -4f), new Vector3(0.4f, 3f, 8f), darkMat);
            Box(darkRoom.transform, "DR_Wall_B", new Vector3(-12f, 1.5f, 4f), new Vector3(0.4f, 3f, 8f), darkMat);
            Box(darkRoom.transform, "DR_Wall_C", new Vector3(-16f, 1.5f, 0f), new Vector3(8f, 3f, 0.4f), darkMat);
            Box(darkRoom.transform, "DR_Roof", new Vector3(-14f, 3f, 0f), new Vector3(8f, 0.3f, 8f), darkMat);

            // 参照物
            Box(props.transform, "Marker_Red_Forward", new Vector3(0f, 0.5f, 15f), Vector3.one, markerMat);
            Box(props.transform, "Marker_Green_Left",  new Vector3(-5f, 0.5f, 12f), Vector3.one, CreateMat("M_Test_MarkerG", new Color(0.1f, 0.55f, 0.2f), 0.1f, 0.4f));
            Box(props.transform, "Marker_Blue_Right",  new Vector3( 5f, 0.5f, 12f), Vector3.one, CreateMat("M_Test_MarkerB", new Color(0.1f, 0.25f, 0.7f), 0.1f, 0.4f));

            // ── 出生点 ──
            var sp = new GameObject("SP_Player_Start");
            sp.transform.SetParent(spawn.transform);
            sp.transform.position = new Vector3(0f, 0.05f, -6f);
            sp.transform.rotation = Quaternion.identity;

            // ── 玩家实例 ──
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.position = sp.transform.position;
            player.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            player.name = "Player";

            // ── 调试 HUD ──
            var debugGo = new GameObject("--- DEBUG ---");
            debugGo.AddComponent<AbyssEcho.DebugTools.PlayerDebugHUD>();

            // ── 自动化测试组件（默认不自动运行，需手动勾选 runOnStart 或由测试构建启用）──
            var autoTest = player.AddComponent<AbyssEcho.DebugTools.AutoTest_Stage2>();
            autoTest.runOnStart = false;

            // 烘焙灯光（可选，测试场景用实时即可，避免等待）
            EditorSceneManager.SaveScene(scene, ScenePath);

            // 加入 Build Settings
            AddSceneToBuild(ScenePath);
        }

        private static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (mat != null) r.sharedMaterial = mat;
            return go;
        }

        private static Material CreateMat(string name, Color color, float metallic, float smoothness)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader) { name = name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.color = color;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            return mat;
        }

        private static void AddSceneToBuild(string scenePath)
        {
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (list.Exists(s => s.path == scenePath)) return;
            list.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}