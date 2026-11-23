using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>S63：把 G5 出口指引挂到第四章玩家身上。</summary>
    public static class G5ExitGuideBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity";

        [MenuItem("ABYSS ECHO/修复 v6.9/第四章 G5 出口指引")]
        public static void Build() => Run();

        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var motor = Object.FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            if (motor == null) { Log("❌ 找不到 PlayerMotor"); return; }

            var go = motor.gameObject;
            var guide = go.GetComponent<AbyssEcho.DebugTools.G5ExitGuide>();
            if (guide == null)
            {
                guide = go.AddComponent<AbyssEcho.DebugTools.G5ExitGuide>();
                Log($"+ 添加 G5ExitGuide 到 {go.name}");
            }
            else Log("G5ExitGuide 已存在，校正参数");

            // 参数
            guide.showZoneMin = new Vector2(-25f, -5f);
            guide.showZoneMax = new Vector2(-13f, 10f);
            guide.doorCenter = new Vector3(-19f, 1.5f, 5f);
            guide.fontSize = 18;

            EditorSceneManager.MarkSceneDirty(scene);
            bool ok = EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log(ok ? "场景已保存" : "❌ 保存失败");

            // 验证
            var all = Object.FindObjectsByType<AbyssEcho.DebugTools.G5ExitGuide>(FindObjectsSortMode.None);
            Log($"场景中 G5ExitGuide 数量 = {all.Length}");
            foreach (var g in all)
                Log($"  @{g.gameObject.name}  显示区 x[{g.showZoneMin.x},{g.showZoneMax.x}] z[{g.showZoneMin.y},{g.showZoneMax.y}]  门洞={g.doorCenter}");
        }

        private static void Log(string msg)
        {
            string line = $"[G5Guide] {msg}";
            Debug.Log(line);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.AppendAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\G5ExitGuide_Result.txt",
                                   line + "\n", new UTF8Encoding(false));
            }
            catch { }
        }
    }
}