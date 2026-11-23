using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>S38：第五章场景完整性审计。</summary>
    public static class ChapterFiveAudit
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter5_Graybox.unity";

        [MenuItem("ABYSS ECHO/审计第五章")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var sb = new StringBuilder();
            sb.AppendLine("===== 第五章场景审计 =====");
            sb.AppendLine();

            var motor = Object.FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            var cam = Object.FindFirstObjectByType<Camera>();
            var det = Object.FindFirstObjectByType<AbyssEcho.Interaction.InteractionDetector>();
            var audio = Object.FindFirstObjectByType<AbyssEcho.Audio.AudioManager>();
            var cross = Object.FindFirstObjectByType<AbyssEcho.UI.CrosshairUI>();
            var pause = Object.FindFirstObjectByType<AbyssEcho.UI.PauseMenuUI>();
            var card = Object.FindFirstObjectByType<AbyssEcho.UI.ChapterTitleCard>();

            sb.AppendLine("--- 必需组件 ---");
            sb.AppendLine($"  玩家 PlayerMotor : {(motor != null ? "✅" : "❌")}");
            sb.AppendLine($"  相机 Camera      : {(cam != null ? "✅" : "❌")}");
            sb.AppendLine($"  交互检测器        : {(det != null ? "✅" : "❌")}");
            sb.AppendLine($"  AudioManager     : {(audio != null ? "✅" : "❌ 会完全没声音")}");
            sb.AppendLine($"  准星             : {(cross != null ? "✅" : "❌")}");
            sb.AppendLine($"  暂停菜单          : {(pause != null ? "✅" : "❌")}");
            sb.AppendLine($"  章节标题卡        : {(card != null ? "✅" : "❌")}");

            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            int lit = 0;
            foreach (var l in lights) if (l.enabled) lit++;
            sb.AppendLine();
            sb.AppendLine("--- 灯光 ---");
            sb.AppendLine($"  共 {lights.Length} 盏，亮 {lit} 盏");

            var props = GameObject.Find("--- PROPS_CH5 ---");
            sb.AppendLine();
            sb.AppendLine("--- 道具 ---");
            if (props != null)
            {
                sb.AppendLine($"  {props.GetComponentsInChildren<Collider>(true).Length} 件");
                foreach (var key in new[] { "出口舱门", "舱门框", "控制台", "引导线", "绞盘" })
                {
                    bool found = false;
                    foreach (var t in props.GetComponentsInChildren<Transform>(true))
                        if (t.name == key) { found = true; break; }
                    sb.AppendLine($"  {(found ? "✅" : "❌")} {key}");
                }
            }
            else sb.AppendLine("  ❌ 找不到 --- PROPS_CH5 ---");

            var horror = GameObject.Find("--- HORROR_CH5 ---");
            sb.AppendLine();
            sb.AppendLine("--- 恐怖内容 ---");
            if (horror != null)
            {
                var mimics = horror.GetComponentsInChildren<AbyssEcho.Horror.MimicStalker>(true);
                var triggers = horror.GetComponentsInChildren<AbyssEcho.Horror.HorrorTrigger>(true);
                sb.AppendLine($"  模仿者：{mimics.Length} 个 {(mimics.Length >= 1 ? "✅" : "❌")}");
                sb.AppendLine($"  触发器：{triggers.Length} 个");
            }
            else sb.AppendLine("  ❌ 找不到 --- HORROR_CH5 ---");

            var level = GameObject.Find("--- LEVEL_CH5 ---");
            sb.AppendLine();
            sb.AppendLine("--- 关卡 ---");
            if (level != null)
            {
                int rooms = level.transform.childCount > 0 ? level.transform.GetChild(0).childCount : 0;
                sb.AppendLine($"  ✅ 几何根节点（{rooms} 个房间）");
            }
            else sb.AppendLine("  ❌ 找不到 --- LEVEL_CH5 ---");

            sb.AppendLine();
            sb.AppendLine("--- 环境光 ---");
            sb.AppendLine($"  ambientLight = {RenderSettings.ambientLight}");

            Finish(sb);
        }

        private static void Finish(StringBuilder sb)
        {
            string text = sb.ToString();
            Debug.Log("<b>[Ch5Audit]</b>\n" + text);
            try
            {
                string dir = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "ChapterFiveAudit.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}