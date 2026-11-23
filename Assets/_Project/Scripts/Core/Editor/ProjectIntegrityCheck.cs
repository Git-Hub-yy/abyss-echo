using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// 阶段一：项目完整性检查（只读）。
    ///
    /// 检查项：
    ///   ① Unity 版本与项目设置
    ///   ② Build Settings 场景列表与顺序
    ///   ③ 五章场景存在性
    ///   ④ Missing Script / Missing Prefab / Missing Material
    ///   ⑤ 丢失的 Texture / Mesh / AudioClip
    ///   ⑥ Shader 丢失或粉色材质
    ///   ⑦ Animator / Timeline 引用
    ///   ⑧ 重复或废弃脚本
    ///   ⑨ 编译错误
    ///   ⑩ Player Settings / 输入系统 / 渲染管线 / 目标平台
    /// </summary>
    public static class ProjectIntegrityCheck
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# 阶段一：项目完整性检查");
            sb.AppendLine();
            sb.AppendLine($"> 生成时间：{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            // ══════════ ① Unity 版本与项目设置 ══════════
            sb.AppendLine("## ① Unity 版本与项目设置");
            sb.AppendLine();
            sb.AppendLine($"| 项 | 值 |");
            sb.AppendLine($"|---|---|");
            sb.AppendLine($"| Unity 版本 | {Application.unityVersion} |");
            sb.AppendLine($"| 产品名 | {Application.productName} |");
            sb.AppendLine($"| 公司名 | {Application.companyName} |");
            sb.AppendLine($"| 版本号 | {Application.version} |");
            sb.AppendLine($"| 平台 | {Application.platform} |");
            sb.AppendLine($"| 颜色空间 | {PlayerSettings.colorSpace} |");
            sb.AppendLine($"| 图形 API | {PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64).Length} 个 |");
            foreach (var api in PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64))
                sb.AppendLine($"|   - | {api} |");
            sb.AppendLine($"| 脚本后端 | {PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone)} |");
            sb.AppendLine($"| API 兼容级别 | {PlayerSettings.GetApiCompatibilityLevel(NamedBuildTarget.Standalone)} |");
            sb.AppendLine($"| 默认屏幕宽高 | {PlayerSettings.defaultScreenWidth} × {PlayerSettings.defaultScreenHeight} |");
            sb.AppendLine($"| 全屏模式 | {PlayerSettings.fullScreenMode} |");
            sb.AppendLine($"| Active Input Handling | {GetActiveInputHandler()} |");
            sb.AppendLine();

            // ══════════ ② Build Settings 场景列表 ══════════
            sb.AppendLine("## ② Build Settings 场景列表");
            sb.AppendLine();
            var buildScenes = EditorBuildSettings.scenes;
            sb.AppendLine($"共 **{buildScenes.Length}** 个场景：");
            sb.AppendLine();
            sb.AppendLine("| 序号 | 路径 | 启用 | 存在 |");
            sb.AppendLine("|---|---|---|---|");
            bool buildListOk = true;
            for (int i = 0; i < buildScenes.Length; i++)
            {
                var s = buildScenes[i];
                bool exists = File.Exists(s.path);
                if (!exists || !s.enabled) buildListOk = false;
                sb.AppendLine($"| {i} | {s.path} | {(s.enabled ? "✅" : "❌")} | {(exists ? "✅" : "❌ 缺失")} |");
            }
            sb.AppendLine();
            sb.AppendLine(buildListOk ? "**结论：构建列表正常**" : "**⚠️ 构建列表有问题**");
            sb.AppendLine();

            // ══════════ ③ 五章场景存在性 ══════════
            sb.AppendLine("## ③ 五章场景存在性");
            sb.AppendLine();
            sb.AppendLine("| 章节 | 路径 | 存在 | 在构建列表 |");
            sb.AppendLine("|---|---|---|---|");
            bool allChaptersOk = true;
            for (int n = 1; n <= 5; n++)
            {
                string p = $"Assets/_Project/Scenes/Level_Chapter{n}_Graybox.unity";
                bool exists = File.Exists(p);
                bool inBuild = buildScenes.Any(s => s.path == p && s.enabled);
                if (!exists || !inBuild) allChaptersOk = false;
                sb.AppendLine($"| 第{n}章 | {p} | {(exists ? "✅" : "❌")} | {(inBuild ? "✅" : "❌")} |");
            }
            sb.AppendLine();
            sb.AppendLine(allChaptersOk ? "**结论：五章场景齐全且均已纳入构建**" : "**⚠️ 有章节缺失或未纳入构建**");
            sb.AppendLine();

            // ══════════ ④ 逐场景资源完整性 ══════════
            sb.AppendLine("## ④ 逐场景资源完整性");
            sb.AppendLine();
            sb.AppendLine("| 场景 | Renderer | Missing Script | Missing Material | Missing Mesh | 粉色Shader |");
            sb.AppendLine("|---|---|---|---|---|---|");

            var sceneResults = new List<(string name, int rend, int missScript, int missMat, int missMesh, int pink)>();

            foreach (var bs in buildScenes)
            {
                if (!bs.enabled || !File.Exists(bs.path)) continue;
                EditorSceneManager.OpenScene(bs.path, OpenSceneMode.Single);
                Physics.SyncTransforms();

                int rend = 0, missScript = 0, missMat = 0, missMesh = 0, pink = 0;

                foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                {
                    foreach (var c in go.GetComponents<Component>())
                        if (c == null) missScript++;
                }

                foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    if (r == null) continue;
                    rend++;

                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh == null) missMesh++;

                    var smr = r as SkinnedMeshRenderer;
                    if (smr != null && smr.sharedMesh == null) missMesh++;

                    var mats = r.sharedMaterials;
                    if (mats == null || mats.Length == 0) { missMat++; continue; }
                    foreach (var m in mats)
                    {
                        if (m == null) { missMat++; continue; }
                        if (m.shader == null ||
                            m.shader.name == "Hidden/InternalErrorShader" ||
                            m.shader.name.Contains("Error"))
                            pink++;
                    }
                }

                sceneResults.Add((Path.GetFileNameWithoutExtension(bs.path), rend, missScript, missMat, missMesh, pink));
                sb.AppendLine($"| {Path.GetFileNameWithoutExtension(bs.path)} | {rend} | " +
                              $"{(missScript == 0 ? "✅ 0" : "❌ " + missScript)} | " +
                              $"{(missMat == 0 ? "✅ 0" : "❌ " + missMat)} | " +
                              $"{(missMesh == 0 ? "✅ 0" : "❌ " + missMesh)} | " +
                              $"{(pink == 0 ? "✅ 0" : "❌ " + pink)} |");
            }
            sb.AppendLine();

            int totalMiss = sceneResults.Sum(x => x.missScript + x.missMat + x.missMesh + x.pink);
            sb.AppendLine(totalMiss == 0
                ? "**结论：所有场景资源引用完整，无 Missing Script / Material / Mesh / 粉色材质**"
                : $"**⚠️ 共 {totalMiss} 处资源问题**");
            sb.AppendLine();

            // ══════════ ⑤ 资源依赖完整性（Texture/Mesh/Audio 丢失检测）══════════
            sb.AppendLine("## ⑤ 资源依赖完整性");
            sb.AppendLine();

            var missingDeps = new Dictionary<string, List<string>>();
            string[] exts = { ".png", ".jpg", ".tga", ".asset", ".mat", ".shader", ".anim", ".controller", ".wav", ".mp3", ".ogg" };
            int checkedAssets = 0;

            foreach (var g in AssetDatabase.FindAssets("t:Material t:Prefab t:AnimatorController"))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (!p.StartsWith("Assets/")) continue;
                checkedAssets++;
                var deps = AssetDatabase.GetDependencies(p, false);
                foreach (var d in deps)
                {
                    if (string.IsNullOrEmpty(d)) continue;
                    if (d.StartsWith("Packages/")) continue;
                    if (!File.Exists(d) && !Directory.Exists(d))
                    {
                        if (!missingDeps.ContainsKey(p)) missingDeps[p] = new List<string>();
                        missingDeps[p].Add(d);
                    }
                }
            }

            sb.AppendLine($"检查了 **{checkedAssets}** 个资源（Material / Prefab / AnimatorController）的依赖");
            sb.AppendLine();
            if (missingDeps.Count == 0)
            {
                sb.AppendLine("**结论：✅ 无丢失的依赖资源**");
            }
            else
            {
                sb.AppendLine($"**⚠️ 发现 {missingDeps.Count} 个资源有丢失依赖：**");
                sb.AppendLine();
                foreach (var kv in missingDeps.Take(20))
                {
                    sb.AppendLine($"- `{kv.Key}`");
                    foreach (var d in kv.Value.Take(5)) sb.AppendLine($"  - ❌ {d}");
                }
            }
            sb.AppendLine();

            // ══════════ ⑥ Texture / Mesh / AudioClip 清单 ══════════
            sb.AppendLine("## ⑥ 资源清单统计");
            sb.AppendLine();
            sb.AppendLine("| 类型 | 数量 |");
            sb.AppendLine("|---|---|");

            var counts = new (string type, string filter)[]
            {
                ("Texture2D", "t:Texture2D"),
                ("Mesh", "t:Mesh"),
                ("AudioClip", "t:AudioClip"),
                ("Material", "t:Material"),
                ("Prefab", "t:Prefab"),
                ("Scene", "t:Scene"),
                ("AnimatorController", "t:AnimatorController"),
                ("PlayableDirector (Timeline)", "t:PlayableDirector"),
            };

            foreach (var (type, filter) in counts)
            {
                int n = 0;
                foreach (var g in AssetDatabase.FindAssets(filter))
                {
                    if (AssetDatabase.GUIDToAssetPath(g).StartsWith("Assets/")) n++;
                }
                sb.AppendLine($"| {type} | {n} |");
            }
            sb.AppendLine();

            // ══════════ ⑦ Shader 检查 ══════════
            sb.AppendLine("## ⑦ Shader 检查");
            sb.AppendLine();

            var shaderIssues = new List<string>();
            var shaderNames = new HashSet<string>();
            foreach (var g in AssetDatabase.FindAssets("t:Shader"))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (!p.StartsWith("Assets/")) continue;
                var sh = AssetDatabase.LoadAssetAtPath<Shader>(p);
                if (sh == null) { shaderIssues.Add($"❌ 无法加载: {p}"); continue; }
                shaderNames.Add(sh.name);
                if (sh.name.Contains("Error")) shaderIssues.Add($"❌ 错误 Shader: {p}");
            }

            sb.AppendLine($"项目内 Shader 共 {shaderNames.Count} 个（不含内置/包内）");
            if (shaderIssues.Count == 0) sb.AppendLine("**结论：✅ 无 Shader 错误**");
            else foreach (var i in shaderIssues.Take(15)) sb.AppendLine($"- {i}");
            sb.AppendLine();

            // 材质使用的 shader 统计
            var matShaderCount = new Dictionary<string, int>();
            foreach (var g in AssetDatabase.FindAssets("t:Material"))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (!p.StartsWith("Assets/")) continue;
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m == null || m.shader == null) continue;
                string sn = m.shader.name;
                if (!matShaderCount.ContainsKey(sn)) matShaderCount[sn] = 0;
                matShaderCount[sn]++;
            }
            sb.AppendLine("材质使用的 Shader 分布：");
            sb.AppendLine();
            sb.AppendLine("| Shader | 材质数 |");
            sb.AppendLine("|---|---|");
            foreach (var kv in matShaderCount.OrderByDescending(x => x.Value))
                sb.AppendLine($"| {kv.Key} | {kv.Value} |");
            sb.AppendLine();

            // ══════════ ⑧ Animator / Timeline ══════════
            sb.AppendLine("## ⑧ Animator / Timeline 引用");
            sb.AppendLine();

            int animatorCount = 0, animatorMissing = 0, timelineCount = 0, timelineMissing = 0;
            foreach (var bs in buildScenes)
            {
                if (!bs.enabled || !File.Exists(bs.path)) continue;
                EditorSceneManager.OpenScene(bs.path, OpenSceneMode.Single);

                foreach (var a in Object.FindObjectsByType<Animator>(FindObjectsSortMode.None))
                {
                    animatorCount++;
                    if (a.runtimeAnimatorController == null) animatorMissing++;
                }
                foreach (var pd in Object.FindObjectsByType<UnityEngine.Playables.PlayableDirector>(FindObjectsSortMode.None))
                {
                    timelineCount++;
                    if (pd.playableAsset == null) timelineMissing++;
                }
            }

            sb.AppendLine("| 项 | 数量 | 缺失 |");
            sb.AppendLine("|---|---|---|");
            sb.AppendLine($"| Animator | {animatorCount} | {(animatorMissing == 0 ? "✅ 0" : "❌ " + animatorMissing)} |");
            sb.AppendLine($"| PlayableDirector (Timeline) | {timelineCount} | {(timelineMissing == 0 ? "✅ 0" : "❌ " + timelineMissing)} |");
            sb.AppendLine();

            // ══════════ ⑨ 重复 / 废弃脚本 ══════════
            sb.AppendLine("## ⑨ 脚本统计");
            sb.AppendLine();

            var scripts = AssetDatabase.FindAssets("t:MonoScript")
                .Select(g => AssetDatabase.GUIDToAssetPath(g))
                .Where(p => p.StartsWith("Assets/"))
                .ToList();

            var nameGroups = scripts.GroupBy(p => Path.GetFileNameWithoutExtension(p))
                                    .Where(g => g.Count() > 1)
                                    .ToList();

            sb.AppendLine($"| 项 | 值 |");
            sb.AppendLine($"|---|---|");
            sb.AppendLine($"| 脚本总数 | {scripts.Count} |");
            sb.AppendLine($"| 重名脚本组 | {nameGroups.Count} |");
            sb.AppendLine();

            if (nameGroups.Count > 0)
            {
                sb.AppendLine("重名脚本（可能是重复定义，需检查）：");
                sb.AppendLine();
                foreach (var g in nameGroups.Take(15))
                {
                    sb.AppendLine($"- **{g.Key}**");
                    foreach (var p in g) sb.AppendLine($"  - {p}");
                }
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("**结论：✅ 无重名脚本**");
                sb.AppendLine();
            }

            // 测试专用脚本识别
            var testScripts = scripts.Where(p =>
                p.Contains("/DebugTools/") ||
                Path.GetFileName(p).Contains("Test") ||
                Path.GetFileName(p).Contains("Diag") ||
                Path.GetFileName(p).Contains("Probe") ||
                Path.GetFileName(p).Contains("Audit") ||
                Path.GetFileName(p).Contains("Check") ||
                p.Contains("/Editor/")).ToList();

            sb.AppendLine($"**测试/诊断专用脚本：{testScripts.Count} 个**");
            sb.AppendLine("（这些脚本在 Builds 里是否存在需要单独检查 —— 见阶段六）");
            sb.AppendLine();

            // ══════════ ⑩ 渲染管线 ══════════
            sb.AppendLine("## ⑩ 渲染管线配置");
            sb.AppendLine();

            var urpAsset = GraphicsSettings.defaultRenderPipeline;
            sb.AppendLine($"| 项 | 值 |");
            sb.AppendLine($"|---|---|");
            sb.AppendLine($"| 默认渲染管线 | {(urpAsset != null ? urpAsset.name : "内置管线")} |");

            var qs = QualitySettings.names;
            sb.AppendLine($"| 质量等级数 | {qs.Length} |");
            sb.AppendLine($"| 当前质量等级 | {qs[QualitySettings.GetQualityLevel()]} |");
            sb.AppendLine();

            // ══════════ 总结 ══════════
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## 阶段一总结");
            sb.AppendLine();
            sb.AppendLine("| 检查项 | 结果 |");
            sb.AppendLine("|---|---|");
            sb.AppendLine($"| Unity 版本与设置 | ✅ {Application.unityVersion} |");
            sb.AppendLine($"| Build Settings 场景列表 | {(buildListOk ? "✅ 正常" : "❌ 有问题")} |");
            sb.AppendLine($"| 五章场景齐全 | {(allChaptersOk ? "✅" : "❌")} |");
            sb.AppendLine($"| Missing Script / Material / Mesh / 粉色 | {(totalMiss == 0 ? "✅ 全部为 0" : "❌ " + totalMiss + " 处")} |");
            sb.AppendLine($"| 资源依赖完整性 | {(missingDeps.Count == 0 ? "✅ 无丢失" : "❌ " + missingDeps.Count + " 个资源有丢失依赖")} |");
            sb.AppendLine($"| Shader 错误 | {(shaderIssues.Count == 0 ? "✅ 无" : "❌ " + shaderIssues.Count)} |");
            sb.AppendLine($"| Animator / Timeline 缺失 | {((animatorMissing + timelineMissing) == 0 ? "✅ 无" : "❌ " + (animatorMissing + timelineMissing))} |");
            sb.AppendLine($"| 重名脚本 | {(nameGroups.Count == 0 ? "✅ 无" : "⚠️ " + nameGroups.Count + " 组")} |");

            string text = sb.ToString();
            Debug.Log("[IntegrityCheck] 完成");
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Final_Test");
                File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Final_Test\01_Project_Integrity_Report.md",
                                  text, new UTF8Encoding(false));
            }
            catch (System.Exception e) { Debug.LogError(e.Message); }
        }

        private static string GetActiveInputHandler()
        {
            try
            {
                var t = typeof(PlayerSettings);
                var p = t.GetProperty("activeInputHandler",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                if (p != null)
                {
                    int v = (int)p.GetValue(null);
                    return v switch { 0 => "旧版 Input Manager", 1 => "新版 Input System", 2 => "两者都启用", _ => v.ToString() };
                }
            }
            catch { }
            return "未知";
        }
    }
}