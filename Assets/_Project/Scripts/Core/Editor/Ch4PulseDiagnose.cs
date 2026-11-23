using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S72a：心跳立方（心瓣）不亮的原因诊断。
    ///
    /// 症状：玩家反馈"心跳立方不亮了"
    ///
    /// 机制回顾（PulseSyncPuzzle.cs）：
    ///     · pulseRenderer 上的 Renderer 用 MaterialPropertyBlock 改颜色
    ///     · ApplyColor(Color.Lerp(idleColor, pulseColor, glow))
    ///     · MaterialPropertyBlock 只能覆盖**属性值**，不能覆盖贴图
    ///
    /// 怀疑：
    ///     S69 材质升级把核心的 M_Ch4_Flesh（纯色，能被 MPB 覆盖）
    ///     换成了 M_Org_FleshDeep（有贴图），
    ///     贴图把 BaseColor 的红色乘暗了 → 看不出搏动。
    ///
    /// 本工具输出：
    ///     · PulseSyncPuzzle 的位置与 pulseRenderer 指向
    ///     · 心瓣 Renderer 的材质名 / 是否有 BaseMap / 当前颜色
    ///     · 是否所有相关对象都被改了材质
    ///
    /// 只读。
    /// </summary>
    public static class Ch4PulseDiagnose
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════");
            sb.AppendLine("  心跳立方（心瓣）诊断");
            sb.AppendLine("═══════════════════════════════════════════════");

            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter4_Graybox.unity",
                                         OpenSceneMode.Single);
            Physics.SyncTransforms();

            var pulse = Object.FindFirstObjectByType<AbyssEcho.Puzzle.PulseSyncPuzzle>();
            if (pulse == null) { sb.AppendLine("❌ 找不到 PulseSyncPuzzle"); Dump(sb); return; }

            sb.AppendLine($"PulseSyncPuzzle 挂在: {pulse.gameObject.name}");
            sb.AppendLine($"  位置 = {pulse.transform.position}");
            sb.AppendLine($"  requiredSyncs = {pulse.requiredSyncs}");
            sb.AppendLine($"  pulseColor = {pulse.pulseColor}");
            sb.AppendLine($"  idleColor  = {pulse.idleColor}");

            // pulseRenderer
            var t = typeof(AbyssEcho.Puzzle.PulseSyncPuzzle);
            var prF = t.GetField("pulseRenderer",
                                 System.Reflection.BindingFlags.Instance |
                                 System.Reflection.BindingFlags.Public |
                                 System.Reflection.BindingFlags.NonPublic);
            var pr = prF?.GetValue(pulse) as Renderer;

            sb.AppendLine();
            sb.AppendLine("──── pulseRenderer ────");
            if (pr == null)
            {
                sb.AppendLine("  ❌ pulseRenderer 为空（心瓣不会亮）");
            }
            else
            {
                sb.AppendLine($"  对象 = {pr.gameObject.name}");
                sb.AppendLine($"  位置 = {pr.transform.position}");
                var mats = pr.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) { sb.AppendLine($"    [{i}] NULL"); continue; }
                    bool hasBase = m.GetTexture("_BaseMap") != null;
                    bool emit = m.IsKeywordEnabled("_EMISSION");
                    Color c = m.GetColor("_BaseColor");
                    sb.AppendLine($"    [{i}] {m.name,-24} BaseMap={(hasBase ? "✓有贴图" : "✗无")} " +
                                  $"Emission={(emit ? "✓" : "-")} BaseColor=({c.r:F2},{c.g:F2},{c.b:F2})");
                    if (hasBase)
                        sb.AppendLine($"         ⚠️ 有贴图 —— MaterialPropertyBlock 改颜色会被贴图压暗");
                }
            }

            // 核心相关对象
            sb.AppendLine();
            sb.AppendLine("──── 场景里名字含「核心」「心」「Pulse」「Heart」的对象 ────");
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r == null) continue;
                string n = r.gameObject.name;
                if (!n.Contains("核心") && !n.Contains("心") && !n.Contains("Pulse") &&
                    !n.Contains("Heart") && !n.Contains("立方")) continue;

                var m = r.sharedMaterial;
                bool hasBase = m != null && m.GetTexture("_BaseMap") != null;
                sb.AppendLine($"  {n,-24} 材质={(m != null ? m.name : "NULL"),-22} " +
                              $"贴图={(hasBase ? "✓" : "✗")} @{r.transform.position}");
            }

            // 检查 PulseSyncPuzzle 的 lightsToEnable / doorToUnlock
            var leF = t.GetField("lightsToEnable",
                                 System.Reflection.BindingFlags.Instance |
                                 System.Reflection.BindingFlags.Public |
                                 System.Reflection.BindingFlags.NonPublic);
            var doorF = t.GetField("doorToUnlock",
                                 System.Reflection.BindingFlags.Instance |
                                 System.Reflection.BindingFlags.Public |
                                 System.Reflection.BindingFlags.NonPublic);
            var lights = leF?.GetValue(pulse) as Light[];
            var door = doorF?.GetValue(pulse) as AbyssEcho.Interaction.DoorInteractable;

            sb.AppendLine();
            sb.AppendLine($"  lightsToEnable: {(lights == null ? "null" : lights.Length + " 个")}");
            if (lights != null)
                foreach (var l in lights)
                    if (l != null) sb.AppendLine($"    {l.gameObject.name} enabled={l.enabled} int={l.intensity:F1}");
            sb.AppendLine($"  doorToUnlock: {(door != null ? door.gameObject.name : "null")}");

            Dump(sb);
        }

        private static void Dump(StringBuilder sb)
        {
            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch4PulseDiagnose.txt",
                              text, new UTF8Encoding(false));
        }
    }
}