using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class Ch5UvVerifyAfter {
        public static void Run() {
            var sb = new StringBuilder();
            sb.AppendLine("=== 第五章墙体 UV 修复后验证 ===");
            sb.AppendLine();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter5_Graybox.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();

            string[] targets = { "East_H1_Full", "West_H7_Full", "Ceiling_H2", "Floor_H1" };
            foreach (var tn in targets) {
                var go = GameObject.Find(tn);
                if (go == null) continue;
                var mesh = go.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null) continue;
                var ws = go.GetComponent<Renderer>().bounds.size;
                var v = mesh.vertices; var uv = mesh.uv; var n = mesh.normals;

                sb.AppendLine($"──── {tn}  {ws.x:F2}×{ws.y:F2}×{ws.z:F2}  材质={go.GetComponent<MeshRenderer>().sharedMaterial?.name} ────");

                var groups = new Dictionary<string, List<int>>();
                for (int i = 0; i < v.Length; i++) {
                    Vector3 nn = n[i];
                    string k = Mathf.Abs(nn.x) > 0.5f ? "X" : (Mathf.Abs(nn.y) > 0.5f ? "Y" : "Z");
                    if (!groups.ContainsKey(k)) groups[k] = new List<int>();
                    groups[k].Add(i);
                }
                foreach (var kv in groups) {
                    float mnU=999,mxU=-999,mnV=999,mxV=-999;
                    float mnA=999,mxA=-999,mnB=999,mxB=-999;
                    foreach (var i in kv.Value) {
                        mnU=Mathf.Min(mnU,uv[i].x); mxU=Mathf.Max(mxU,uv[i].x);
                        mnV=Mathf.Min(mnV,uv[i].y); mxV=Mathf.Max(mxV,uv[i].y);
                        Vector3 nn = n[i];
                        if (Mathf.Abs(nn.x) > 0.5f) { mnA=Mathf.Min(mnA,v[i].z); mxA=Mathf.Max(mxA,v[i].z); mnB=Mathf.Min(mnB,v[i].y); mxB=Mathf.Max(mxB,v[i].y); }
                        else if (Mathf.Abs(nn.y) > 0.5f) { mnA=Mathf.Min(mnA,v[i].x); mxA=Mathf.Max(mxA,v[i].x); mnB=Mathf.Min(mnB,v[i].z); mxB=Mathf.Max(mxB,v[i].z); }
                        else { mnA=Mathf.Min(mnA,v[i].x); mxA=Mathf.Max(mxA,v[i].x); mnB=Mathf.Min(mnB,v[i].y); mxB=Mathf.Max(mxB,v[i].y); }
                    }
                    float wA, wB;
                    Vector3 nn0 = n[kv.Value[0]];
                    if (Mathf.Abs(nn0.x) > 0.5f) { wA=(mxA-mnA)*ws.z; wB=(mxB-mnB)*ws.y; }
                    else if (Mathf.Abs(nn0.y) > 0.5f) { wA=(mxA-mnA)*ws.x; wB=(mxB-mnB)*ws.z; }
                    else { wA=(mxA-mnA)*ws.x; wB=(mxB-mnB)*ws.y; }
                    string verdict = (wA < 0.3f || wB < 0.3f) ? "薄面" :
                        (Mathf.Abs((mxU-mnU)/Mathf.Max(0.01f,wA) - 0.5f) < 0.1f && Mathf.Abs((mxV-mnV)/Mathf.Max(0.01f,wB) - 0.5f) < 0.1f ? "✅ 正确" : "⚠️ 偏离");
                    sb.AppendLine($"  {kv.Key}面 世界{wA:F2}×{wB:F2}  UV {mxU-mnU:F3}×{mxV-mnV:F3}  " +
                                  $"每米 {(mxU-mnU)/Mathf.Max(0.01f,wA):F3}/{(mxV-mnV)/Mathf.Max(0.01f,wB):F3}  {verdict}");
                }
                sb.AppendLine();
            }
            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch5UvVerifyAfter.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}