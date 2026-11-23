using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S1 一键初始化：创建 URP 管线资源、Universal Renderer、全局设置，并生成两个基础场景。
    /// 菜单：ABYSS ECHO/S1 初始化管线与场景
    ///
    /// 设计原则：
    ///  - 只创建"不存在"的资源，重复执行安全（幂等）。
    ///  - 不覆盖用户已有资源。
    ///  - 所有路径集中在常量里，方便日后调整。
    /// </summary>
    public static class ProjectBootstrap
    {
        private const string SettingsFolder = "Assets/_Project/Settings";
        private const string ScenesFolder   = "Assets/_Project/Scenes";

        private const string RendererPath   = SettingsFolder + "/URP_Renderer.asset";
        private const string PipelinePath   = SettingsFolder + "/URP_Pipeline.asset";
        private const string GlobalsPath    = SettingsFolder + "/URP_GlobalSettings.asset";

        private const string TemplateScenePath = ScenesFolder + "/_Templates/Template_Empty.unity";
        private const string PlayerTestScenePath = ScenesFolder + "/TEST_Player.unity";
        private const string PersistentScenePath = ScenesFolder + "/Persistent.unity";

        [MenuItem("ABYSS ECHO/S1 初始化管线与场景")]
        public static void Initialize()
        {
            EnsureFolder(SettingsFolder);
            EnsureFolder(ScenesFolder);
            EnsureFolder(ScenesFolder + "/_Templates");

            var rendererData = EnsureRendererAsset();
            var pipelineAsset = EnsurePipelineAsset(rendererData);
            EnsureGlobalSettings();
            AssignGraphicsSettings(pipelineAsset);

            CreateTemplateScene();
            CreatePersistentScene();
            CreatePlayerTestScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<b>[ABYSS ECHO] S1 初始化完成</b>\n" +
                      "管线：" + PipelinePath + "\n" +
                      "渲染器：" + RendererPath + "\n" +
                      "场景：" + TemplateScenePath + "\n         " +
                      PersistentScenePath + "\n         " + PlayerTestScenePath);
        }

        // ────────────────────────────── URP 资源 ──────────────────────────────

        private static UniversalRendererData EnsureRendererAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (existing != null) return existing;

            var data = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(data, RendererPath);
            return data;
        }

        private static UniversalRenderPipelineAsset EnsurePipelineAsset(UniversalRendererData rendererData)
        {
            var existing = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (existing != null) return existing;

            var asset = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(asset, PipelinePath);

            // 第一版基础设置：偏好"稳、暗、清晰"
            asset.shadowDistance = 40f;
            asset.msaaSampleCount = 4;
            asset.supportsHDR = true;
            asset.renderScale = 1.0f;

            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void EnsureGlobalSettings()
        {
            // 2023.1 中 URP 全局设置由 RenderPipelineGlobalSettings 管理。
            // 若未创建，Unity 首次渲染时自动生成；这里不强行创建，避免版本差异导致报错。
            var globals = AssetDatabase.LoadAssetAtPath<RenderPipelineGlobalSettings>(GlobalsPath);
            if (globals != null) Debug.Log("URP 全局设置已存在：" + GlobalsPath);
        }

        private static void AssignGraphicsSettings(UniversalRenderPipelineAsset pipeline)
        {
            GraphicsSettings.defaultRenderPipeline = pipeline;

            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);

            Debug.Log("已将 URP 管线指定到 GraphicsSettings 与全部质量等级。");
        }

        // ────────────────────────────── 场景 ──────────────────────────────

        private static void CreateTemplateScene()
        {
            if (File.Exists(TemplateScenePath)) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.045f, 1f);
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0f, 1.6f, -3f);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.6f;
            light.color = new Color(0.75f, 0.85f, 1f);
            lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            new GameObject("--- ENVIRONMENT ---");
            new GameObject("--- PROPS ---");
            new GameObject("--- INTERACTABLES ---");
            new GameObject("--- AUDIO ---");
            new GameObject("--- HORROR_EVENTS ---");
            new GameObject("--- SPAWN_POINTS ---");
            new GameObject("--- LIGHTING ---");

            EditorSceneManager.SaveScene(scene, TemplateScenePath);
        }

        private static void CreatePersistentScene()
        {
            if (File.Exists(PersistentScenePath)) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var managers = new GameObject("--- MANAGERS ---");
            managers.AddComponent<AbyssEcho.Core.ManagersHost>();

            EditorSceneManager.SaveScene(scene, PersistentScenePath);
        }

        private static void CreatePlayerTestScene()
        {
            if (File.Exists(PlayerTestScenePath)) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 灯光
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.5f;
            light.color = new Color(0.7f, 0.82f, 1f);
            lightGo.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            // 地面（40x40）
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground_40x40";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(40f, 1f, 40f);

            // 四面墙
            CreateWall("Wall_North", new Vector3(0f, 1.5f, 20f), new Vector3(40f, 3f, 0.4f));
            CreateWall("Wall_South", new Vector3(0f, 1.5f, -20f), new Vector3(40f, 3f, 0.4f));
            CreateWall("Wall_East", new Vector3(20f, 1.5f, 0f), new Vector3(0.4f, 3f, 40f));
            CreateWall("Wall_West", new Vector3(-20f, 1.5f, 0f), new Vector3(0.4f, 3f, 40f));

            // 低矮通道（测试蹲下）：两个柱 + 顶板，净高 1.4m
            var tunnel = new GameObject("LowTunnel");
            tunnel.transform.position = new Vector3(0f, 0f, 8f);
            CreateWall("Pillar_L", new Vector3(-1.2f, 1.5f, 0f), new Vector3(0.4f, 3f, 6f), tunnel.transform);
            CreateWall("Pillar_R", new Vector3( 1.2f, 1.5f, 0f), new Vector3(0.4f, 3f, 6f), tunnel.transform);
            CreateWall("Roof",     new Vector3( 0f, 2.1f, 0f), new Vector3(2.4f, 0.4f, 6f), tunnel.transform);

            // 斜坡（测试地面吸附）
            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "Ramp_15deg";
            ramp.transform.position = new Vector3(-8f, 0.8f, 5f);
            ramp.transform.localScale = new Vector3(4f, 0.3f, 6f);
            ramp.transform.rotation = Quaternion.Euler(15f, 0f, 0f);

            // 台阶
            for (int i = 0; i < 5; i++)
            {
                var step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                step.name = "Step_" + i;
                step.transform.position = new Vector3(8f, 0.15f + i * 0.3f, 4f + i * 0.6f);
                step.transform.localScale = new Vector3(3f, 0.3f, 0.6f);
            }

            // 参照物：红色方块（用于判断朝向）
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "Marker_Red_Forward";
            marker.transform.position = new Vector3(0f, 0.5f, 15f);
            marker.transform.localScale = Vector3.one;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.7f, 0.1f, 0.1f);
            marker.GetComponent<MeshRenderer>().sharedMaterial = mat;

            new GameObject("--- SPAWN_POINTS ---").transform.position = Vector3.zero;

            EditorSceneManager.SaveScene(scene, PlayerTestScenePath);
        }

        private static void CreateWall(string name, Vector3 pos, Vector3 scale, Transform parent = null)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.position = pos;
            wall.transform.localScale = scale;
            if (parent != null) wall.transform.SetParent(parent, true);
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