using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S50：给第 2~5 章挑选合适的灯光加 FlickerLight。
    ///
    /// ══════════════════ 挑选原则（不是机械复制 16 个）══════════════════
    ///
    ///   ① **只给当前已点亮的灯加**
    ///      大量 Emergency 灯处于 intensity=0 / enabled=false（未通电）。
    ///      给它们加 FlickerLight 会让"本该灭的灯"闪起来 —— 逻辑错误。
    ///      筛选条件：light.enabled == true && light.intensity > 0.5f
    ///
    ///   ② **优先恐怖价值高的灯**
    ///      顺序：Warning（橙黄警示） > Emergency（红应急） > Ceiling（常规）
    ///      Warning 灯闪烁最有恐怖氛围（"警告灯接触不良"）
    ///
    ///   ③ **控制数量，避免全场景乱闪**
    ///      第一章 82 盏灯里有 16 个 FlickerLight（约 20%）。
    ///      本次给 Ch2~Ch5 各选 10~12 个，保持"部分灯不稳"的感觉。
    ///
    ///   ④ **不选 HorrorTrigger 要关的灯**
    ///      Ch2~Ch5 的 lightsToKill 都是空的，但代码仍会检查，
    ///      以防将来配置变化导致冲突。
    ///
    ///   ⑤ **不用 Dying 模式**
    ///      DoDying() 会执行 _light.enabled = false（永久熄灯），
    ///      容易与恐怖事件冲突。统一用 Flicker 模式。
    ///
    /// ══════════════════ 参数 ══════════════════
    ///
    ///   全部使用与第一章相同的参数：
    ///     mode=Flicker  minInterval=1.2→1.5  maxInterval=4.5→5.5
    ///     flickerDuration=0.12  dimRatio=0.12  fearReactive=true
    ///   baseIntensity 取该灯当前的 intensity（保持原亮度不变）
    /// </summary>
    public static class FlickerBuilder
    {
        private const string SceneFmt = "Assets/_Project/Scenes/Level_Chapter{0}_Graybox.unity";

        /// <summary>每章目标数量（不是 16，按各章灯光布局定）。</summary>
        private static int TargetCount(int ch)
        {
            switch (ch)
            {
                case 2: return 12;   // 第二章灯光最多（82 盏），选 12 个
                case 3: return 10;   // 第三章 81 盏
                case 4: return 10;   // 第四章 81 盏
                case 5: return 10;   // 第五章 81 盏
                default: return 10;
            }
        }

        [MenuItem("ABYSS ECHO/修复 v5.6/补 FlickerLight - 第二章")]
        public static void BuildCh2() => BuildFor(2);

        [MenuItem("ABYSS ECHO/修复 v5.6/补 FlickerLight - 第三章")]
        public static void BuildCh3() => BuildFor(3);

        [MenuItem("ABYSS ECHO/修复 v5.6/补 FlickerLight - 第四章")]
        public static void BuildCh4() => BuildFor(4);

        [MenuItem("ABYSS ECHO/修复 v5.6/补 FlickerLight - 第五章")]
        public static void BuildCh5() => BuildFor(5);

        public static void BuildFor(int chapterNum)
        {
            string path = string.Format(SceneFmt, chapterNum);
            if (!File.Exists(path)) { Fail(chapterNum, $"场景不存在：{path}"); return; }

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Log(chapterNum, $"已打开 {path}");

            // ── 收集 HorrorTrigger 要关的灯（冲突规避）──
            var protectedLights = new HashSet<string>();
            foreach (var t in Object.FindObjectsByType<AbyssEcho.Horror.HorrorTrigger>(FindObjectsSortMode.None))
            {
                var so = new SerializedObject(t);
                var pKill = so.FindProperty("lightsToKill");
                if (pKill == null) continue;
                for (int i = 0; i < pKill.arraySize; i++)
                {
                    var el = pKill.GetArrayElementAtIndex(i);
                    if (el.objectReferenceValue != null)
                        protectedLights.Add(el.objectReferenceValue.name);
                }
            }
            Log(chapterNum, $"HorrorTrigger 保护灯数量 = {protectedLights.Count}");

            // ── 已存在的 FlickerLight ──
            var existing = Object.FindObjectsByType<AbyssEcho.Horror.FlickerLight>(FindObjectsSortMode.None);
            var existingNames = new HashSet<string>();
            foreach (var e in existing) existingNames.Add(e.gameObject.name);
            Log(chapterNum, $"已存在 FlickerLight = {existing.Length}");

            // ── 挑选候选灯 ──
            var candidates = new List<(Light light, int priority)>();

            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l == null) continue;
                if (l.type != LightType.Point) continue;               // 只处理点光源（排除 Directional）
                if (!l.enabled || l.intensity <= 0.5f) continue;       // ★ 只给已点亮的灯
                if (protectedLights.Contains(l.gameObject.name)) continue;   // ★ 跳过会被恐怖事件关掉的
                if (existingNames.Contains(l.gameObject.name)) continue;     // 已有组件

                // 优先级：Warning > Emergency > Ceiling
                int prio;
                string n = l.gameObject.name;
                if (n.Contains("Warning")) prio = 0;
                else if (n.Contains("Emergency")) prio = 1;
                else prio = 2;

                candidates.Add((l, prio));
            }

            // 排序：先按优先级，再按名字（保证可重复执行结果一致）
            candidates.Sort((a, b) =>
            {
                if (a.priority != b.priority) return a.priority.CompareTo(b.priority);
                return string.Compare(a.light.gameObject.name, b.light.gameObject.name);
            });

            Log(chapterNum, $"可用候选灯 = {candidates.Count}（已点亮且未被保护的 Point 灯）");

            // ══════════════════ 按世界坐标去重（重要）══════════════════
            //
            //   排查发现：本项目大量灯光的世界坐标重合。
            //     Ch1: 82 盏灯 → 只有 67 个唯一坐标
            //     Ch3: 81 盏灯 → 只有 46 个唯一坐标（(0,2.8,0) 就有 11 盏）
            //
            //   这是 RoomLightBuilder 的历史问题（房间中心相同的灯叠在一起），
            //   **不属于本批修复范围**，但会影响"选哪些灯闪烁"：
            //   如果选中同坐标的多盏灯，闪烁效果会全部堆在同一个点上，
            //   看起来像"某一个点疯狂闪"，而不是"场景里的灯在各自闪烁"。
            //
            //   所以这里按坐标去重：同一坐标只保留一盏。
            var usedPositions = new HashSet<string>();
            foreach (var e in existing)
            {
                Vector3 ep = e.transform.position;
                usedPositions.Add($"{ep.x:F1},{ep.y:F1},{ep.z:F1}");
            }

            var deduped = new List<(Light light, int priority)>();
            var skippedDup = 0;

            foreach (var c in candidates)
            {
                Vector3 wp = c.light.transform.position;
                string key = $"{wp.x:F1},{wp.y:F1},{wp.z:F1}";
                if (!usedPositions.Add(key)) { skippedDup++; continue; }
                deduped.Add(c);
            }

            candidates = deduped;
            Log(chapterNum, $"坐标去重后候选 = {candidates.Count}（跳过同坐标 {skippedDup} 盏）");

            int target = TargetCount(chapterNum) - existing.Length;
            if (target < 0) target = 0;

            Log(chapterNum, $"本次目标添加数量 = {target}（最终总数 {existing.Length + target}）");

            int added = 0;
            var addedNames = new List<string>();

            foreach (var c in candidates)
            {
                if (added >= target) break;

                var lt = c.light;
                float originalIntensity = lt.intensity;

                var fl = lt.gameObject.AddComponent<AbyssEcho.Horror.FlickerLight>();
                fl.mode = AbyssEcho.Horror.FlickerLight.Mode.Flicker;

                // ⚠️ baseIntensity 必须设为该灯当前的 intensity，
                //   否则 FlickerLight.Awake() 里 baseIntensity <= 0 才取 intensity，
                //   而默认值是 2.5 —— 会让亮度突变！
                fl.baseIntensity = originalIntensity;

                // 与前章一致（FlickerLight 脚本默认值就是这些，显式写出便于核对）
                fl.minInterval = 1.2f;
                fl.maxInterval = 4.5f;
                fl.flickerDuration = 0.12f;
                fl.dimRatio = 0.12f;
                fl.fearReactive = true;

                EditorUtility.SetDirty(fl);
                added++;
                addedNames.Add($"{lt.gameObject.name}(int={originalIntensity:F1})");

                Log(chapterNum, $"  + {lt.gameObject.name}  baseIntensity={originalIntensity:F1}  " +
                                $"（{lt.transform.parent?.name ?? "?"}）");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Log(chapterNum, saved ? $"场景已保存（新增 {added} 个）" : "❌ 保存失败");

            Verify(chapterNum, addedNames, protectedLights);
        }

        private static void Verify(int ch, List<string> addedNames, HashSet<string> protectedLights)
        {
            var all = Object.FindObjectsByType<AbyssEcho.Horror.FlickerLight>(FindObjectsSortMode.None);
            var allLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);

            Log(ch, "---- 结果 ----");
            Log(ch, $"  FlickerLight 总数 = {all.Length}");
            Log(ch, $"  场景灯光总数 = {allLights.Length}");

            int conflict = 0;
            int dyingMode = 0;
            foreach (var f in all)
            {
                if (protectedLights.Contains(f.gameObject.name)) { conflict++; Log(ch, $"    ⚠️ 冲突：{f.gameObject.name}"); }
                if (f.mode == AbyssEcho.Horror.FlickerLight.Mode.Dying) dyingMode++;
            }
            Log(ch, $"  与 HorrorTrigger 冲突数 = {conflict}");
            Log(ch, $"  使用 Dying 模式的数量 = {dyingMode}（应为 0）");

            // 检查是否有灯被错误设为 0 亮度
            int zeroIntensity = 0;
            foreach (var f in all)
            {
                if (f.baseIntensity <= 0.5f) { zeroIntensity++; Log(ch, $"    ⚠️ {f.gameObject.name} baseIntensity={f.baseIntensity}"); }
            }
            Log(ch, $"  baseIntensity 异常(<=0.5) 数量 = {zeroIntensity}");

            Log(ch, "  新增清单：");
            foreach (var n in addedNames) Log(ch, $"    {n}");

            // 未触碰的其他系统
            Log(ch, $"  （未触碰）FearSystem={Object.FindObjectsByType<AbyssEcho.Horror.FearSystem>(FindObjectsSortMode.None).Length} " +
                    $"FearVisuals={Object.FindObjectsByType<AbyssEcho.Horror.FearVisuals>(FindObjectsSortMode.None).Length} " +
                    $"Volume={Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None).Length} " +
                    $"Footsteps={Object.FindObjectsByType<AbyssEcho.Horror.PlayerFootsteps>(FindObjectsSortMode.None).Length} " +
                    $"Death={Object.FindObjectsByType<AbyssEcho.Horror.PlayerDeathSystem>(FindObjectsSortMode.None).Length}");

            Log(ch, "完成");
        }

        private static void Log(int ch, string msg)
        {
            string line = $"[FlickerBuilder] Ch{ch} | {msg}";
            Debug.Log(line);
            Write(line);
        }

        private static void Fail(int ch, string msg)
        {
            string line = $"[FlickerBuilder] Ch{ch} | ❌ {msg}";
            Debug.LogError(line);
            Write(line);
        }

        private static void Write(string line)
        {
            try
            {
                string dir = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "FlickerBuilder_Result.txt"),
                                   line + "\n", new System.Text.UTF8Encoding(false));
            }
            catch { }
        }
    }
}