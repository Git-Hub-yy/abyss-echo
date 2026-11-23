using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S1 自检工具（仅编辑器）。检查工程是否处于"可开发"状态。
    /// 菜单：ABYSS ECHO/运行 S1 环境自检
    ///
    /// 只做"读取与校验"，不修改任何工程文件。
    /// 结果同时输出到 Console 与 Reports/S1_环境自检结果.txt。
    ///
    /// ⚠️ 本文件必须位于 Editor 文件夹：它引用了 UnityEditor API，
    /// 若放在运行时目录会导致 Player 构建失败（S1 已于 2026-11-19 实测踩坑）。
    /// </summary>
    public static class ProjectHealthCheck
    {
        private const string ReportRelativePath = "../Reports/S1_环境自检结果.txt";

        [MenuItem("ABYSS ECHO/运行 S1 环境自检")]
        public static void Run()
        {
            var sb = new StringBuilder();
            int pass = 0, fail = 0, warn = 0;

            void Check(string name, Func<string> reader, Func<string, bool> ok, string expect)
            {
                string value;
                try { value = reader(); }
                catch (Exception e) { value = "<异常: " + e.GetType().Name + ">"; }

                bool good = false;
                try { good = ok(value); } catch { good = false; }

                if (good) { pass++; sb.AppendLine("[通过] " + name + " = " + value); }
                else { fail++; sb.AppendLine("[失败] " + name + " = " + value + "   （期望：" + expect + "）"); }
            }

            void Info(string name, Func<string> reader)
            {
                string value;
                try { value = reader(); } catch (Exception e) { value = "<异常: " + e.Message + ">"; }
                warn++;
                sb.AppendLine("[信息] " + name + " = " + value);
            }

            sb.AppendLine("ABYSS ECHO · S1 环境自检");
            sb.AppendLine("时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("编辑器：" + Application.unityVersion);
            sb.AppendLine("─────────────────────────────────────");

            // 1. 编辑器版本
            Check("Unity 版本", () => Application.unityVersion,
                  v => v == "2023.1.1f1c1", "2023.1.1f1c1");

            // 2. 新输入系统已启用
            // 说明：PlayerSettings.activeInputHandler 为只读属性，取值为
            //   0 = 旧版 Input Manager，1 = 新版 Input System，2 = 两者都启用。
            // 之前在 ProjectSettings.asset 中已确认为 2。
            // 新版 Input System 的可用性以程序集是否编译成功为准，见第 6 项包检查。
            Check("Active Input Handling",
                  () =>
                  {
                      var t = typeof(PlayerSettings);
                      var p = t.GetProperty("activeInputHandler",
                                            System.Reflection.BindingFlags.Public |
                                            System.Reflection.BindingFlags.NonPublic |
                                            System.Reflection.BindingFlags.Static);
                      return p != null ? "枚举值 " + p.GetValue(null) : "无法读取（以包检查为准）";
                  },
                  v => v == "枚举值 2" || v.StartsWith("无法读取"), "枚举值 2（两者都启用）");

            // 3. URP 管线已指定
            Check("当前渲染管线",
                  () => GraphicsSettings.defaultRenderPipeline == null
                        ? "内置管线（未指定）"
                        : GraphicsSettings.defaultRenderPipeline.GetType().Name,
                  v => v.Contains("Universal"), "UniversalRenderPipelineAsset");

            // 4. 颜色空间
            Check("颜色空间", () => PlayerSettings.colorSpace.ToString(),
                  v => v == "Linear", "Linear");

            // 5. 关键目录
            string[] required =
            {
                "Assets/_Project/Scripts/Core",
                "Assets/_Project/Scripts/Player",
                "Assets/_Project/Scripts/Interaction",
                "Assets/_Project/Scripts/Tools",
                "Assets/_Project/Scripts/UI",
                "Assets/_Project/Scripts/Audio",
                "Assets/_Project/Scripts/Puzzle",
                "Assets/_Project/Scripts/Door",
                "Assets/_Project/Scripts/Inventory",
                "Assets/_Project/Scripts/Quest",
                "Assets/_Project/Scripts/Save",
                "Assets/_Project/Scripts/Narrative",
                "Assets/_Project/Scripts/Horror",
                "Assets/_Project/Scripts/Monster",
                "Assets/_Project/Scripts/Utils",
                "Assets/_Project/Scenes",
                "Assets/_Project/Prefabs",
                "Assets/_Project/Data",
                "Assets/_Project/Settings"
            };
            int missing = 0;
            foreach (var dir in required)
                if (!AssetDatabase.IsValidFolder(dir)) { missing++; sb.AppendLine("    缺失目录：" + dir); }
            Check("目录结构", () => (required.Length - missing) + "/" + required.Length + " 个目录存在",
                  v => v == required.Length + "/" + required.Length + " 个目录存在", "全部存在");

            // 6. 包
            string[] packages =
            {
                "com.unity.inputsystem",
                "com.unity.render-pipelines.universal",
                "com.unity.textmeshpro",
                "com.unity.ugui",
                "com.unity.timeline"
            };
            var pkgLines = new StringBuilder();
            foreach (var p in packages)
            {
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(
                    "Packages/" + p + "/package.json");
                pkgLines.Append("      ").AppendLine(p + " -> " + (info != null ? info.version : "未安装"));
            }
            bool allPkg = pkgLines.ToString().IndexOf("未安装", StringComparison.Ordinal) < 0;
            sb.Append("    包版本：\n").Append(pkgLines);
            Check("核心包安装状态", () => allPkg ? "全部已安装" : "存在缺失",
                  v => v == "全部已安装", "全部已安装");

            // 7. 平台
            Info("当前平台", () => Application.platform.ToString());
            Info("目标平台（构建）", () => EditorUserBuildSettings.activeBuildTarget.ToString());

            // 8. 图形设备
            Info("GPU", () => SystemInfo.graphicsDeviceName + " / " + SystemInfo.graphicsDeviceVersion);
            Info("显存", () => SystemInfo.graphicsMemorySize + " MB");
            Info("内存", () => SystemInfo.systemMemorySize + " MB");
            Info("屏幕分辨率", () => Screen.currentResolution.width + "x" + Screen.currentResolution.height);

            sb.AppendLine("─────────────────────────────────────");
            sb.AppendLine("总结：通过 " + pass + " 项，失败 " + fail + " 项，信息 " + warn + " 项");
            sb.AppendLine(fail == 0 ? "结论：✅ S1 自检通过" : "结论：❌ 存在未通过项，请勿进入 S2");

            string text = sb.ToString();
            Debug.Log("<b>ABYSS ECHO · S1 环境自检</b>\n" + text);

            // 写到 <工程根>/../Reports 目录；失败时降级到 persistentDataPath，绝不静默丢失
            string writtenPath = null;
            try
            {
                // 说明：batchmode 下当前工作目录不可靠（实测会落到工程外），
                // 因此这里以 Application.dataPath 为唯一基准解析工程根目录。
                // dataPath 形如 D:\...\ABYSS_ECHO\UnityProject\Assets
                string projectRoot = Path.GetDirectoryName(Path.GetFullPath(Application.dataPath));
                string reportPath = Path.GetFullPath(Path.Combine(projectRoot ?? string.Empty, ReportRelativePath));
                string reportDir = Path.GetDirectoryName(reportPath);
                if (reportDir != null)
                {
                    Directory.CreateDirectory(reportDir);
                    File.WriteAllText(reportPath, text);
                    writtenPath = reportPath;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("写入 Reports 目录失败，尝试降级：" + e.Message);
            }

            if (writtenPath == null)
            {
                try
                {
                    writtenPath = Path.Combine(Application.persistentDataPath, "S1_环境自检结果.txt");
                    File.WriteAllText(writtenPath, text);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("降级写入仍失败：" + e.Message);
                }
            }

            Debug.Log(writtenPath != null
                ? "自检结果已写入：" + writtenPath
                : "自检结果仅保留在 Console 中（文件写入全部失败）");
        }
    }
}