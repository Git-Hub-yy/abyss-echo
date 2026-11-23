using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    public static class CoreObjInspect
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter4_Graybox.unity", OpenSceneMode.Single);

            sb.AppendLine("══════ 『核心』物体详细检查 ══════");

            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.name != "核心") continue;

                string path = GetPath(t);
                sb.AppendLine();
                sb.AppendLine($"● {path}");
                sb.AppendLine($"  位置 = {t.position}");
                sb.AppendLine($"  本地位置 = {t.localPosition}");
                sb.AppendLine($"  旋转 = {t.eulerAngles}");
                sb.AppendLine($"  本地缩放 = {t.localScale}");
                sb.AppendLine($"  世界缩放 = {t.lossyScale}");
                sb.AppendLine($"  active={t.gameObject.activeInHierarchy}");

                foreach (var c in t.GetComponents<Collider>())
                {
                    string size = "";
                    if (c is BoxCollider bc)
                    {
                        Vector3 ws = Vector3.Scale(bc.size, t.lossyScale);
                        size = $" size={bc.size} 世界尺寸={ws} center={bc.center}";
                        Vector3 worldCenter = t.TransformPoint(bc.center);
                        sb.AppendLine($"    世界范围 x[{worldCenter.x - ws.x/2:F2},{worldCenter.x + ws.x/2:F2}] " +
                                      $"y[{worldCenter.y - ws.y/2:F2},{worldCenter.y + ws.y/2:F2}] " +
                                      $"z[{worldCenter.z - ws.z/2:F2},{worldCenter.z + ws.z/2:F2}]");
                    }
                    sb.AppendLine($"  Collider = {c.GetType().Name}{size} isTrigger={c.isTrigger} enabled={c.enabled}");
                }

                var comps = t.GetComponents<Component>();
                var names = new System.Collections.Generic.List<string>();
                foreach (var c in comps) if (c != null) names.Add(c.GetType().Name);
                sb.AppendLine($"  组件 = {string.Join(", ", names)}");

                // 父子关系
                sb.AppendLine($"  父 = {(t.parent != null ? t.parent.name : "无")}");
                foreach (Transform child in t)
                    sb.AppendLine($"    子 = {child.name} @ {child.position} scale={child.lossyScale}");

                // 是否有 Renderer
                var r = t.GetComponent<Renderer>();
                if (r != null)
                    sb.AppendLine($"  Renderer 材质 = {(r.sharedMaterial != null ? r.sharedMaterial.name : "无")}");
            }

            // 心瓣与核心的相对关系
            sb.AppendLine();
            sb.AppendLine("──── 心瓣 vs 核心 ────");
            var valve = GameObject.Find("HeartValve");
            var core = GameObject.Find("核心");
            if (valve != null && core != null)
            {
                sb.AppendLine($"  心瓣位置 = {valve.transform.position}  尺寸 = {valve.transform.lossyScale}");
                sb.AppendLine($"  核心位置 = {core.transform.position}  尺寸 = {core.transform.lossyScale}");
                sb.AppendLine($"  水平距离 = {Vector3.Distance(new Vector3(valve.transform.position.x,0,valve.transform.position.z), new Vector3(core.transform.position.x,0,core.transform.position.z)):F2}m");

                var coreCol = core.GetComponent<Collider>();
                if (coreCol != null)
                {
                    bool inside = coreCol.bounds.Contains(valve.transform.position);
                    sb.AppendLine($"  心瓣是否在核心包围盒内 = {(inside ? "❌ 是（被包住）" : "✅ 否")}");
                    sb.AppendLine($"  核心包围盒 center={coreCol.bounds.center} size={coreCol.bounds.size}");
                }
            }

            // 心瓣交互距离
            sb.AppendLine();
            sb.AppendLine("──── 心瓣交互配置 ────");
            if (valve != null)
            {
                var pulse = valve.GetComponent<AbyssEcho.Puzzle.PulseSyncPuzzle>();
                if (pulse != null)
                {
                    var so = new SerializedObject(pulse);
                    var it = so.FindProperty("iteractDistance");
                    if (it == null) it = so.FindProperty("interactDistance");
                    sb.AppendLine($"  interactDistance = {(it != null ? it.floatValue.ToString() : "未找到字段")}");

                    // 列出所有可序列化字段
                    var iter = so.GetIterator();
                    while (iter.NextVisible(true))
                    {
                        if (iter.propertyType == SerializedPropertyType.Float)
                            sb.AppendLine($"    {iter.name} = {iter.floatValue}");
                    }
                }

                var baseType = typeof(AbyssEcho.Interaction.InteractableBase);
                var distField = baseType.GetField("interactDistance", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                if (distField != null)
                    sb.AppendLine($"  InteractableBase.interactDistance = {distField.GetValue(pulse)}");
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports/CoreObjInspect.txt")), text, new UTF8Encoding(false));
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