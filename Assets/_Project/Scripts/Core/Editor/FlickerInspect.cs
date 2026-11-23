using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    public static class FlickerInspect
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== FlickerLight / 灯光 / HorrorTrigger 交叉检查 ===");

            string[] scenes = {
                "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity",
                "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity",
                "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity",
                "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity",
                "Assets/_Project/Scenes/Level_Chapter5_Graybox.unity",
            };

            foreach (var p in scenes)
            {
                EditorSceneManager.OpenScene(p, OpenSceneMode.Single);
                sb.AppendLine();
                sb.AppendLine($"### {Path.GetFileNameWithoutExtension(p)}");

                var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
                sb.AppendLine($"  灯光总数 = {lights.Length}");

                // 按类型/所在房间分组
                var byType = new Dictionary<LightType, int>();
                foreach (var l in lights)
                {
                    if (!byType.ContainsKey(l.type)) byType[l.type] = 0;
                    byType[l.type]++;
                }
                foreach (var kv in byType) sb.AppendLine($"    {kv.Key}: {kv.Value}");

                // FlickerLight 详情
                var fls = Object.FindObjectsByType<AbyssEcho.Horror.FlickerLight>(FindObjectsSortMode.None);
                sb.AppendLine($"  FlickerLight = {fls.Length}");
                foreach (var f in fls)
                {
                    var lt = f.GetComponent<Light>();
                    var so = new SerializedObject(f);
                    var pMode = so.FindProperty("mode");
                    var pBase = so.FindProperty("baseIntensity");
                    var pMin = so.FindProperty("minInterval");
                    var pMax = so.FindProperty("maxInterval");
                    var pDur = so.FindProperty("flickerDuration");
                    var pDim = so.FindProperty("dimRatio");
                    var pFear = so.FindProperty("fearReactive");

                    sb.AppendLine($"    {f.gameObject.name}");
                    sb.AppendLine($"      mode={(pMode != null ? ((AbyssEcho.Horror.FlickerLight.Mode)pMode.enumValueIndex).ToString() : "?")} " +
                                  $"base={(pBase != null ? pBase.floatValue.ToString("F2") : "?")} " +
                                  $"interval={(pMin != null ? pMin.floatValue.ToString("F2") : "?")}~{(pMax != null ? pMax.floatValue.ToString("F2") : "?")} " +
                                  $"dur={(pDur != null ? pDur.floatValue.ToString("F2") : "?")} dim={(pDim != null ? pDim.floatValue.ToString("F2") : "?")} " +
                                  $"fearReactive={(pFear != null ? pFear.boolValue.ToString() : "?")}");
                    if (lt != null)
                        sb.AppendLine($"      Light: type={lt.type} intensity={lt.intensity:F2} range={lt.range:F1} color={lt.color} enabled={lt.enabled}");
                    sb.AppendLine($"      路径={GetPath(f.transform)}");
                }

                // HorrorTrigger 的 lightsToKill / lightsToFlicker
                sb.AppendLine("  --- HorrorTrigger 控制的灯 ---");
                var trigs = Object.FindObjectsByType<AbyssEcho.Horror.HorrorTrigger>(FindObjectsSortMode.None);
                var killedSet = new HashSet<string>();
                var flickerSet = new HashSet<string>();

                foreach (var t in trigs)
                {
                    var so = new SerializedObject(t);
                    var pKill = so.FindProperty("lightsToKill");
                    var pFlick = so.FindProperty("lightsToFlicker");

                    var killNames = new List<string>();
                    if (pKill != null)
                    {
                        for (int i = 0; i < pKill.arraySize; i++)
                        {
                            var el = pKill.GetArrayElementAtIndex(i);
                            if (el.objectReferenceValue != null)
                            { killNames.Add(el.objectReferenceValue.name); killedSet.Add(el.objectReferenceValue.name); }
                        }
                    }
                    sb.AppendLine($"    {t.gameObject.name}: lightsToKill=[{string.Join(", ", killNames)}]");
                }

                // 冲突检查：既被 HorrorTrigger 关闭、又有 FlickerLight 的灯
                sb.AppendLine("  --- 冲突检查 ---");
                int conflicts = 0;
                foreach (var f in fls)
                {
                    if (killedSet.Contains(f.gameObject.name))
                    {
                        sb.AppendLine($"    ⚠️ 冲突: {f.gameObject.name} 既被 HorrorTrigger.lightsToKill 关闭，又有 FlickerLight");
                        conflicts++;
                    }
                }
                if (conflicts == 0) sb.AppendLine("    ✅ 无冲突");
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports/FlickerInspect.txt")), text, new UTF8Encoding(false));
        }

        private static string GetPath(Transform t)
        {
            string p = t.name;
            var cur = t.parent; int g = 0;
            while (cur != null && g++ < 20) { p = cur.name + "/" + p; cur = cur.parent; }
            return p;
        }
    }
}