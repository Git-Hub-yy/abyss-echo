using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class OrganicChapterCompare {
        public static void Run() {
            var log = new StringBuilder();
            string dir = "D:/chatgpt/Deepseek/ABYSS_ECHO/Reports/OrganicCompare";
            Directory.CreateDirectory(dir);
            log.AppendLine("=== 三/四/五章有机材质对比截图 ===");

            var scenes = new (int ch, string p)[] {
                (3,"Assets/_Project/Scenes/Level_Chapter3_Graybox.unity"),
                (4,"Assets/_Project/Scenes/Level_Chapter4_Graybox.unity"),
                (5,"Assets/_Project/Scenes/Level_Chapter5_Graybox.unity"),
            };

            foreach (var (ch,p) in scenes) {
                EditorSceneManager.OpenScene(p, OpenSceneMode.Single);
                Physics.SyncTransforms();

                var tempGo = new GameObject("__ORG_CAM__");
                var cam = tempGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.fieldOfView = 65f;
                cam.nearClipPlane = 0.05f;

                // 找最大的有机墙
                MeshRenderer best = null;
                float bestScore = 0;
                foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)) {
                    if (r == null || r.sharedMaterial == null) continue;
                    if (!r.sharedMaterial.name.StartsWith("M_Org_")) continue;
                    float score = r.bounds.size.x * r.bounds.size.y;
                    // 优先找大面墙
                    if (score > bestScore) { bestScore = score; best = r; }
                }

                if (best == null) { log.AppendLine($"  第{ch}章: 无有机墙"); Object.DestroyImmediate(tempGo); continue; }

                var b = best.bounds;
                var ws = b.size;
                int thin = ws.x < ws.y ? (ws.x < ws.z ? 0 : 2) : (ws.y < ws.z ? 1 : 2);
                Vector3 pos;
                if (thin == 0) pos = new Vector3(b.center.x + (b.center.x < 0 ? 1 : -1) * (ws.x*0.5f + 4f), b.center.y, b.center.z);
                else if (thin == 2) pos = new Vector3(b.center.x, b.center.y, b.center.z + (b.center.z < 0 ? 1 : -1) * (ws.z*0.5f + 4f));
                else pos = new Vector3(b.center.x, b.center.y + ws.y*0.5f + 4f, b.center.z);
                pos.y = Mathf.Max(1.62f, pos.y);

                cam.transform.position = pos;
                cam.transform.LookAt(b.center);

                string f = Path.Combine(dir, $"Ch{ch}_{best.gameObject.name}_{best.sharedMaterial.name}.png");
                Render(cam, f);
                var bc = best.sharedMaterial.GetColor("_BaseColor");
                log.AppendLine($"  第{ch}章: {best.gameObject.name}  材质={best.sharedMaterial.name}  " +
                               $"_BaseColor=({bc.r:F2},{bc.g:F2},{bc.b:F2})  亮度={bc.maxColorComponent:F2}");
                Object.DestroyImmediate(tempGo);
            }
            Debug.Log(log.ToString());
            File.WriteAllText("D:/chatgpt/Deepseek/ABYSS_ECHO/Reports/OrganicCompare.txt", log.ToString(), new UTF8Encoding(false));
        }
        private static void Render(Camera cam, string path) {
            int w=960,h=540; RenderTexture rt=RenderTexture.GetTemporary(w,h,24,RenderTextureFormat.ARGB32);
            cam.targetTexture=rt; cam.Render(); RenderTexture.active=rt;
            var tex=new Texture2D(w,h,TextureFormat.RGB24,false);
            tex.ReadPixels(new Rect(0,0,w,h),0,0); tex.Apply(); RenderTexture.active=null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            cam.targetTexture=null; Object.DestroyImmediate(tex); RenderTexture.ReleaseTemporary(rt);
        }
    }
}