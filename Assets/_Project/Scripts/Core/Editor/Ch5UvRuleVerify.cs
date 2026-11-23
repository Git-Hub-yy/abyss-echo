using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class Ch5UvRuleVerify {
        public static void Run() {
            var sb = new StringBuilder();
            sb.AppendLine("=== 第五章 UV 映射规则验证 ===");
            sb.AppendLine();

            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter5_Graybox.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();

            // 取几个典型物体，逐顶点打印
            string[] targets = { "East_H1_Full", "Ceiling_H2", "Floor_H1", "West_H7_Full" };

            foreach (var tn in targets) {
                GameObject go = GameObject.Find(tn);
                if (go == null) { sb.AppendLine($"{tn}: 找不到"); continue; }
                var mf = go.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var mesh = mf.sharedMesh;
                var ws = go.GetComponent<Renderer>().bounds.size;

                sb.AppendLine($"──── {tn}  {ws.x:F2}×{ws.y:F2}×{ws.z:F2}  ────");
                sb.AppendLine($"  {"顶点",-4} {"局部位置",-24} {"UV",-20} {"法线",-14} 校验");

                var v = mesh.vertices; var uv = mesh.uv; var n = mesh.normals;

                // 按法线分组，每组取前 2 个顶点
                var seen = new HashSet<string>();
                for (int i = 0; i < v.Length; i++) {
                    Vector3 nn = n[i];
                    string k = Mathf.Abs(nn.x) > 0.5f ? "+X" : (Mathf.Abs(nn.y) > 0.5f ? "+Y" : "+Z");
                    if (seen.Contains(k)) continue;
                    seen.Add(k);

                    // 校验 U/V 对应哪个轴
                    string check = "";
                    float lx = v[i].x, ly = v[i].y, lz = v[i].z;
                    // U 如果是 局部X×wsX×0.5
                    float uGuessX = Mathf.Abs(lx + 0.5f) * ws.x * 0.5f;
                    float uGuessZ = Mathf.Abs(lz + 0.5f) * ws.z * 0.5f;
                    if (Mathf.Abs(uv[i].x - uGuessX) < 0.05f) check += $"U←X({uGuessX:F2}) ";
                    if (Mathf.Abs(uv[i].x - uGuessZ) < 0.05f) check += $"U←Z({uGuessZ:F2}) ";

                    sb.AppendLine($"  {i,-4} ({lx,5:F2},{ly,5:F2},{lz,5:F2})      " +
                                  $"({uv[i].x,6:F3},{uv[i].y,6:F3})  ({nn.x:F0},{nn.y:F0},{nn.z:F0})    {check}");
                }
                sb.AppendLine();
            }

            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch5UvRuleVerify.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}