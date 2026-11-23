using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S67：第一章统一 PBR 材质库构建器。
    ///
    /// ══════════════════════ 设计原则 ══════════════════════
    ///
    ///   ① 用 URP/Lit，接完整 PBR 贴图组（Color / Normal / Roughness / Metallic / AO）
    ///   ② 同一张贴图可以被多个材质复用，但**参数不同**（Metallic、Smoothness、Tiling、色调）
    ///      —— 满足"金属表面不能全部使用相同的 Metallic 和 Smoothness"
    ///   ③ 金属/非金属分开：非金属 Metallic=0，金属 Metallic≈1
    ///   ④ 发光材质必须有 Emission（不是只改 BaseColor）
    ///   ⑤ Tiling 按物体实际尺寸设定，避免拉伸
    ///
    /// ══════════════════════ 贴图来源 ══════════════════════
    ///
    ///   全部来自项目内已有的 ambientCG CC0 素材（2048²）：
    ///     MetalPlates013  工业铆接钢板
    ///     DiamondPlate009 花纹防滑钢板
    ///     Metal027        金属
    ///     Concrete048     混凝土
    ///     Pipe001         管道
    ///     Tiles141        瓷砖
    ///
    /// ══════════════════════ 幂等 ══════════════════════
    ///
    ///   已存在的材质会被就地更新参数，不会删除重建（避免丢失场景引用）。
    /// </summary>
    public static class Ch1MaterialLibraryBuilder
    {
        private const string Root = "Assets/Art/Materials";
        private const string TexRoot = "Assets/_Project/Art/Textures/ambientCG";

        private struct TexSet
        {
            public Texture2D Color;
            public Texture2D Normal;
            public Texture2D Rough;      // 作为 MetallicGlossMap 的 G 通道参考
            public Texture2D Metal;
            public Texture2D AO;
        }

        private static readonly Dictionary<string, TexSet> _cache = new Dictionary<string, TexSet>();

        [MenuItem("ABYSS ECHO/美术 v8.0/构建第一章材质库")]
        public static void Build() => Run();

        public static void Run()
        {
            Log("══════ 构建第一章材质库 ══════");

            var metalPlates = LoadSet("MetalPlates013");
            var diamond = LoadSet("DiamondPlate009");
            var metal = LoadSet("Metal027");
            var concrete = LoadSet("Concrete048");
            var pipe = LoadSet("Pipe001");
            var tiles = LoadSet("Tiles141");

            int created = 0, updated = 0;

            // ══════════════════ 建筑 Architecture ══════════════════
            //
            // 深海基地的墙是"冷蓝灰、低饱和、有使用痕迹"，不是亮金属。
            // 钢板贴图 + 低 Metallic（漆面覆盖）+ 中高粗糙。

            created += MakeMat($"{Root}/Architecture/M_Arch_WallSteel",
                metalPlates, new Color(0.42f, 0.47f, 0.52f),     // 冷蓝灰
                metallic: 0.55f, smooth: 0.32f, tiling: 2f,
                normalScale: 1.0f, note: "工业墙面金属 · 冷蓝灰漆面钢板");

            created += MakeMat($"{Root}/Architecture/M_Arch_WallPanel",
                metalPlates, new Color(0.34f, 0.38f, 0.43f),
                metallic: 0.72f, smooth: 0.44f, tiling: 1.4f,
                normalScale: 1.15f, note: "工业墙面金属 · 深蓝灰露钢面");

            created += MakeMat($"{Root}/Architecture/M_Arch_FloorPlate",
                diamond, new Color(0.30f, 0.33f, 0.36f),          // 深灰
                metallic: 0.85f, smooth: 0.30f, tiling: 3f,
                normalScale: 1.3f, note: "防滑地面 · 花纹钢板");

            created += MakeMat($"{Root}/Architecture/M_Arch_FloorConcrete",
                concrete, new Color(0.33f, 0.34f, 0.35f),
                metallic: 0.0f, smooth: 0.18f, tiling: 2.5f,
                normalScale: 1.0f, note: "防滑地面 · 湿混凝土");

            created += MakeMat($"{Root}/Architecture/M_Arch_Ceiling",
                metalPlates, new Color(0.26f, 0.29f, 0.33f),       // 更暗
                metallic: 0.60f, smooth: 0.24f, tiling: 2.2f,
                normalScale: 0.9f, note: "金属天花板 · 暗钢");

            created += MakeMat($"{Root}/Architecture/M_Arch_Tile",
                tiles, new Color(0.46f, 0.50f, 0.54f),
                metallic: 0.05f, smooth: 0.52f, tiling: 2f,
                normalScale: 1.0f, note: "瓷砖墙面 · 医疗区");

            // 厚重舱门：比普通墙更暗、更粗、粗糙度更高（不像新抛光金属）
            created += MakeMat($"{Root}/Architecture/M_Arch_BlastDoor",
                metalPlates, new Color(0.22f, 0.25f, 0.29f),
                metallic: 0.90f, smooth: 0.38f, tiling: 1.0f,
                normalScale: 1.5f, note: "厚重舱门 · 深色厚钢板");

            // ══════════════════ 机械 Machinery ══════════════════

            created += MakeMat($"{Root}/Machinery/M_Mach_BrushedSteel",
                metal, new Color(0.62f, 0.66f, 0.70f),
                metallic: 0.95f, smooth: 0.62f, tiling: 2f,
                normalScale: 0.6f, note: "拉丝钢材 · 高反射");

            created += MakeMat($"{Root}/Machinery/M_Mach_DarkPainted",
                metal, new Color(0.16f, 0.18f, 0.21f),
                metallic: 0.35f, smooth: 0.28f, tiling: 1.8f,
                normalScale: 0.8f, note: "深色喷漆金属 · 设备外壳");

            created += MakeMat($"{Root}/Machinery/M_Mach_Rubber",
                concrete, new Color(0.09f, 0.09f, 0.10f),          // 近黑
                metallic: 0.0f, smooth: 0.14f, tiling: 2.5f,
                normalScale: 0.7f, note: "橡胶 · 软管/减震垫");

            created += MakeMat($"{Root}/Machinery/M_Mach_Pipe",
                pipe, new Color(0.44f, 0.46f, 0.48f),
                metallic: 0.88f, smooth: 0.42f, tiling: 1.2f,
                normalScale: 1.1f, note: "管道金属");

            created += MakeMat($"{Root}/Machinery/M_Mach_WornMetal",
                metalPlates, new Color(0.38f, 0.36f, 0.33f),       // 偏黄褐，像锈蚀
                metallic: 0.80f, smooth: 0.20f, tiling: 2.4f,
                normalScale: 1.4f, note: "磨损金属 · 锈蚀划痕");

            // ══════════════════ 科技 Technology ══════════════════

            // 发光材质：必须有 Emission
            created += MakeEmissive($"{Root}/Technology/M_Tech_ScreenBlue",
                new Color(0.02f, 0.10f, 0.18f),
                new Color(0.10f, 0.65f, 1.00f), intensity: 2.2f,
                note: "蓝色显示屏 · 自发光");

            created += MakeEmissive($"{Root}/Technology/M_Tech_GlowCyan",
                new Color(0.02f, 0.14f, 0.16f),
                new Color(0.15f, 0.95f, 1.00f), intensity: 3.0f,
                note: "青色发光材质");

            created += MakeEmissive($"{Root}/Technology/M_Tech_GlowRed",
                new Color(0.16f, 0.01f, 0.01f),
                new Color(1.00f, 0.12f, 0.06f), intensity: 2.6f,
                note: "红色警示发光材质");

            created += MakeEmissive($"{Root}/Technology/M_Tech_IndicatorGreen",
                new Color(0.02f, 0.12f, 0.04f),
                new Color(0.20f, 1.00f, 0.35f), intensity: 2.0f,
                note: "指示灯 · 绿色");

            // 玻璃：透明 Surface，正确渲染队列
            created += MakeGlass($"{Root}/Technology/M_Tech_Glass",
                new Color(0.62f, 0.74f, 0.78f, 0.16f),
                smooth: 0.94f, note: "玻璃 · 观察窗");

            created += MakeGlass($"{Root}/Technology/M_Tech_GlassFrosted",
                new Color(0.55f, 0.62f, 0.66f, 0.42f),
                smooth: 0.55f, note: "磨砂玻璃");

            // 设备外壳：介于喷漆和拉丝之间
            created += MakeMat($"{Root}/Machinery/M_Mach_DeviceShell",
                metal, new Color(0.28f, 0.31f, 0.35f),
                metallic: 0.62f, smooth: 0.46f, tiling: 1.6f,
                normalScale: 0.75f, note: "设备外壳 · 控制台");

            // ══════════════════ 道具 Props ══════════════════

            created += MakeMat($"{Root}/Props/M_Prop_Plastic",
                concrete, new Color(0.42f, 0.44f, 0.46f),
                metallic: 0.0f, smooth: 0.38f, tiling: 3f,
                normalScale: 0.5f, note: "塑料 · 器材");

            created += MakeMat($"{Root}/Props/M_Prop_Ceramic",
                tiles, new Color(0.68f, 0.70f, 0.70f),
                metallic: 0.0f, smooth: 0.68f, tiling: 3f,
                normalScale: 0.5f, note: "陶瓷 · 绝缘件");

            created += MakeMat($"{Root}/Props/M_Prop_Wood",
                concrete, new Color(0.24f, 0.17f, 0.11f),          // 深褐
                metallic: 0.0f, smooth: 0.22f, tiling: 2.5f,
                normalScale: 0.9f, note: "木质 · 家具");

            created += MakeMat($"{Root}/Props/M_Prop_Fabric",
                concrete, new Color(0.20f, 0.19f, 0.18f),
                metallic: 0.0f, smooth: 0.10f, tiling: 4f,
                normalScale: 0.8f, note: "布料 · 织物");

            created += MakeMat($"{Root}/Props/M_Prop_Crate",
                metalPlates, new Color(0.40f, 0.38f, 0.32f),
                metallic: 0.45f, smooth: 0.26f, tiling: 1.5f,
                normalScale: 1.2f, note: "货箱 · 磨损金属箱体");

            created += MakeMat($"{Root}/Props/M_Prop_Dark",
                metal, new Color(0.10f, 0.11f, 0.12f),
                metallic: 0.30f, smooth: 0.20f, tiling: 2f,
                normalScale: 0.7f, note: "深色通用");

            created += MakeMat($"{Root}/Props/M_Prop_White",
                concrete, new Color(0.72f, 0.74f, 0.74f),
                metallic: 0.0f, smooth: 0.35f, tiling: 2.5f,
                normalScale: 0.5f, note: "白色通用 · 医疗器械");

            created += MakeMat($"{Root}/Props/M_Prop_Metal",
                metal, new Color(0.48f, 0.52f, 0.56f),
                metallic: 0.88f, smooth: 0.52f, tiling: 1.8f,
                normalScale: 0.8f, note: "通用金属 · 道具默认");

            // 警示条纹：用高饱和黄黑（贴图用 DiamondPlate 提供凹凸）
            created += MakeMat($"{Root}/Props/M_Prop_HazardStripe",
                diamond, new Color(0.72f, 0.55f, 0.08f),
                metallic: 0.25f, smooth: 0.34f, tiling: 2.2f,
                normalScale: 1.0f, note: "警示条纹 · 黄");

            created += MakeMat($"{Root}/Props/M_Prop_NotePaper",
                concrete, new Color(0.82f, 0.79f, 0.70f),
                metallic: 0.0f, smooth: 0.16f, tiling: 1f,
                normalScale: 0.3f, note: "纸张 · 记录");

            // ══════════════════ 有机 Organic ══════════════════
            //
            //  用 Concrete 贴图做粗糙起伏，颜色压到暗绿/暗紫，
            //  高粗糙度 + 低金属 = 湿润有机物（不是金属）

            created += MakeMat($"{Root}/Organic/M_Org_WetFlesh",
                concrete, new Color(0.18f, 0.09f, 0.10f),          // 暗红褐
                metallic: 0.0f, smooth: 0.62f, tiling: 1.6f,
                normalScale: 1.6f, note: "湿润有机组织");

            created += MakeMat($"{Root}/Organic/M_Org_DarkSkin",
                concrete, new Color(0.09f, 0.10f, 0.09f),          // 近黑绿
                metallic: 0.0f, smooth: 0.48f, tiling: 2.0f,
                normalScale: 1.4f, note: "暗色生物皮肤");

            created += MakeMat($"{Root}/Organic/M_Org_Slime",
                concrete, new Color(0.11f, 0.16f, 0.12f),          // 暗绿
                metallic: 0.0f, smooth: 0.86f, tiling: 2.2f,
                normalScale: 0.8f, note: "黏液表面 · 高光滑");

            Log($"材质创建/更新完成：{created} 个新建，{updated} 个已存在");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            VerifyLibrary();
        }

        // ══════════════════════════════════════════════════════════

        private static TexSet LoadSet(string name)
        {
            if (_cache.TryGetValue(name, out var cached)) return cached;

            string dir = $"{TexRoot}/{name}";
            var set = new TexSet
            {
                Color = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{name}_2K-JPG_Color.jpg"),
                Normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{name}_2K-JPG_NormalGL.jpg"),
                Rough = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{name}_2K-JPG_Roughness.jpg"),
                Metal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{name}_2K-JPG_Metalness.jpg"),
                AO = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{name}_2K-JPG_AmbientOcclusion.jpg"),
            };
            _cache[name] = set;

            Log($"  贴图组 {name}: Color={set.Color != null} Normal={set.Normal != null} " +
                $"Rough={set.Rough != null} Metal={set.Metal != null} AO={set.AO != null}");
            return set;
        }

        /// <summary>创建/更新一个标准 URP/Lit PBR 材质。</summary>
        private static int MakeMat(string path, TexSet tex, Color tint,
                                    float metallic, float smooth, float tiling,
                                    float normalScale, string note)
        {
            bool isNew = false;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(WithMatExt(path));
            if (mat == null)
            {
                EnsureDir(path);
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, WithMatExt(path));
                isNew = true;
            }

            // Base
            mat.SetTexture("_BaseMap", tex.Color);
            mat.SetColor("_BaseColor", tint);
            mat.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));

            // Normal（必须启用 _NORMALMAP 关键字，否则不生效）
            if (tex.Normal != null)
            {
                mat.SetTexture("_BumpMap", tex.Normal);
                mat.SetTextureScale("_BumpMap", new Vector2(tiling, tiling));
                mat.SetFloat("_BumpScale", normalScale);
                mat.EnableKeyword("_NORMALMAP");
            }

            // Metallic / Smoothness
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smooth);

            // 金属贴图：作为 MetallicGlossMap 使用会让 Metallic 完全由贴图决定，
            // 这里刻意**不用**，改成用标量 Metallic 让每种材质的金属度可控可区分。
            // （任务要求"金属表面不能全部使用相同的 Metallic 和 Smoothness"）

            // AO
            if (tex.AO != null)
            {
                mat.SetTexture("_OcclusionMap", tex.AO);
                mat.SetTextureScale("_OcclusionMap", new Vector2(tiling, tiling));
                mat.SetFloat("_OcclusionStrength", 0.85f);
            }

            // 不透明
            mat.SetFloat("_Surface", 0f);
            mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Opaque");
            mat.renderQueue = -1;

            EditorUtility.SetDirty(mat);
            Log($"  {(isNew ? "+" : "=")} {Path.GetFileName(path),-28} {note}");
            if (isNew) _newCount++;
            return isNew ? 1 : 0;
        }

        /// <summary>创建发光材质（Emission 必须有）。</summary>
        private static int MakeEmissive(string path, Color baseCol, Color emitCol,
                                         float intensity, string note)
        {
            bool isNew = false;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(WithMatExt(path));
            if (mat == null)
            {
                EnsureDir(path);
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, WithMatExt(path));
                isNew = true;
            }

            mat.SetColor("_BaseColor", baseCol);
            mat.SetFloat("_Metallic", 0.1f);
            mat.SetFloat("_Smoothness", 0.72f);

            // Emission：URP/Lit 需要开 _EMISSION 关键字
            mat.SetColor("_EmissionColor", emitCol * intensity);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            mat.SetFloat("_Surface", 0f);
            mat.SetOverrideTag("RenderType", "Opaque");
            mat.renderQueue = -1;

            EditorUtility.SetDirty(mat);
            Log($"  {(isNew ? "+" : "=")} {Path.GetFileName(path),-28} {note}");
            if (isNew) _newCount++;
            return isNew ? 1 : 0;
        }

        /// <summary>创建玻璃材质（透明 + 正确的渲染队列）。</summary>
        private static int MakeGlass(string path, Color col, float smooth, string note)
        {
            bool isNew = false;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(WithMatExt(path));
            if (mat == null)
            {
                EnsureDir(path);
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, WithMatExt(path));
                isNew = true;
            }

            mat.SetColor("_BaseColor", col);
            mat.SetFloat("_Metallic", 0.0f);
            mat.SetFloat("_Smoothness", smooth);

            // URP 透明：_Surface=1, _Blend=0(Alpha), 队列 Transparent
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.SetInt("_ZWrite", 0);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;

            EditorUtility.SetDirty(mat);
            Log($"  {(isNew ? "+" : "=")} {Path.GetFileName(path),-28} {note}");
            if (isNew) _newCount++;
            return isNew ? 1 : 0;
        }

        private static int _newCount;

        /// <summary>确保路径以 .mat 结尾（AssetDatabase.CreateAsset 需要扩展名）。</summary>
        private static string WithMatExt(string p)
            => p.EndsWith(".mat", System.StringComparison.OrdinalIgnoreCase) ? p : p + ".mat";

        private static void EnsureDir(string assetPath)
        {
            string dir = Path.GetDirectoryName(assetPath).Replace("\\", "/");
            if (!AssetDatabase.IsValidFolder(dir))
            {
                var parts = dir.Split('/');
                string cur = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = cur + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(cur, parts[i]);
                    cur = next;
                }
            }
        }

        private static void VerifyLibrary()
        {
            Log("---- 材质库清单 ----");
            var guids = AssetDatabase.FindAssets("t:Material", new[] { Root });
            Log($"Assets/Art/Materials 下共 {guids.Length} 个材质");

            int missingTex = 0, emissive = 0, transparent = 0;
            foreach (var g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m == null) continue;

                bool hasBase = m.GetTexture("_BaseMap") != null;
                bool hasEmit = m.IsKeywordEnabled("_EMISSION") &&
                               m.GetColor("_EmissionColor").maxColorComponent > 0.01f;
                bool isTrans = m.GetFloat("_Surface") > 0.5f;
                if (!hasBase && !hasEmit) missingTex++;
                if (hasEmit) emissive++;
                if (isTrans) transparent++;

                Log($"  {Path.GetFileName(p),-30} base={(hasBase ? "有" : "-")} " +
                    $"emit={(hasEmit ? "有" : "-")} trans={(isTrans ? "是" : "-")} " +
                    $"metal={m.GetFloat("_Metallic"):F2} smooth={m.GetFloat("_Smoothness"):F2}");
            }
            Log($"缺贴图(且非发光) = {missingTex}   发光 = {emissive}   透明 = {transparent}");
        }

        private static void Log(string msg)
        {
            string line = $"[Ch1Mat] {msg}";
            Debug.Log(line);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.AppendAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch1MaterialLibrary_Result.txt",
                                   line + "\n", new UTF8Encoding(false));
            }
            catch { }
        }
    }
}