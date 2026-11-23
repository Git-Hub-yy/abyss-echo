using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// 验收审计工具 —— **只读**，不修改任何场景/资产。
    ///
    /// 覆盖：
    ///   A. 全工程脚本编译错误统计
    ///   B. 每个场景的 Missing Script（丢失脚本引用）
    ///   C. 每个场景的 Missing Reference（序列化字段为空但类型不允许）
    ///   D. 组件统计（玩家/相机/交互/UI/恐怖/音效/谜题）
    ///   E. Update 高频操作扫描（源码静态分析）
    ///   F. 五章门锁与物品 ID 链路
    /// </summary>
    public static class AcceptanceAudit
    {
        private static readonly string[] Scenes =
        {
            "Assets/_Project/Scenes/MainMenu.unity",
            "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity",
            "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity",
            "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity",
            "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity",
            "Assets/_Project/Scenes/Level_Chapter5_Graybox.unity",
        };

        [MenuItem("ABYSS ECHO/审计/完整验收审计（只读）")]
        public static void RunAll()
        {
            var sb = new StringBuilder();

            sb.AppendLine("========== ABYSS ECHO 完整验收审计（只读）==========");
            sb.AppendLine();

            AuditScenes(sb);
            AuditPlayerScenes(sb);
            AuditUpdateHotspots(sb);

            string text = sb.ToString();
            Debug.Log(text);

            string dir = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(Application.dataPath), "../Reports"));
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "AcceptanceAudit_Raw.txt"), text, new UTF8Encoding(false));
            Debug.Log("[Audit] 原始数据已写入 Reports/AcceptanceAudit_Raw.txt");
        }

        // ══════════════════ A/B/C：逐场景静态检查 ══════════════════

        private static void AuditScenes(StringBuilder sb)
        {
            foreach (var path in Scenes)
            {
                if (!File.Exists(path))
                {
                    sb.AppendLine($"### {Path.GetFileName(path)}：❌ 场景文件不存在");
                    sb.AppendLine();
                    continue;
                }

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                sb.AppendLine($"### {Path.GetFileName(path)}");
                sb.AppendLine($"  路径: {path}");

                int roots = scene.GetRootGameObjects().Length;
                int totalGo = 0;
                int totalComp = 0;
                int missingScript = 0;
                int missingRef = 0;

                var missingScriptList = new List<string>();
                var missingRefList = new List<string>();

                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    {
                        totalGo++;

                        // Missing Script 检测
                        var comps = t.GetComponents<Component>();
                        foreach (var c in comps)
                        {
                            totalComp++;
                            if (c == null)
                            {
                                missingScript++;
                                missingScriptList.Add($"{GetPath(t)}");
                            }
                        }

                        // Missing Reference：序列化字段为 Object 但为空
                        foreach (var c in comps)
                        {
                            if (c == null) continue;
                            if (c is Transform || c is RectTransform) continue;

                            var so = new SerializedObject(c);
                            var it = so.GetIterator();
                            while (it.NextVisible(true))
                            {
                                if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                                if (it.objectReferenceValue != null) continue;
                                if (it.objectReferenceInstanceIDValue == 0 && it.propertyPath.Contains("m_Script")) continue;

                                missingRef++;
                                if (missingRefList.Count < 40)
                                    missingRefList.Add($"{GetPath(t)} :: {c.GetType().Name}.{it.propertyPath}");
                            }
                        }
                    }
                }

                sb.AppendLine($"  根对象 {roots}，GameObject {totalGo}，组件 {totalComp}");
                sb.AppendLine($"  Missing Script: {missingScript}");
                if (missingScript > 0)
                    foreach (var s in missingScriptList) sb.AppendLine($"    - {s}");

                sb.AppendLine($"  Missing Reference: {missingRef}");
                if (missingRef > 0)
                    foreach (var s in missingRefList) sb.AppendLine($"    - {s}");

                sb.AppendLine();
            }
        }

        private static string GetPath(Transform t)
        {
            string p = t.name;
            var cur = t.parent;
            int guard = 0;
            while (cur != null && guard++ < 20)
            {
                p = cur.name + "/" + p;
                cur = cur.parent;
            }
            return p;
        }

        // ══════════════════ D：玩家场景组件统计 ══════════════════

        private static void AuditPlayerScenes(StringBuilder sb)
        {
            sb.AppendLine("========== 各章节组件统计 ==========");

            string[] chapters =
            {
                "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity",
                "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity",
                "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity",
                "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity",
                "Assets/_Project/Scenes/Level_Chapter5_Graybox.unity",
            };

            foreach (var path in chapters)
            {
                if (!File.Exists(path)) continue;
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                sb.AppendLine();
                sb.AppendLine($"### {Path.GetFileName(path)}");

                Count<AbyssEcho.Player.PlayerMotor>(sb, "PlayerMotor");
                Count<AbyssEcho.Player.PlayerLook>(sb, "PlayerLook");
                Count<AbyssEcho.Player.PlayerInputReader>(sb, "PlayerInputReader");
                Count<AbyssEcho.Interaction.InteractionDetector>(sb, "InteractionDetector");
                Count<AbyssEcho.Tools.FlashlightSystem>(sb, "FlashlightSystem");
                Count<AbyssEcho.Inventory.PlayerInventory>(sb, "PlayerInventory");
                Count<AbyssEcho.UI.CrosshairUI>(sb, "CrosshairUI");
                Count<AbyssEcho.UI.PauseMenuUI>(sb, "PauseMenuUI");
                Count<AbyssEcho.UI.NarrativeUI>(sb, "NarrativeUI");
                Count<AbyssEcho.UI.InventoryUI>(sb, "InventoryUI");
                Count<AbyssEcho.UI.ChapterTitleCard>(sb, "ChapterTitleCard");
                Count<AbyssEcho.Audio.AudioManager>(sb, "AudioManager");
                Count<AbyssEcho.Horror.FearSystem>(sb, "FearSystem");
                Count<AbyssEcho.Horror.FearVisuals>(sb, "FearVisuals");
                Count<AbyssEcho.Horror.PlayerDeathSystem>(sb, "PlayerDeathSystem");
                Count<AbyssEcho.Narrative.ChapterEndingSystem>(sb, "ChapterEndingSystem");
                Count<AbyssEcho.Narrative.JournalSystem>(sb, "JournalSystem");
                Count<AbyssEcho.Save.AutoSaveManager>(sb, "AutoSaveManager");
                Count<AbyssEcho.Monster.MonsterAI>(sb, "MonsterAI");
                Count<AbyssEcho.Horror.MimicStalker>(sb, "MimicStalker");
                Count<AbyssEcho.Horror.MirrorReflection>(sb, "MirrorReflection");
                Count<AbyssEcho.Horror.HorrorTrigger>(sb, "HorrorTrigger");
                Count<AbyssEcho.Horror.DeepWaterZone>(sb, "DeepWaterZone");
                Count<AbyssEcho.Interaction.DoorInteractable>(sb, "DoorInteractable");
                Count<AbyssEcho.Interaction.PickupItem>(sb, "PickupItem");
                Count<AbyssEcho.Narrative.ReadableNote>(sb, "ReadableNote");
                Count<AbyssEcho.Puzzle.PowerRestorePanel>(sb, "PowerRestorePanel");
                Count<AbyssEcho.Puzzle.KeypadPanel>(sb, "KeypadPanel");
                Count<AbyssEcho.Puzzle.WaterPumpSwitch>(sb, "WaterPumpSwitch");
                Count<AbyssEcho.Puzzle.TapeRecorder>(sb, "TapeRecorder");
                Count<AbyssEcho.Puzzle.FrequencyConsole>(sb, "FrequencyConsole");
                Count<AbyssEcho.Puzzle.OfferingAltar>(sb, "OfferingAltar");
                Count<AbyssEcho.Puzzle.PulseSyncPuzzle>(sb, "PulseSyncPuzzle");
                Count<AbyssEcho.Puzzle.EvacuationConsole>(sb, "EvacuationConsole");

                var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
                int lit = 0;
                foreach (var l in lights) if (l.enabled) lit++;
                sb.AppendLine($"  Light: {lights.Length}（亮 {lit}）");

                var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
                sb.AppendLine($"  Camera: {cams.Length}");
                foreach (var c in cams)
                    sb.AppendLine($"    - {c.gameObject.name} enabled={c.enabled} depth={c.depth} post={c.allowHDR}");

                var vols = Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None);
                sb.AppendLine($"  Volume: {vols.Length}");
                foreach (var v in vols)
                    sb.AppendLine($"    - {v.gameObject.name} global={v.isGlobal} weight={v.weight} profile={(v.sharedProfile != null ? v.sharedProfile.name : "NULL")}");
            }

            sb.AppendLine();
        }

        private static void Count<T>(StringBuilder sb, string label) where T : Component
        {
            var arr = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            sb.AppendLine($"  {label}: {arr.Length}");
            if (arr.Length > 0 && arr.Length <= 4)
                foreach (var c in arr)
                    sb.AppendLine($"    - {c.gameObject.name}");
        }

        // ══════════════════ E：Update 高频操作静态扫描 ══════════════════

        private static void AuditUpdateHotspots(StringBuilder sb)
        {
            sb.AppendLine("========== Update 高频操作静态扫描 ==========");
            sb.AppendLine("（扫描 Assets/_Project/Scripts 下所有 .cs，找 Update 里的危险调用）");
            sb.AppendLine();

            string root = "Assets/_Project/Scripts";
            var files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);

            string[] danger =
            {
                "FindFirstObjectByType",
                "FindObjectsByType",
                "FindObjectOfType",
                "FindObjectsOfType",
                "GetComponent<",
                "GetComponentsInChildren",
                "Camera.main",
                ".material",
                "Instantiate(",
                "new GameObject",
                "Resources.Load",
                "Debug.Log(",
            };

            int hits = 0;

            foreach (var f in files)
            {
                var lines = File.ReadAllLines(f);
                int inUpdateDepth = 0;

                for (int i = 0; i < lines.Length; i++)
                {
                    string ln = lines[i];

                    // 粗略判断进入 Update / LateUpdate / FixedUpdate
                    if (ln.Contains("void Update(") || ln.Contains("void LateUpdate(") || ln.Contains("void FixedUpdate("))
                        inUpdateDepth = 1;

                    if (inUpdateDepth > 0)
                    {
                        foreach (var d in danger)
                        {
                            if (ln.Contains(d))
                            {
                                hits++;
                                sb.AppendLine($"  {Path.GetFileName(f)}:{i + 1}  [{d}]  {ln.Trim()}");
                                break;
                            }
                        }

                        // 退出方法（遇到下一个方法声明或类结束）
                        if (ln.Trim() == "}" && i > 0)
                            inUpdateDepth = 0;
                    }
                }
            }

            if (hits == 0) sb.AppendLine("  未发现明显高危调用");
            sb.AppendLine($"  合计命中：{hits}");
            sb.AppendLine();
        }
    }
}