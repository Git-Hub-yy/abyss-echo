using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AbyssEcho.Core.EditorTools
{
    public static class ProfileDetail
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Volume_Main 详细内容 ===");

            var prof = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Project/Settings/Volume_Main.asset");
            if (prof == null) { sb.AppendLine("❌ 加载失败"); Debug.Log(sb.ToString()); return; }

            sb.AppendLine($"资源路径 = Assets/_Project/Settings/Volume_Main.asset");
            sb.AppendLine($"组件数量 = {prof.components.Count}");
            sb.AppendLine();

            foreach (var c in prof.components)
            {
                if (c == null) { sb.AppendLine("<NULL>"); continue; }

                // active 是 public field (VolumeComponent.active)
                bool active = c.active;
                sb.AppendLine($"--- {c.GetType().Name} ---");
                sb.AppendLine($"  active = {active}");

                // 列出所有 VolumeParameter<float> 类型的字段及其值
                var fields = c.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
                foreach (var f in fields)
                {
                    if (f.FieldType == typeof(ClampedFloatParameter) ||
                        f.FieldType == typeof(FloatParameter) ||
                        f.FieldType == typeof(ColorParameter) ||
                        f.FieldType == typeof(BoolParameter))
                    {
                        object val = f.GetValue(c);
                        string vstr = "?";
                        try
                        {
                            var vf = val.GetType().GetProperty("value");
                            vstr = vf != null ? vf.GetValue(val)?.ToString() : val?.ToString();

                            // overrideState
                            var of = val.GetType().GetField("overrideState");
                            if (of != null) vstr += $"  (overrideState={of.GetValue(val)})";
                        }
                        catch { }
                        sb.AppendLine($"    {f.Name} = {vstr}");
                    }
                }
                sb.AppendLine();
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports/ProfileDetail.txt")), text, new UTF8Encoding(false));
        }
    }
}