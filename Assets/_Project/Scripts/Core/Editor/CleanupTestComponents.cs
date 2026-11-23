using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S27：清理场景里残留的测试组件。
    ///
    /// ⚠️ 事故原因（玩家实测"第二章水位下降后就莫名退出游戏"）：
    ///   BuildToolsS26Test 把 AutoTest_S26 挂进了第二章场景并保存。
    ///   打包发布版时，BuildToolsRelease 的显式禁用列表里没有 S26，
    ///   所以它带着 runOnStart=True 进了正式版。
    ///
    ///   AutoTest_S26 的行为：
    ///     ① 删除存档
    ///     ② 自动模拟抽水（水位下降）
    ///     ③ 跑完 10 项测试后调用 Application.Quit()
    ///   → 玩家看到的就是"水位下降后游戏自己退出"。
    ///
    ///   修复：把测试组件从场景里彻底移除（不是禁用，是删除），
    ///       并且禁止测试构建器再把测试组件保存进正式场景。
    /// </summary>
    public static class CleanupTestComponents
    {
        private static readonly string[] Scenes =
        {
            "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity",
            "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity",
            "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity",
            "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity",
            "Assets/_Project/Scenes/Level_Chapter5_Graybox.unity",
        };

        [MenuItem("ABYSS ECHO/工具：清理场景中的测试组件")]
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== 清理测试组件 ===");
            int totalRemoved = 0;

            foreach (var path in Scenes)
            {
                if (!File.Exists(path)) continue;

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                sb.AppendLine($"--- {Path.GetFileName(path)} ---");

                // 找出所有 DebugTools 命名空间下的组件
                var all = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                int removed = 0;

                foreach (var mb in all)
                {
                    if (mb == null) continue;
                    var t = mb.GetType();
                    if (t.Namespace == null || !t.Namespace.Contains("DebugTools")) continue;

                    // 保留这几个无害的（它们不调用 Quit）
                    if (t.Name == "PlayerDebugHUD") continue;

                    sb.AppendLine($"  移除 {t.Name} @ {mb.gameObject.name}");
                    Object.DestroyImmediate(mb);
                    removed++;
                }

                // 删除空的测试根节点
                foreach (var goName in new[] { "--- S24TEST ---", "--- S25TEST ---", "--- S26TEST ---", "--- DOORTEST ---", "--- INTERACTDIAG ---", "--- HORRORDIAG ---", "--- WALKTEST ---", "--- REALWALK ---", "--- UIDIAG ---", "--- DOORDIAG ---", "--- MEASURE ---", "--- AUDIT ---" })
                {
                    var go = GameObject.Find(goName);
                    if (go != null)
                    {
                        sb.AppendLine($"  删除残留节点 {goName}");
                        Object.DestroyImmediate(go);
                        removed++;
                    }
                }

                if (removed > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }

                sb.AppendLine($"  共处理 {removed} 项");
                totalRemoved += removed;
            }

            sb.AppendLine();
            sb.AppendLine($"总计：{totalRemoved} 项");

            string text = sb.ToString();
            Debug.Log("<b>[CleanupTest]</b>\n" + text);

            try
            {
                string dir = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "CleanupTestComponents.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}