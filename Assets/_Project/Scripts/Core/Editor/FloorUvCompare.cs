using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class FloorUvCompare {
        public static void Run() {
            var sb = new StringBuilder();

            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter1_Graybox.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();
            sb.AppendLine("══════ 第一章地板 UV ══════");
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)) {
                if (r.sharedMaterial == null || r.sharedMaterial.name != "M_Arch_FloorPlate") continue;
                var m = r.GetComponent<MeshFilter>()?.sharedMesh;
                if (m == null || m.uv == null) continue;
                float mnU=999,mxU=-999,mnV=999,mxV=-999;
                foreach (var v in m.uv) { mnU=Mathf.Min(mnU,v.x); mxU=Mathf.Max(mxU,v.x); mnV=Mathf.Min(mnV,v.y); mxV=Mathf.Max(mxV,v.y); }
                var ws = r.bounds.size;
                sb.AppendLine($"  {r.gameObject.name,-20} 尺寸={ws.x:F1}x{ws.z:F1} UV跨度={(mxU-mnU):F2}x{(mxV-mnV):F2}  tiling={r.sharedMaterial.GetTextureScale("_BaseMap")}");
                break;
            }

            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter2_Graybox.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();
            sb.AppendLine();
            sb.AppendLine("══════ 第二章地板 UV ══════");
            int n = 0;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)) {
                if (r.sharedMaterial == null || r.sharedMaterial.name != "M_Arch_FloorPlate") continue;
                if (n++ >= 5) break;
                var m = r.GetComponent<MeshFilter>()?.sharedMesh;
                if (m == null || m.uv == null) continue;
                float mnU=999,mxU=-999,mnV=999,mxV=-999;
                foreach (var v in m.uv) { mnU=Mathf.Min(mnU,v.x); mxU=Mathf.Max(mxU,v.x); mnV=Mathf.Min(mnV,v.y); mxV=Mathf.Max(mxV,v.y); }
                var ws = r.bounds.size;
                sb.AppendLine($"  {r.gameObject.name,-20} 尺寸={ws.x:F1}x{ws.z:F1} UV跨度={(mxU-mnU):F2}x{(mxV-mnV):F2}  tiling={r.sharedMaterial.GetTextureScale("_BaseMap")}");
            }

            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\FloorUvCompare.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}