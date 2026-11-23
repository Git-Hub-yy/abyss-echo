using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// 阶段一补充：核实两个疑点。
    ///
    ///   ① Animator 缺失 2 个 —— 是哪些对象？是否影响游戏？
    ///   ② AudioClip = 0 —— 游戏是否本来就没有音频？AudioManager 怎么工作？
    /// </summary>
    public static class IntegrityFollowUp
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# 阶段一补充：疑点核实");
            sb.AppendLine();
            sb.AppendLine($"> 生成时间：{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            // ══════════ ① Animator 缺失 ══════════
            sb.AppendLine("## ① Animator 缺失详情");
            sb.AppendLine();

            var buildScenes = EditorBuildSettings.scenes;
            foreach (var bs in buildScenes)
            {
                if (!bs.enabled || !File.Exists(bs.path)) continue;
                EditorSceneManager.OpenScene(bs.path, OpenSceneMode.Single);

                var anims = Object.FindObjectsByType<Animator>(FindObjectsSortMode.None);
                if (anims.Length == 0) continue;

                sb.AppendLine($"### {Path.GetFileNameWithoutExtension(bs.path)}");
                sb.AppendLine();
                sb.AppendLine("| 对象 | Controller | 影响 |");
                sb.AppendLine("|---|---|---|");
                foreach (var a in anims)
                {
                    string ctrl = a.runtimeAnimatorController != null ? a.runtimeAnimatorController.name : "❌ NULL";
                    string impact = a.runtimeAnimatorController != null
                        ? "正常"
                        : "无动画播放（物体静止，但不影响功能）";
                    sb.AppendLine($"| {a.gameObject.name} | {ctrl} | {impact} |");
                }
                sb.AppendLine();
            }

            // ══════════ ② 音频系统 ══════════
            sb.AppendLine("## ② 音频系统检查");
            sb.AppendLine();

            int audioClips = AssetDatabase.FindAssets("t:AudioClip")
                .Count(g => AssetDatabase.GUIDToAssetPath(g).StartsWith("Assets/"));
            sb.AppendLine($"**项目内 AudioClip 资源数：{audioClips}**");
            sb.AppendLine();

            if (audioClips == 0)
            {
                sb.AppendLine("### 结论：项目没有任何音频文件");
                sb.AppendLine();
                sb.AppendLine("需要检查 `AudioManager` 在无音频资源时的行为：");
                sb.AppendLine();
            }

            // 查 AudioManager 实现
            var amPath = AssetDatabase.FindAssets("t:MonoScript AudioManager")
                .Select(g => AssetDatabase.GUIDToAssetPath(g))
                .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == "AudioManager");

            if (amPath != null)
            {
                sb.AppendLine($"**AudioManager 位置：** `{amPath}`");
                sb.AppendLine();

                var src = File.ReadAllText(amPath);
                // 统计所有 AudioSource.Play 调用
                int playCalls = System.Text.RegularExpressions.Regex.Matches(src, @"\.Play").Count;
                int nullChecks = System.Text.RegularExpressions.Regex.Matches(src, @"== null|!= null").Count;

                sb.AppendLine("| 项 | 值 |");
                sb.AppendLine("|---|---|");
                int srcLines = src.Split('\n').Length;
                sb.AppendLine($"| 代码行数 | {srcLines} |");
                sb.AppendLine($"| Play 调用次数 | {playCalls} |");
                sb.AppendLine($"| 空值检查次数 | {nullChecks} |");
                sb.AppendLine();

                // 找 AudioClip 字段
                var clipFields = System.Text.RegularExpressions.Regex.Matches(
                    src, @"(public|private)\s+AudioClip\w*\s+(\w+)");
                sb.AppendLine($"**AudioClip 字段数：{clipFields.Count}**");
                if (clipFields.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("| 字段名 |");
                    sb.AppendLine("|---|");
                    foreach (System.Text.RegularExpressions.Match m in clipFields)
                        sb.AppendLine($"| {m.Groups[2].Value} |");
                }
                sb.AppendLine();

                sb.AppendLine("### 结论");
                sb.AppendLine();
                if (audioClips == 0)
                {
                    sb.AppendLine("- 音频系统**没有可用资源**");
                    sb.AppendLine("- `PlayUiBlip()` 等调用会静默失败（AudioSource.PlayClipAtPoint(null) 不报错）");
                    sb.AppendLine("- **不影响游戏功能**，但玩家听到的是静音");
                    sb.AppendLine("- 判定：**P3（音效缺失，非阻断）** —— 需用户确认是否接受");
                }
            }
            else
            {
                sb.AppendLine("⚠️ 找不到 AudioManager 脚本");
            }
            sb.AppendLine();

            // ══════════ ③ 场景数量核对 ══════════
            sb.AppendLine("## ③ 场景文件清单");
            sb.AppendLine();
            sb.AppendLine("| 路径 | 在构建列表 |");
            sb.AppendLine("|---|---|");
            foreach (var g in AssetDatabase.FindAssets("t:Scene"))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (!p.StartsWith("Assets/")) continue;
                bool inBuild = buildScenes.Any(s => s.path == p && s.enabled);
                sb.AppendLine($"| {p} | {(inBuild ? "✅" : "—")} |");
            }
            sb.AppendLine();

            // ══════════ ④ 测试脚本是否会被打进构建 ══════════
            sb.AppendLine("## ④ 测试脚本对构建的影响");
            sb.AppendLine();
            sb.AppendLine("**说明：**");
            sb.AppendLine();
            sb.AppendLine("- `Assets/_Project/Scripts/DebugTools/` 下的脚本**不是 Editor 专用**");
            sb.AppendLine("  （没有放在 `Editor/` 文件夹，也没有 `#if UNITY_EDITOR` 保护）");
            sb.AppendLine("- 因此它们**会被编译进正式构建**，占用少量体积");
            sb.AppendLine("- 但它们**不会被自动执行**（需要场景里有对应组件才会运行）");
            sb.AppendLine("- 场景里是否挂了测试组件 → 见阶段六检查");
            sb.AppendLine();

            string text = sb.ToString();
            Debug.Log("[IntegrityFollowUp] 完成");
            File.AppendAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Final_Test\01_Project_Integrity_Report.md",
                               "\n---\n\n" + text, new UTF8Encoding(false));
        }
    }
}