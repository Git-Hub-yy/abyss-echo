using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class WallUvCheck {
        public static void Run() {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter1_Graybox.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();

            sb.AppendLine("══════ 墙壁对象的网格与 UV ══════");
            int checkedN = 0;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)) {
                if (r.sharedMaterial == null) continue;
                if (r.sharedMaterial.name != "M_Arch_WallSteel") continue;
                if (checkedN++ >= 6) break;

                var mf = r.GetComponent<MeshFilter>();
                var mesh = mf != null ? mf.sharedMesh : null;
                var rend = r.GetComponent<Renderer>();
                sb.AppendLine($"  {r.gameObject.name} @ {r.transform.position}");
                sb.AppendLine($"    缩放 = {r.transform.lossyScale}  旋转={r.transform.eulerAngles}");
                if (mesh != null) {
                    sb.AppendLine($"    网格 = {mesh.name}  顶点={mesh.vertexCount}");
                    var uv = mesh.uv;
                    if (uv != null && uv.Length > 0) {
                        float minU=999,maxU=-999,minV=999,maxV=-999;
                        foreach (var v in uv) { minU=Mathf.Min(minU,v.x); maxU=Mathf.Max(maxU,v.x); minV=Mathf.Min(minV,v.y); maxV=Mathf.Max(maxV,v.y); }
                        sb.AppendLine($"    UV 范围: U[{minU:F3},{maxU:F3}] V[{minV:F3},{maxV:F3}]");
                        sb.AppendLine($"    UV 跨度: {(maxU-minU):F3} x {(maxV-minV):F3}");
                    } else sb.AppendLine("    ❌ 无 UV");
                }
                var mat = r.sharedMaterial;
                sb.AppendLine($"    材质 Tiling = {mat.GetTextureScale("_BaseMap")}");
                sb.AppendLine($"    世界尺寸 = {rend.bounds.size}");
            }

            sb.AppendLine();
            sb.AppendLine("══════ 对比：地板（正常）══════");
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)) {
                if (r.sharedMaterial == null || r.sharedMaterial.name != "M_Arch_FloorPlate") continue;
                var mesh = r.GetComponent<MeshFilter>()?.sharedMesh;
                sb.AppendLine($"  {r.gameObject.name} 缩放={r.transform.lossyScale} 世界尺寸={r.GetComponent<Renderer>().bounds.size}");
                if (mesh != null && mesh.uv != null && mesh.uv.Length > 0) {
                    var uv = mesh.uv;
                    float minU=999,maxU=-999,minV=999,maxV=-999;
                    foreach (var v in uv) { minU=Mathf.Min(minU,v.x); maxU=Mathf.Max(maxU,v.x); minV=Mathf.Min(minV,v.y); maxV=Mathf.Max(maxV,v.y); }
                    sb.AppendLine($"    UV 跨度: {(maxU-minU):F3} x {(maxV-minV):F3}  网格={mesh.name}");
                }
                break;
            }

            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\WallUvCheck.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}