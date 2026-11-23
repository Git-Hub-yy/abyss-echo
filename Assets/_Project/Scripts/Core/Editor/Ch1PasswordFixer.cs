using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S79：把**场景里已序列化**的门禁密码从 7412 改成 1212。
    ///
    /// ══════════════════════ 为什么需要单独改场景 ══════════════════════
    ///
    ///   KeypadPanel.cs 的字段默认值已改成 "1212"：
    ///       public string correctCode = "1212";
    ///
    ///   但**场景里已经存在的 Keypad_C2 实例**
    ///   把 "7412" 序列化进了 .unity 文件，
    ///   Unity 加载时用序列化值覆盖字段默认值。
    ///
    ///   所以只改代码默认值没用，必须直接改场景里的实例。
    ///
    /// ══════════════════════ 同时改纸条 ══════════════════════
    ///
    ///   纸条 Note_KeypadCode 的正文里写着 "7 4 1 2"，
    ///   必须同步改成 "1 2 1 2"。
    ///
    ///   ⚠️ 纸条正文存在 NoteInteractable 组件里，需要找对字段。
    /// </summary>
    public static class Ch1PasswordFixer
    {
        private static readonly string[] Scenes =
        {
            "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity",
        };

        private const string OLD_CODE = "7412";
        private const string NEW_CODE = "1212";
        private const string OLD_TEXT = "7 4 1 2";
        private const string NEW_TEXT = "1 2 1 2";

        [MenuItem("ABYSS ECHO/修复 v10.6/第一章密码改为1212")]
        public static void Build() => Run(dryRun: false);

        [MenuItem("ABYSS ECHO/修复 v10.6/预演（只读）")]
        public static void DryRun() => Run(dryRun: true);

        public static void Run(bool dryRun)
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════");
            sb.AppendLine(dryRun ? "  第一章密码 7412 → 1212 —— 预演"
                                 : "  第一章密码 7412 → 1212 —— 执行");
            sb.AppendLine("═══════════════════════════════════════════════════");

            int changedPads = 0, changedNotes = 0;

            foreach (var scenePath in Scenes)
            {
                if (!File.Exists(scenePath)) continue;
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                Physics.SyncTransforms();
                bool dirty = false;

                sb.AppendLine();
                sb.AppendLine($"──── {Path.GetFileNameWithoutExtension(scenePath)} ────");

                // ══════ ① 门禁键盘 ══════
                var pads = Object.FindObjectsByType<AbyssEcho.Puzzle.KeypadPanel>(FindObjectsSortMode.None);
                sb.AppendLine($"  找到 KeypadPanel = {pads.Length} 个");

                var codeF = typeof(AbyssEcho.Puzzle.KeypadPanel)
                    .GetField("correctCode",
                              System.Reflection.BindingFlags.Instance |
                              System.Reflection.BindingFlags.Public |
                              System.Reflection.BindingFlags.NonPublic);

                foreach (var p in pads)
                {
                    if (p == null || codeF == null) continue;
                    string cur = codeF.GetValue(p) as string;
                    sb.AppendLine($"    {p.gameObject.name}: '{cur}'");

                    if (cur == OLD_CODE)
                    {
                        if (!dryRun)
                        {
                            codeF.SetValue(p, NEW_CODE);
                            EditorUtility.SetDirty(p);
                            changedPads++;
                            dirty = true;
                        }
                        sb.AppendLine($"      → {(dryRun ? "[预演] 将改为" : "已改为")} '{NEW_CODE}'");
                    }
                    else if (cur == NEW_CODE)
                    {
                        sb.AppendLine("      ✅ 已是 1212");
                    }
                    else
                    {
                        sb.AppendLine($"      ⚠️ 既不是 {OLD_CODE} 也不是 {NEW_CODE}，跳过（避免误改）");
                    }
                }

                // ══════ ② 纸条 ══════
                sb.AppendLine();
                sb.AppendLine("  纸条相关对象：");
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                {
                    string n = t.name;
                    if (!n.Contains("Note") && !n.Contains("便条") && !n.Contains("纸条")) continue;

                    var comps = new List<string>();
                    string bodyFieldName = null;
                    object bodyComp = null;

                    foreach (var c in t.gameObject.GetComponents<Component>())
                    {
                        if (c == null) continue;
                        comps.Add(c.GetType().Name);

                        // 找含有 body 字段且当前值包含密码的组件
                        foreach (var f in c.GetType().GetFields(
                                     System.Reflection.BindingFlags.Instance |
                                     System.Reflection.BindingFlags.Public |
                                     System.Reflection.BindingFlags.NonPublic))
                        {
                            if (f.FieldType != typeof(string)) continue;
                            string v = f.GetValue(c) as string;
                            if (v != null && (v.Contains(OLD_TEXT) || v.Contains(OLD_CODE)))
                            {
                                bodyFieldName = f.Name;
                                bodyComp = c;
                                break;
                            }
                        }
                        if (bodyComp != null) break;
                    }

                    sb.AppendLine($"    {n,-28} 组件=[{string.Join(",", comps)}]");

                    if (bodyComp != null && bodyFieldName != null)
                    {
                        var f = bodyComp.GetType().GetField(bodyFieldName,
                            System.Reflection.BindingFlags.Instance |
                            System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.NonPublic);
                        string oldVal = f.GetValue(bodyComp) as string;
                        sb.AppendLine($"      字段 '{bodyFieldName}' 含旧密码");
                        sb.AppendLine($"      旧: {oldVal}");

                        if (!dryRun)
                        {
                            string newVal = oldVal.Replace(OLD_TEXT, NEW_TEXT).Replace(OLD_CODE, NEW_CODE);
                            f.SetValue(bodyComp, newVal);
                            if (bodyComp is Object o) EditorUtility.SetDirty(o);
                            changedNotes++;
                            dirty = true;
                            sb.AppendLine($"      新: {newVal}");
                        }
                        else
                        {
                            sb.AppendLine($"      [预演] 将替换为: {oldVal.Replace(OLD_TEXT, NEW_TEXT).Replace(OLD_CODE, NEW_CODE)}");
                        }
                    }
                }

                // ══════ 保存 ══════
                if (!dryRun && dirty)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    bool ok = EditorSceneManager.SaveScene(scene, scenePath);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    sb.AppendLine();
                    sb.AppendLine(ok ? "  ✅ 场景已保存" : "  ❌ 保存失败");
                }
            }

            sb.AppendLine();
            sb.AppendLine("───────────────────────────────────");
            sb.AppendLine($"改门禁密码 {changedPads} 个，改纸条 {changedNotes} 个");

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch1PasswordFix_Result.txt",
                              text, new UTF8Encoding(false));
        }
    }
}