using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class PathProbe {
        public static void Run() {
            var sb = new StringBuilder();
            sb.AppendLine("══════ 路径解析测试 ══════");
            string[] tests = {
                "Assets/Art/Materials/Architecture/M_Arch_WallSteel.mat",
                "Assets/Art/Materials/Architecture/M_Arch_WallSteel",
                "Assets/_Project/Art/Materials/Level/M_Wall_Metal.mat",
                "Assets/_Project/Art/Materials/Props/M_Prop_Metal.mat",
            };
            foreach (var p in tests) {
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                sb.AppendLine($"  LoadAssetAtPath<Material>(\"{p}\") = {(m != null ? m.name : "NULL")}");
                sb.AppendLine($"    AssetDatabase.LoadMainAssetAtPath = {(AssetDatabase.LoadMainAssetAtPath(p) != null ? "有" : "NULL")}");
                var o = AssetDatabase.LoadAssetAtPath<Object>(p);
                sb.AppendLine($"    LoadAssetAtPath<Object> = {(o != null ? o.GetType().Name : "NULL")}");
            }
            sb.AppendLine();
            sb.AppendLine("══════ FindAssets 搜索 ══════");
            var gs = AssetDatabase.FindAssets("t:Material", new[] {"Assets/Art/Materials"});
            sb.AppendLine($"  Assets/Art/Materials 下 = {gs.Length} 个");
            foreach (var g in gs) sb.AppendLine($"    {AssetDatabase.GUIDToAssetPath(g)}");

            sb.AppendLine();
            sb.AppendLine("══════ GUID 直查 ══════");
            var path = AssetDatabase.GUIDToAssetPath("dbd4b0b70e7d967438eb2acab8dac58");
            sb.AppendLine($"  GUID -> path = \"{path}\"");

            sb.AppendLine();
            sb.AppendLine("══════ 文件系统检查 ══════");
            string fs = Path.Combine(Application.dataPath, "Art/Materials/Architecture/M_Arch_WallSteel.mat");
            sb.AppendLine($"  File.Exists(\"{fs}\") = {File.Exists(fs)}");

            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\PathProbe.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}