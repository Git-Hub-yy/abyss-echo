using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// 复核：PlayerDeathSystem 在各章的配置与依赖（只读）。
    ///
    /// 检查内容：
    ///   · PlayerDeathSystem 是否存在、挂在哪
    ///   · 全部序列化字段值（与第一章对比，找出差异）
    ///   · Awake 依赖的 6 个组件在各章是否存在
    ///   · MonsterAI / MimicStalker 是否能找到玩家与死亡系统
    /// </summary>
    public static class DeathSystemCheck
    {
        private static readonly string[] Scenes =
        {
            "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity",
            "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity",
            "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity",
            "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity",
            "Assets/_Project/Scenes/Level_Chapter5_Graybox.unity",
        };

        [MenuItem("ABYSS ECHO/复核/死亡系统检查")]
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("========== PlayerDeathSystem 检查 ==========");

            foreach (var path in Scenes)
            {
                if (!File.Exists(path)) { sb.AppendLine($"\n### {Path.GetFileName(path)} ❌ 不存在"); continue; }

                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                string sn = Path.GetFileNameWithoutExtension(path);

                sb.AppendLine();
                sb.AppendLine($"### {sn}");

                // ── 1. PlayerDeathSystem ──
                var deaths = Object.FindObjectsByType<AbyssEcho.Horror.PlayerDeathSystem>(FindObjectsSortMode.None);
                sb.AppendLine($"  PlayerDeathSystem 数量 = {deaths.Length}");

                foreach (var d in deaths)
                {
                    sb.AppendLine($"    挂载路径 = {GetPath(d.transform)}");
                    sb.AppendLine($"    active={d.gameObject.activeInHierarchy} enabled={d.enabled}");
                    sb.AppendLine($"    IsDead = {d.IsDead}");

                    var so = new SerializedObject(d);
                    foreach (var f in new[] { "fadeDuration", "deathTitle", "deathSubtitle", "restartHint",
                                               "fadeColor", "titleColor", "textColor",
                                               "titleFontSize", "textFontSize" })
                    {
                        var p = so.FindProperty(f);
                        if (p == null) { sb.AppendLine($"      {f} = <无此字段>"); continue; }
                        string v;
                        switch (p.propertyType)
                        {
                            case SerializedPropertyType.Float: v = p.floatValue.ToString("F3"); break;
                            case SerializedPropertyType.String: v = p.stringValue; break;
                            case SerializedPropertyType.Color: v = p.colorValue.ToString(); break;
                            case SerializedPropertyType.Integer: v = p.intValue.ToString(); break;
                            default: v = p.ToString(); break;
                        }
                        sb.AppendLine($"      {f} = {v}");
                    }
                }

                // ── 2. Awake 依赖 ──
                sb.AppendLine("  -- Awake() 依赖的组件 --");
                Dep<AbyssEcho.Player.PlayerInputReader>(sb, "_input", "PlayerInputReader");
                Dep<AbyssEcho.Player.PlayerMotor>(sb, "_motor", "PlayerMotor");
                Dep<AbyssEcho.Player.PlayerLook>(sb, "_look", "PlayerLook");
                Dep<AbyssEcho.Interaction.InteractionDetector>(sb, "_detector", "InteractionDetector");
                Dep<AbyssEcho.UI.CrosshairUI>(sb, "_crosshair", "CrosshairUI");
                Dep<AbyssEcho.Horror.FearVisuals>(sb, "_fearVisuals", "FearVisuals（可选，null 不崩）");

                // ── 3. Player 对象 ──
                sb.AppendLine("  -- Player 对象 --");
                var motor = Object.FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
                if (motor != null)
                {
                    var pgo = motor.gameObject;
                    sb.AppendLine($"    Player = {GetPath(pgo.transform)}  active={pgo.activeInHierarchy}");
                    var comps = pgo.GetComponents<Component>();
                    var names = new StringBuilder();
                    foreach (var c in comps)
                        if (c != null) names.Append(c.GetType().Name + ", ");
                    sb.AppendLine($"    组件列表 = {names}");
                }
                else sb.AppendLine("    ❌ 没找到 PlayerMotor（玩家对象）");

                // ── 4. 怪物 ──
                sb.AppendLine("  -- 怪物 --");
                var monster = Object.FindFirstObjectByType<AbyssEcho.Monster.MonsterAI>();
                if (monster != null)
                    sb.AppendLine($"    MonsterAI = {monster.gameObject.name}  active={monster.gameObject.activeInHierarchy}");
                else sb.AppendLine("    MonsterAI = 无");

                var mimics = Object.FindObjectsByType<AbyssEcho.Horror.MimicStalker>(FindObjectsSortMode.None);
                sb.AppendLine($"    MimicStalker = {mimics.Length}");
                foreach (var m in mimics)
                    sb.AppendLine($"       {m.gameObject.name} enabled={m.enabled} state={m.StateName}");

                // ── 5. 是否有其他死亡处理组件 ──
                sb.AppendLine("  -- 其他可能的死亡处理 --");
                int others = 0;
                foreach (var c in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                {
                    if (c == null) continue;
                    string tn = c.GetType().Name;
                    if (tn.Contains("Death") && !(c is AbyssEcho.Horror.PlayerDeathSystem))
                    {
                        sb.AppendLine($"    ⚠️ {tn} @ {GetPath(c.transform)}");
                        others++;
                    }
                }
                if (others == 0) sb.AppendLine("    无其他 *Death* 组件");
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports/DeathSystemCheck.txt")), text, new UTF8Encoding(false));
        }

        private static void Dep<T>(StringBuilder sb, string field, string label) where T : Component
        {
            var arr = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            sb.AppendLine($"    {label,-34} = {(arr.Length > 0 ? $"✓ ({arr.Length}) @ {GetPath(arr[0].transform)}" : "❌ 无")}");
        }

        private static string GetPath(Transform t)
        {
            string p = t.name;
            var cur = t.parent;
            int g = 0;
            while (cur != null && g++ < 20) { p = cur.name + "/" + p; cur = cur.parent; }
            return p;
        }
    }
}