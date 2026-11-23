using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S64：第三章现有多结局系统检查（只读，不修改任何文件）。
    /// </summary>
    public static class Ch3EndingAudit
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("══════════════════════════════════════════");
            sb.AppendLine("  第三章结局系统现状审计（只读）");
            sb.AppendLine("══════════════════════════════════════════");

            // ── 1. ChapterEndingSystem 结构 ──
            sb.AppendLine();
            sb.AppendLine("【一】ChapterEndingSystem 结构");
            var t = typeof(AbyssEcho.Narrative.ChapterEndingSystem);
            sb.AppendLine($"  类 = {t.FullName}");

            sb.AppendLine("  公有字段/属性：");
            foreach (var f in t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                sb.AppendLine($"    {f.FieldType.Name,-24} {f.Name}");
            foreach (var p in t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                sb.AppendLine($"    [P] {p.PropertyType.Name,-20} {p.Name}");

            sb.AppendLine("  私有字段（关键）：");
            foreach (var f in t.GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
            {
                if (f.Name.Contains("k__BackingField")) continue;
                sb.AppendLine($"    {f.FieldType.Name,-24} {f.Name}");
            }

            sb.AppendLine("  方法：");
            foreach (var m in t.GetMethods(System.Reflection.BindingFlags.Instance |
                                            System.Reflection.BindingFlags.NonPublic |
                                            System.Reflection.BindingFlags.Public |
                                            System.Reflection.BindingFlags.DeclaredOnly))
                sb.AppendLine($"    {m.ReturnType.Name,-12} {m.Name}({string.Join(", ", System.Array.ConvertAll(m.GetParameters(), x => x.ParameterType.Name))})");

            // ── 2. 第三章布局房间 ──
            sb.AppendLine();
            sb.AppendLine("【二】第三章房间");
            var rooms = AbyssEcho.LevelGen.ChapterLayout.GetRooms(
                AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter3);
            sb.AppendLine($"{"ID",-16} {"名称",-14} {"X 范围",-22} {"Z 范围",-22} {"高",-6}");
            sb.AppendLine(new string('-', 84));
            foreach (var r in rooms)
                sb.AppendLine($"{r.Id,-16} {r.DisplayName,-14} [{r.MinX,6:F1},{r.MaxX,6:F1}]        " +
                              $"[{r.MinZ,6:F1},{r.MaxZ,6:F1}]        {r.Height,4:F1}");

            // ── 3. 场景对象 ──
            sb.AppendLine();
            sb.AppendLine("【三】第三章场景对象清单");
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter3_Graybox.unity",
                                         OpenSceneMode.Single);
            Physics.SyncTransforms();

            var ending = Object.FindFirstObjectByType<AbyssEcho.Narrative.ChapterEndingSystem>();
            sb.AppendLine($"  ChapterEndingSystem 数量 = {Object.FindObjectsByType<AbyssEcho.Narrative.ChapterEndingSystem>(FindObjectsSortMode.None).Length}");
            if (ending != null)
            {
                sb.AppendLine($"    @{ending.gameObject.name}");
                var tcF = t.GetField("triggerCenter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var tsF = t.GetField("triggerSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (tcF != null) sb.AppendLine($"    triggerCenter = {(Vector3)tcF.GetValue(ending)}");
                if (tsF != null) sb.AppendLine($"    triggerSize   = {(Vector3)tsF.GetValue(ending)}");
            }

            // 交互对象
            sb.AppendLine();
            sb.AppendLine("  可交互对象（实现 IInteractable 或有 Interactable 基类）：");
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (mb == null) continue;
                var mt = mb.GetType();
                bool isInteractable = false;
                foreach (var i in mt.GetInterfaces())
                    if (i.Name == "IInteractable") { isInteractable = true; break; }
                if (!isInteractable) continue;
                sb.AppendLine($"    {mb.gameObject.name,-32} {mt.Name,-28} @ {mb.transform.position}");
            }

            // 道具
            sb.AppendLine();
            sb.AppendLine("  Pickup / 道具：");
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (mb == null) continue;
                string n = mb.GetType().Name;
                if (n.Contains("Pickup") || n.Contains("Item") || n.Contains("Inventory"))
                    sb.AppendLine($"    {mb.gameObject.name,-32} {n,-28} @ {mb.transform.position}");
            }

            // 谜题
            sb.AppendLine();
            sb.AppendLine("  谜题 / 机关：");
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (mb == null) continue;
                string n = mb.GetType().Name;
                if (n.Contains("Puzzle") || n.Contains("Altar") || n.Contains("Console") ||
                    n.Contains("Panel") || n.Contains("Switch") || n.Contains("Lever"))
                    sb.AppendLine($"    {mb.gameObject.name,-32} {n,-28} @ {mb.transform.position}");
            }

            // ── 4. 第三章结局文本 ──
            sb.AppendLine();
            sb.AppendLine("【四】第三章结局文本（ChapterEndingLines）");
            var lt = typeof(AbyssEcho.Narrative.ChapterEndingLines);
            foreach (var m in lt.GetMethods(System.Reflection.BindingFlags.Public |
                                            System.Reflection.BindingFlags.Static |
                                            System.Reflection.BindingFlags.DeclaredOnly))
                sb.AppendLine($"    {m.ReturnType.Name} {m.Name}({string.Join(", ", System.Array.ConvertAll(m.GetParameters(), x => x.ParameterType.Name + " " + x.Name))})");

            // ── 5. 章节跳转 ──
            sb.AppendLine();
            sb.AppendLine("【五】章节跳转配置");
            var ce = typeof(AbyssEcho.Narrative.ChapterEndingSystem);
            sb.AppendLine("  ChapterEndingSystem 内部 GoToNextChapter 目标：");
            sb.AppendLine("    （需读源码确认，见报告）");

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch3EndingAudit.txt",
                              text, new UTF8Encoding(false));
        }
    }
}