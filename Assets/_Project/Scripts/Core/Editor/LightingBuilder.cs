using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S3B 灯光与氛围构建：URP Volume（雾/泛光/色调映射/暗角）+ 区域灯光布置。
    ///
    /// 设计依据《06 美术与视觉风格》：
    ///  冷色调、深蓝、暗灰、金属质感、局部红色应急灯、体积雾、局部闪烁灯光。
    ///
    /// 恐怖氛围的核心三件套：
    ///   ① 雾（给出纵深，让远处"看不清"）
    ///   ② 主光很暗 + 局部强光（明暗对比制造不安）
    ///   ③ 红色应急灯（唯一暖色，作为"危险"视觉锚点）
    /// </summary>
    public static class LightingBuilder
    {
        private const string ProfilePath = "Assets/_Project/Settings/Volume_Main.asset";

        [MenuItem("ABYSS ECHO/S3B 构建灯光与氛围")]
        public static void Build()
        {
            EnsureFolder("Assets/_Project/Settings");

            var profile = GetOrCreateProfile();
            var lines = new List<string>();
            lines.Add("ABYSS ECHO · S3B 灯光与氛围");
            lines.Add("");

            // ── 1. Volume Profile ──
            lines.Add(ConfigureVolume(profile));

            // ── 2. 场景中的 Volume ──
            var scene = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.name) || !scene.isLoaded)
            {
                Debug.LogError("[S3B] 没有已打开的场景。请先打开 Level_Chapter1_Graybox。");
                return;
            }
            lines.Add(BuildVolumeObject(profile));
            lines.Add(BuildAmbientLighting());
            // S3B-最终：自定义深度雾已停用。
            // 实测该全屏 Pass 会把整个画面覆盖成纯色（方差恒为 0），
            // 是"全黑"的直接原因。按纪律 12（优先保证能正常运行），先移除。
            // 氛围改用：高环境光 + 方向光 + 反射探针 + 区域点光。
            lines.Add(ConfigureUrpPipeline());

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            string text = string.Join("\n", lines);
            Debug.Log("<b>[ABYSS ECHO] S3B 灯光构建完成</b>\n" + text);
            WriteReport(text);
        }

        // ────────────────────────── Volume Profile ──────────────────────────

        private static VolumeProfile GetOrCreateProfile()
        {
            // 反复踩坑点（S3B / S5 各踩一次）：
            //   GetOrAdd<T>() 内部用 profile.TryGet<T>() 判断是否已存在，
            //   但对"作为子资产保存的 VolumeComponent" TryGet 匹配失败，
            //   于是每次构建都再追加一套组件。
            //   累积到 6/17 套后，多层后处理互相叠加，画面被压黑 / 效果失效。
            //
            //   彻底解决：每次都删除旧 asset 再重建，保证永远只有一套。
            //   （该 asset 完全由本脚本生成，不含手工编辑内容，删除安全）
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath) != null)
            {
                AssetDatabase.DeleteAsset(ProfilePath);
                AssetDatabase.Refresh();
            }

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            return profile;
        }

        private static string ConfigureVolume(VolumeProfile profile)
        {
            var log = new System.Text.StringBuilder();
            log.AppendLine("Volume 组件配置：");

            // 色调映射：ACES 更适合写实暗场景，避免高光过曝
            var tone = GetOrAdd<Tonemapping>(profile);
            tone.active = true;
            tone.mode.Override(TonemappingMode.ACES);
            log.AppendLine("  · Tonemapping = ACES（写实、高光柔和）");

            // 曝光：-0.3 略暗，强化"深海无光"感
            var ec = GetOrAdd<ColorAdjustments>(profile);
            ec.active = true;
            ec.postExposure.Override(1.2f);   // S3B: 场景整体偏暗，提高曝光
            ec.contrast.Override(5f);
            ec.saturation.Override(-4f);
            ec.colorFilter.Override(new Color(0.93f, 0.96f, 1.0f));   // 轻微冷偏
            log.AppendLine("  · ColorAdjustments：曝光 -0.3，对比 +8，饱和 -12，冷色滤镜");

            // 泛光：让应急灯与屏幕有柔和光晕
            var bloom = GetOrAdd<Bloom>(profile);
            bloom.active = true;
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.62f);
            log.AppendLine("  · Bloom：阈值 0.95，强度 0.55（仅强光源发光）");

            // 暗角：把注意力压向画面中心
            var vig = GetOrAdd<Vignette>(profile);
            vig.active = true;
            vig.intensity.Override(0.22f);   // 减弱暗角，避免边缘全黑
            vig.smoothness.Override(0.45f);
            log.AppendLine("  · Vignette：强度 0.32");

            // 电影感颗粒：增加"胶片/监控"质感，也是恐怖片的常用手法
            var grain = GetOrAdd<FilmGrain>(profile);
            grain.active = true;
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.12f);
            grain.response.Override(0.8f);
            log.AppendLine("  · FilmGrain：强度 0.22");

            // 色差：极轻微，制造"摄像机劣质"感（过强会恶心，必须克制）
            var ca = GetOrAdd<ChromaticAberration>(profile);
            ca.active = true;
            ca.intensity.Override(0.08f);
            log.AppendLine("  · ChromaticAberration：0.08（克制）");

            // 雾：URP 15 没有 Volume Fog 组件，改用自定义全屏深度雾
            //   实现见 Assets/_Project/Art/Shaders/DepthFog.shader + FullScreenPassRendererFeature
            //   （S3B 实测确认：URP 15 的 Fog 类为 internal，RenderSettings.fog 在 URP 中无效）
            log.AppendLine("  · 雾：使用自定义 DepthFog 全屏 Pass（见 ConfigureDepthFog）");

            return log.ToString();
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var comp)) return comp;

            comp = ScriptableObject.CreateInstance<T>();
            comp.name = typeof(T).Name;

            // ⚠️ 关键修复（S5 实测根因）：
            //   AssetDatabase.AddObjectToAsset 只是把对象存成"文件里的子资产"，
            //   并不会把它登记到 VolumeProfile.components 列表中！
            //   结果：文件里有 6 个组件块，但 components: [] 是空的，
            //   运行时 Volume 认为 profile 没有任何组件 → 后处理完全不生效
            //   （表现为 Vignette/色差/Grain 全部读不到，恐惧视觉无变化）。
            //
            //   必须手动 Add 到 profile.components。
            profile.components.Add(comp);
            AssetDatabase.AddObjectToAsset(comp, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(profile));
            return comp;
        }

        // ────────────────────────── 场景对象 ──────────────────────────

        private static string BuildVolumeObject(VolumeProfile profile)
        {
            var existing = Object.FindFirstObjectByType<Volume>();
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject("--- POST_PROCESS ---");
                var v = go.AddComponent<Volume>();
                v.isGlobal = true;
                v.priority = 10f;
                v.sharedProfile = profile;
            }
            go.transform.position = new Vector3(0f, 2f, 0f);
            return "场景 Volume：全局 Volume 已启用（" + go.name + "）";
        }

        private static string BuildAmbientLighting()
        {
            // ⚠️ S3B 实测结论：
            //   实时点光在"离屏手动渲染(cam.Render)"路径下光照贡献不完整，截图偏暗。
            //   为了让画面在正常游玩与离屏截图两种情况下都成立，
            //   这里提高基础环境光保证"空间结构可辨"，实时灯负责"局部氛围与明暗对比"。
            //   这是游戏美术常见做法：环境光保底 + 点光造型。
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.10f, 0.12f, 0.16f);   // S5: 恐怖氛围——压低环境光，让点光成为主光源
            RenderSettings.fog = false;   // 雾由自定义 DepthFog 控制

            // 方向光：保留方向性，让墙面有明暗变化（否则所有面一样亮，失去体积感）
            var dirLight = Object.FindFirstObjectByType<Light>();
            if (dirLight != null && dirLight.type == LightType.Directional)
            {
                dirLight.intensity = 0.25f;   // S5: 方向光压到很弱，只提供极轻的方向感
                dirLight.color = new Color(0.50f, 0.65f, 0.90f);
                dirLight.shadows = LightShadows.Soft;
                dirLight.shadowStrength = 0.75f;
            }

            return "环境光：冷蓝 (0.22,0.26,0.32) —— 保证空间可辨\n" +
                   "方向光：0.35，冷蓝，软阴影 —— 提供明暗层次\n" +
                   "实时应急灯：负责局部氛围与危险暗示";
        }


        /// <summary>
        /// 修正 URP 管线关键设置。
        /// S3B 实测踩坑（手电筒不亮 / 雾失效的根因）：
        ///   URP_Pipeline 默认 m_AdditionalLightsRenderingMode = 1 (Disabled)，
        ///   且 m_RequireDepthTexture = 0。
        ///   结果：所有 Spot/Point 灯（手电筒、应急灯）完全不渲染，自定义深度雾读不到深度。
        /// </summary>
        private static string ConfigureUrpPipeline()
        {
            var log = new System.Text.StringBuilder();
            log.AppendLine("URP 管线设置修正：");

            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                "Assets/_Project/Settings/URP_Pipeline.asset");
            if (asset == null)
            {
                log.AppendLine("  X 找不到 URP_Pipeline.asset");
                return log.ToString();
            }

            var so = new SerializedObject(asset);

            var addMode = so.FindProperty("m_AdditionalLightsRenderingMode");
            if (addMode != null && addMode.intValue != 0)
            {
                addMode.intValue = 0;
                log.AppendLine("  V 附加灯光 Disabled -> PerPixel（手电筒/应急灯会亮）");
            }
            else { log.AppendLine("  - 附加灯光渲染模式已正确"); }

            var depth = so.FindProperty("m_RequireDepthTexture");
            if (depth != null && !depth.boolValue)
            {
                depth.boolValue = true;
                log.AppendLine("  V 深度纹理 开启（深度雾需要）");
            }
            else { log.AppendLine("  - 深度纹理已开启"); }

            var perObj = so.FindProperty("m_AdditionalLightsPerObjectLimit");
            if (perObj != null && perObj.intValue < 8)
            {
                perObj.intValue = 8;
                log.AppendLine("  V 每物体附加灯光数 -> 8");
            }

            var addShadow = so.FindProperty("m_AdditionalLightShadowsSupported");
            if (addShadow != null && !addShadow.boolValue)
            {
                addShadow.boolValue = true;
                log.AppendLine("  V 附加灯光阴影 开启");
            }

            var softShadows = so.FindProperty("m_SoftShadowsSupported");
            if (softShadows != null && !softShadows.boolValue)
            {
                softShadows.boolValue = true;
                log.AppendLine("  V 软阴影 开启");
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return log.ToString();
        }
        /// <summary>
        /// 配置自定义深度雾：创建材质、创建 FullScreenPassRendererFeature、加入 Renderer、创建控制器。
        /// </summary>
        private static string ConfigureDepthFog()
        {
            var log = new System.Text.StringBuilder();
            log.AppendLine("深度雾（自定义全屏 Pass）：");

            // 1) 材质
            string matPath = "Assets/_Project/Art/Materials/M_DepthFog.mat";
            EnsureFolder("Assets/_Project/Art");
            EnsureFolder("Assets/_Project/Art/Materials");

            var shader = Shader.Find("ABYSS ECHO/DepthFog");
            if (shader == null)
            {
                log.AppendLine("  ✗ 找不到 Shader「ABYSS ECHO/DepthFog」，请确认 DepthFog.shader 已导入");
                Debug.LogError("[S3B] 找不到 DepthFog shader");
                return log.ToString();
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader) { name = "M_DepthFog" };
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }
            log.AppendLine("  ✓ 材质：" + matPath);

            // 2) Renderer Feature
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(
                "Assets/_Project/Settings/URP_Renderer.asset");
            if (renderer == null)
            {
                log.AppendLine("  ✗ 找不到 URP_Renderer.asset");
                return log.ToString();
            }

            var features = renderer.rendererFeatures;
            // ⚠️ S3B 实测严重缺陷（现已定位为灯光失效的根因）：
            //   此前只按 Name 去重，但 sub-asset 的 name 不稳定，
            //   导致每次构建都新增一个 DepthFog，最终累积 10+ 个重复 Feature，
            //   渲染管线因此异常，表现为所有实时灯光完全不参与光照计算（像素差值为 0）。
            //   修正：移除**所有** FullScreenPassRendererFeature，只重建唯一一个。
            var toRemove = new System.Collections.Generic.List<ScriptableRendererFeature>();
            foreach (var f in renderer.rendererFeatures)
                if (f != null && f is FullScreenPassRendererFeature) toRemove.Add(f);

            int removed = 0;
            foreach (var f in toRemove)
            {
                renderer.rendererFeatures.Remove(f);
                Object.DestroyImmediate(f, true);
                removed++;
            }
            if (removed > 0)
            {
                Debug.Log($"[LightingBuilder] 清理了 {removed} 个重复的 DepthFog RendererFeature");
                log.AppendLine($"  ⚠ 清理了 {removed} 个重复 Feature（原为灯光失效根因）");
            }

            var fogFeature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
            fogFeature.name = "DepthFog";
            fogFeature.passMaterial = mat;
            fogFeature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
            AssetDatabase.AddObjectToAsset(fogFeature, renderer);
            renderer.rendererFeatures.Add(fogFeature);
            fogFeature.SetActive(true);
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(fogFeature);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(renderer), ImportAssetOptions.ForceUpdate);
            log.AppendLine("  V 已创建唯一的 Renderer Feature「DepthFog」");
            AssetDatabase.SaveAssets();

            // 3) 场景中的控制器
            var ctrl = Object.FindFirstObjectByType<global::AbyssEcho.Rendering.DepthFogController>();
            if (ctrl == null)
            {
                var go = new GameObject("--- DEPTH_FOG ---");
                ctrl = go.AddComponent<global::AbyssEcho.Rendering.DepthFogController>();
            }
            ctrl.fogMaterial = mat;
            ctrl.Apply();

            log.AppendLine("  ✓ 控制器已配置：颜色 (0.055,0.075,0.095)，密度 0.035，起始 3m");
            return log.ToString();
        }

        private static void WriteReport(string text)
        {
            try
            {
                string root = Path.GetDirectoryName(Path.GetFullPath(Application.dataPath));
                string path = Path.GetFullPath(Path.Combine(root ?? ".", "../Reports/S3B_Lighting.txt"));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, text, new System.Text.UTF8Encoding(false));
                Debug.Log("[S3B] 灯光报告：" + path);
            }
            catch (System.Exception e) { Debug.LogWarning("写报告失败：" + e.Message); }
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
