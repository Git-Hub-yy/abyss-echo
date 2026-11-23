using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S55：确认五章场景中 PeaceModeManager 的实际挂载情况。
    ///
    /// 用 Unity 真实加载场景后查找组件，
    /// 而不是搜场景文件的字节（二进制格式搜索不可靠）。
    /// </summary>
    public static class PeaceModeVerify
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("══════ PeaceModeManager 挂载验证 ══════");

            int totalProblems = 0;

            for (int n = 1; n <= 5; n++)
            {
                string p = $"Assets/_Project/Scenes/Level_Chapter{n}_Graybox.unity";
                if (!File.Exists(p)) { sb.AppendLine($"Ch{n}: 场景不存在"); continue; }

                EditorSceneManager.OpenScene(p, OpenSceneMode.Single);

                var all = Object.FindObjectsByType<AbyssEcho.DebugTools.PeaceModeManager>(
                    FindObjectsSortMode.None);
                var motor = Object.FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();

                sb.AppendLine();
                sb.AppendLine($"### Chapter{n}");
                sb.AppendLine($"  PeaceModeManager 数量 = {all.Length}");
                foreach (var pm in all)
                    sb.AppendLine($"    @ {GetPath(pm.transform)}  key={pm.toggleKey} " +
                                  $"startEnabled={pm.startEnabled} toast={pm.toastDuration}s");

                sb.AppendLine($"  PlayerMotor = {(motor != null ? motor.gameObject.name : "❌")}");

                if (all.Length == 1)
                {
                    bool onPlayer = motor != null && all[0].gameObject == motor.gameObject;
                    sb.AppendLine($"  挂在 Player 上 = {(onPlayer ? "✅" : "⚠️ 不在 Player 上")}");
                    if (!onPlayer) totalProblems++;
                }
                else if (all.Length == 0)
                {
                    sb.AppendLine("  ❌ 缺失");
                    totalProblems++;
                }
                else
                {
                    sb.AppendLine("  ❌ 重复");
                    totalProblems++;
                }
            }

            sb.AppendLine();
            sb.AppendLine($"问题数量 = {totalProblems}");

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(Application.dataPath), "../Reports/PeaceModeVerify.txt")),
                text, new UTF8Encoding(false));
        }

        private static string GetPath(Transform t)
        {
            string p = t.name;
            var cur = t.parent; int g = 0;
            while (cur != null && g++ < 20) { p = cur.name + "/" + p; cur = cur.parent; }
            return p;
        }
    }
}