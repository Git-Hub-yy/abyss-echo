using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class ExactCount {
        public static void Run() {
            var sb = new StringBuilder();
            sb.AppendLine("=== 各章 M_Arch_FloorPlate / M_Arch_Ceiling 精确数量 ===");
            var scenes = new (int ch, string p)[] {
                (1,"Assets/_Project/Scenes/Level_Chapter1_Graybox.unity"),
                (2,"Assets/_Project/Scenes/Level_Chapter2_Graybox.unity"),
                (3,"Assets/_Project/Scenes/Level_Chapter3_Graybox.unity"),
                (4,"Assets/_Project/Scenes/Level_Chapter4_Graybox.unity"),
                (5,"Assets/_Project/Scenes/Level_Chapter5_Graybox.unity"),
            };
            int tf=0, tc=0;
            foreach (var (ch,p) in scenes) {
                EditorSceneManager.OpenScene(p, OpenSceneMode.Single);
                int f=0, c=0;
                foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)) {
                    if (r == null || r.sharedMaterial == null) continue;
                    if (r.sharedMaterial.name == "M_Arch_FloorPlate") f++;
                    else if (r.sharedMaterial.name == "M_Arch_Ceiling") c++;
                }
                sb.AppendLine($"第{ch}章: 地板={f}  天花板={c}");
                tf+=f; tc+=c;
            }
            sb.AppendLine($"合计: 地板={tf}  天花板={tc}");
            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\ExactCount.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}