using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class TexelDensityCheck {
        public static void Run() {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter1_Graybox.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();

            sb.AppendLine("══════ 每米 UV 密度（考虑 Tiling 后实际值）══════");
            sb.AppendLine("  期望：墙面 U/米 ≈ V/米 ≈ 0.5~1.0（每 1-2 米一个贴图循环）");
            sb.AppendLine();
            sb.AppendLine($"{"材质",-24} {"对象",-28} {"世界尺寸",-22} {"材质Tiling",-14} 实际U/米  实际V/米");
            sb.AppendLine(new string('-', 110));

            var seen = new System.Collections.Generic.HashSet<string>();
            int bad = 0, total = 0;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)) {
                if (r.sharedMaterial == null) continue;
                string matName = r.sharedMaterial.name;
                if (!matName.StartsWith("M_Arch") && !matName.StartsWith("M_Mach")) continue;

                var m = r.GetComponent<MeshFilter>()?.sharedMesh;
                if (m == null || m.uv == null || m.uv.Length == 0) continue;
                if (!seen.Add(matName)) continue;

                float mnU=999,mxU=-999,mnV=999,mxV=-999;
                foreach (var v in m.uv) { mnU=Mathf.Min(mnU,v.x); mxU=Mathf.Max(mxU,v.x); mnV=Mathf.Min(mnV,v.y); mxV=Mathf.Max(mxV,v.y); }
                var ws = r.bounds.size;
                var tiling = r.sharedMaterial.GetTextureScale("_BaseMap");

                float w1, w2;
                if (ws.x < ws.z && ws.z >= ws.y) { w1 = ws.z; w2 = ws.y; }
                else if (ws.z < ws.x && ws.x >= ws.y) { w1 = ws.x; w2 = ws.y; }
                else { w1 = Mathf.Max(ws.x, ws.z); w2 = Mathf.Max(ws.x, ws.z); }

                float au = (mxU-mnU) * tiling.x / Mathf.Max(0.1f, w1);
                float av = (mxV-mnV) * tiling.y / Mathf.Max(0.1f, w2);
                bool ok = au >= 0.3f && au <= 1.5f && av >= 0.3f && av <= 1.5f;
                total++;
                if (!ok) bad++;

                sb.AppendLine($"{matName,-24} {r.gameObject.name,-28} {ws.x:F1}x{ws.y:F1}x{ws.z:F1}      {tiling.x:F1}x{tiling.y:F1}      {au:F3}    {av:F3}   {(ok?"✓":"❌")}");
            }

            sb.AppendLine();
            sb.AppendLine($"检查 {total} 种材质，异常 {bad} 种");

            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\TexelDensityCheck.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}