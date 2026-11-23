using System.IO;
using UnityEditor;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S9 发布配置：产品信息、分辨率、启动场景、构建列表。
    ///
    /// 为什么需要一个专门的配置工具：
    ///   直接改 ProjectSettings.asset 文本不可靠（Unity 会覆盖），
    ///   而 PlayerSettings API 是官方支持的方式，批处理里也能生效。
    /// </summary>
    public static class ReleaseSettingsBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";

        [MenuItem("ABYSS ECHO/S9 配置发布设置")]
        public static void Configure()
        {
            var log = new System.Text.StringBuilder();
            log.AppendLine("ABYSS ECHO · 发布配置");
            log.AppendLine("=====================================");

            // ── 1. 产品信息 ──
            PlayerSettings.companyName = "AbyssEchoStudio";
            PlayerSettings.productName = "ABYSS ECHO";
            PlayerSettings.bundleVersion = "1.1.0";
            log.AppendLine("产品名：ABYSS ECHO");
            log.AppendLine("公司：AbyssEchoStudio");
            log.AppendLine("版本：1.0.0（含第二章）");

            // ── 2. 显示设置 ──
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;   // 无边框全屏，Alt+Tab 友好
            PlayerSettings.resizableWindow = true;
            PlayerSettings.allowFullscreenSwitch = true;
            PlayerSettings.runInBackground = false;   // 失焦时暂停，省电
            log.AppendLine("分辨率：1920×1080");
            log.AppendLine("全屏模式：FullScreenWindow（无边框）");
            log.AppendLine("可调整窗口：是");

            // ── 3. 启动画面 ──
            // 保留 Unity 启动画面（免费版强制显示），但缩短时长
            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.showUnityLogo = true;
            PlayerSettings.SplashScreen.backgroundColor = new Color(0.03f, 0.04f, 0.06f);
            log.AppendLine("启动画面：Unity 标志 + 深色背景");
            log.AppendLine("背景色：(0.03, 0.04, 0.06)");

            // ── 4. 构建场景列表 ──
            var guid = AssetDatabase.AssetPathToGUID(ScenePath);
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogError("[Release] 找不到场景：" + ScenePath);
                EditorApplication.Exit(1);
                return;
            }

            // 只放正式关卡，把 TEST_Player 从构建列表移除
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
            log.AppendLine();
            log.AppendLine("构建场景列表：");
            log.AppendLine("  · " + ScenePath + "（唯一启动场景）");
            log.AppendLine("  · TEST_Player.unity 已从构建列表移除");

            // ── 5. 图形与性能 ──
            PlayerSettings.colorSpace = ColorSpace.Linear;
            log.AppendLine();
            log.AppendLine("色彩空间：Linear");

            // ── 6. 存档路径检查 ──
            log.AppendLine();
            log.AppendLine("存档路径：" + Application.persistentDataPath);

            log.AppendLine();
            log.AppendLine("=====================================");
            log.AppendLine("配置完成");

            string text = log.ToString();
            Debug.Log("<b>[Release]</b>\n" + text);

            try
            {
                string dir = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "S9_ReleaseSettings.txt"), text,
                                  new System.Text.UTF8Encoding(false));
            }
            catch { }

            AssetDatabase.SaveAssets();
        }
    }
}