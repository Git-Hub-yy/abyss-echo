using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>S32：第三章场景完整性审计。</summary>
    public static class ChapterThreeAudit
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity";

        [MenuItem("ABYSS ECHO/审计第三章")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var sb = new StringBuilder();
            sb.AppendLine("===== 第三章场景审计 =====");
            sb.AppendLine();

            // 基础组件
            var motor = Object.FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            var cam = Object.FindFirstObjectByType<Camera>();
            var det = Object.FindFirstObjectByType<AbyssEcho.Interaction.InteractionDetector>();
            var audio = Object.FindFirstObjectByType<AbyssEcho.Audio.AudioManager>();
            var cross = Object.FindFirstObjectByType<AbyssEcho.UI.CrosshairUI>();
            var pause = Object.FindFirstObjectByType<AbyssEcho.UI.PauseMenuUI>();
            var hud = Object.FindFirstObjectByType<AbyssEcho.DebugTools.PlayerDebugHUD>();
            var card = Object.FindFirstObjectByType<AbyssEcho.UI.ChapterTitleCard>();

            sb.AppendLine("--- 必需组件 ---");
            sb.AppendLine($"  玩家 PlayerMotor : {(motor != null ? "✅" : "❌")}");
            sb.AppendLine($"  相机 Camera      : {(cam != null ? "✅" : "❌")}");
            sb.AppendLine($"  交互检测器        : {(det != null ? "✅" : "❌")}");
            sb.AppendLine($"  AudioManager     : {(audio != null ? "✅" : "❌ 会完全没声音")}");
            sb.AppendLine($"  准星             : {(cross != null ? "✅" : "❌")}");
            sb.AppendLine($"  暂停菜单          : {(pause != null ? "✅" : "❌")}");
            sb.AppendLine($"  章节标题卡        : {(card != null ? "✅" : "❌")}");
            sb.AppendLine($"  调试 HUD         : {(hud != null ? "存在（发布版会关）" : "无")}");

            // 灯光
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            int lit = 0;
            foreach (var l in lights) if (l.enabled) lit++;
            sb.AppendLine();
            sb.AppendLine("--- 灯光 ---");
            sb.AppendLine($"  共 {lights.Length} 盏，亮 {lit} 盏");

            // 道具
            var props = GameObject.Find("--- PROPS_CH3 ---");
            sb.AppendLine();
            sb.AppendLine("--- 道具 ---");
            if (props != null)
            {
                var cols = props.GetComponentsInChildren<Collider>(true);
                sb.AppendLine($"  {cols.Length} 件（有碰撞）");
                // 抽查关键道具
                foreach (var key in new[] { "★用过的餐盘7", "有水的杯子", "温的水杯", "供奉台", "长餐桌", "Water_F7" })
                {
                    var t = FindDeep(props.transform, key);
                    sb.AppendLine($"  {(t != null ? "✅" : "❌")} {key}");
                }
            }
            else sb.AppendLine("  ❌ 找不到 --- PROPS_CH3 ---");

            // 关卡
            sb.AppendLine();
            sb.AppendLine("--- 关卡 ---");
            var level = GameObject.Find("--- LEVEL_CH3 ---");
            sb.AppendLine($"  几何根节点：{(level != null ? "✅" : "❌")}");
            if (level != null) sb.AppendLine($"  房间节点数：{level.transform.childCount}");

            // 环境
            sb.AppendLine();
            sb.AppendLine("--- 环境光 ---");
            sb.AppendLine($"  ambientMode = {RenderSettings.ambientMode}");
            sb.AppendLine($"  ambientLight = {RenderSettings.ambientLight}");
            sb.AppendLine($"  fog = {RenderSettings.fog}, density = {RenderSettings.fogDensity}");

            Finish(sb);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        private static void Finish(StringBuilder sb)
        {
            string text = sb.ToString();
            Debug.Log("<b>[Ch3Audit]</b>\n" + text);
            try
            {
                string dir = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "ChapterThreeAudit.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}