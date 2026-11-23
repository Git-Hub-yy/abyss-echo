using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S52：把五章场景里 FlashlightSystem 的 normalIntensity 从 3.2 改为 15。
    ///
    /// ══════════════════ 为什么必须在场景里改 ══════════════════
    ///
    ///   `normalIntensity` 是 [SerializeField]，会被序列化进场景。
    ///   只改脚本里的默认值（= 15f）对**已经保存的场景无效** ——
    ///   场景里存的是旧值 3.2，加载时以场景为准。
    ///
    /// ══════════════════ 只改一个字段 ══════════════════
    ///
    ///   · normalIntensity: 3.2 → 15
    ///   · range / spotAngle / 其他所有字段：保持不变
    ///   · 不新增/删除任何组件
    /// </summary>
    public static class FlashlightIntensityBuilder
    {
        private const float NEW_INTENSITY = 15f;
        private const string SceneFmt = "Assets/_Project/Scenes/Level_Chapter{0}_Graybox.unity";

        [MenuItem("ABYSS ECHO/修复 v5.8/手电筒强度 - 第一章")]
        public static void BuildCh1() => BuildFor(1);

        [MenuItem("ABYSS ECHO/修复 v5.8/手电筒强度 - 第二章")]
        public static void BuildCh2() => BuildFor(2);

        [MenuItem("ABYSS ECHO/修复 v5.8/手电筒强度 - 第三章")]
        public static void BuildCh3() => BuildFor(3);

        [MenuItem("ABYSS ECHO/修复 v5.8/手电筒强度 - 第四章")]
        public static void BuildCh4() => BuildFor(4);

        [MenuItem("ABYSS ECHO/修复 v5.8/手电筒强度 - 第五章")]
        public static void BuildCh5() => BuildFor(5);

        public static void BuildFor(int chapterNum)
        {
            string path = string.Format(SceneFmt, chapterNum);
            if (!File.Exists(path)) { Fail(chapterNum, $"场景不存在：{path}"); return; }

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Log(chapterNum, $"已打开 {path}");

            int found = 0, changed = 0;

            // 用 SerializedObject 精确改字段，避免手写 YAML
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (mb == null) continue;
                if (mb.GetType() != typeof(AbyssEcho.Tools.FlashlightSystem)) continue;

                found++;
                var so = new SerializedObject(mb);
                var prop = so.FindProperty("normalIntensity");
                if (prop == null) { Log(chapterNum, "  ❌ 找不到 normalIntensity 属性"); continue; }

                float oldVal = prop.floatValue;
                Log(chapterNum, $"  {GetPath(mb.transform)}  normalIntensity: {oldVal} → {NEW_INTENSITY}");

                if (Mathf.Approximately(oldVal, NEW_INTENSITY))
                {
                    Log(chapterNum, "    已是最新值，跳过");
                    continue;
                }

                prop.floatValue = NEW_INTENSITY;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(mb);
                changed++;
            }

            Log(chapterNum, $"找到 FlashlightSystem = {found}，实际修改 = {changed}");

            if (changed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                bool saved = EditorSceneManager.SaveScene(scene, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Log(chapterNum, saved ? "场景已保存" : "❌ 保存失败");
            }
            else
            {
                Log(chapterNum, "无改动，不保存");
            }

            // ── 验证 ──
            var fl = Object.FindFirstObjectByType<AbyssEcho.Tools.FlashlightSystem>();
            if (fl != null)
            {
                var f = typeof(AbyssEcho.Tools.FlashlightSystem)
                    .GetField("normalIntensity", BindingFlags.Instance | BindingFlags.NonPublic);
                float v = (float)f.GetValue(fl);
                Log(chapterNum, $"---- 验证 ----");
                Log(chapterNum, $"  normalIntensity 运行时读取 = {v}");
                Log(chapterNum, $"  期望值 = {NEW_INTENSITY}  {(Mathf.Approximately(v, NEW_INTENSITY) ? "✅" : "❌")}");
            }

            Log(chapterNum, "完成");
        }

        private static string GetPath(Transform t)
        {
            string p = t.name;
            var cur = t.parent; int g = 0;
            while (cur != null && g++ < 20) { p = cur.name + "/" + p; cur = cur.parent; }
            return p;
        }

        private static void Log(int ch, string msg)
        {
            string line = $"[FlashIntensity] Ch{ch} | {msg}";
            Debug.Log(line);
            Write(line);
        }

        private static void Fail(int ch, string msg)
        {
            string line = $"[FlashIntensity] Ch{ch} | ❌ {msg}";
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
                File.AppendAllText(Path.Combine(dir, "FlashlightIntensity_Result.txt"),
                                   line + "\n", new System.Text.UTF8Encoding(false));
            }
            catch { }
        }
    }
}