using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class Ch5OrganicShot {
        [MenuItem("ABYSS ECHO/修复 v10.2/第五章有机截图 AFTER")]
        public static void ShotAfter() => Run("AFTER");

        [MenuItem("ABYSS ECHO/修复 v10.2/第五章有机截图 BEFORE")]
        public static void ShotBefore() => Run("BEFORE");

        public static void Run(string tag) {
            var log = new StringBuilder();
            log.AppendLine($"=== 第五章有机材质截图 —— {tag} ===");
            string dir = $"D:/chatgpt/Deepseek/ABYSS_ECHO/Reports/Ch5TextureShots_{tag}";
            Directory.CreateDirectory(dir);

            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter5_Graybox.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();

            var tempGo = new GameObject("__CH5_CAM__");
            var cam = tempGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.05f;

            // 找有机墙
            var organic = new List<MeshRenderer>();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)) {
                if (r == null || r.sharedMaterial == null) continue;
                string mn = r.sharedMaterial.name;
                if (mn == "M_Org_Membrane" || mn == "M_Org_FleshWet" || mn == "M_Org_SlimeBio") {
                    // 只要大面墙（至少一个轴 >= 3 米）
                    var ws = r.bounds.size;
                    float maxDim = Mathf.Max(ws.x, Mathf.Max(ws.y, ws.z));
                    if (maxDim >= 3f) organic.Add(r);
                }
            }
            organic.Sort((a,b) => b.bounds.size.magnitude.CompareTo(a.bounds.size.magnitude));
            log.AppendLine($"  找到有机墙 {organic.Count} 个");

            var shots = new List<(string name, Vector3 pos, Vector3 look)>();

            // 按材质各取最大的 2 个
            var byMat = new Dictionary<string, List<MeshRenderer>>();
            foreach (var r in organic) {
                string mn = r.sharedMaterial.name;
                if (!byMat.ContainsKey(mn)) byMat[mn] = new List<MeshRenderer>();
                if (byMat[mn].Count < 2) byMat[mn].Add(r);
            }

            int idx = 1;
            foreach (var kv in byMat) {
                foreach (var r in kv.Value) {
                    var b = r.bounds;
                    var c = b.center;
                    var ws = b.size;
                    // 判断墙的朝向：哪个轴最薄
                    int thin = ws.x < ws.y ? (ws.x < ws.z ? 0 : 2) : (ws.y < ws.z ? 1 : 2);
                    // 薄轴是墙的法线方向，相机沿这个轴退开
                    Vector3 offset;
                    if (thin == 0) offset = new Vector3(Mathf.Max(3.5f, ws.x*0.5f+2.5f), 0, 0);
                    else if (thin == 2) offset = new Vector3(0, 0, Mathf.Max(3.5f, ws.z*0.5f+2.5f));
                    else offset = new Vector3(0, Mathf.Max(3.5f, ws.y*0.5f+2.5f), 0);
                    // 站在墙前
                    var pos = c + offset * (c.x < 0 ? 1 : -1) * (thin==0?1:0) + offset * (c.z < 0 ? 1 : -1) * (thin==2?1:0);
                    if (thin == 0) pos = new Vector3(c.x + (c.x < 0 ? ws.x*0.5f + 3.5f : -(ws.x*0.5f+3.5f)), c.y, c.z);
                    else if (thin == 2) pos = new Vector3(c.x, c.y, c.z + (c.z < 0 ? ws.z*0.5f + 3.5f : -(ws.z*0.5f+3.5f)));
                    else pos = new Vector3(c.x, c.y + ws.y*0.5f + 3.5f, c.z);
                    pos.y = Mathf.Max(1.6f, pos.y);
                    shots.Add(($"{idx:D2}_{kv.Key}_{r.gameObject.name}", pos, c));
                    idx++;
                }
            }

            // 整体场景
            shots.Add(($"{idx:D2}_整体场景", new Vector3(0, 1.62f, -5), new Vector3(0, 1.5f, 20)));

            int ok = 0;
            foreach (var s in shots) {
                cam.transform.position = s.pos;
                cam.transform.LookAt(s.look);
                if (RenderOne(cam, Path.Combine(dir, s.name + ".png"))) {
                    log.AppendLine($"  [OK] {s.name}  相机{s.pos}");
                    ok++;
                } else log.AppendLine($"  [FAIL] {s.name}");
            }
            Object.DestroyImmediate(tempGo);
            log.AppendLine($"  {ok}/{shots.Count} 张");
            Debug.Log(log.ToString());
            File.WriteAllText($"D:/chatgpt/Deepseek/ABYSS_ECHO/Reports/Ch5TextureShots_{tag}.txt", log.ToString(), new UTF8Encoding(false));
        }
        private static bool RenderOne(Camera cam, string path) {
            int w=1280,h=720; RenderTexture rt=null; Texture2D tex=null;
            try {
                rt = RenderTexture.GetTemporary(w,h,24,RenderTextureFormat.ARGB32);
                cam.targetTexture = rt; cam.Render();
                RenderTexture.active = rt;
                tex = new Texture2D(w,h,TextureFormat.RGB24,false);
                tex.ReadPixels(new Rect(0,0,w,h),0,0); tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(path, tex.EncodeToPNG());
                return true;
            } catch { return false; }
            finally {
                cam.targetTexture = null;
                if (tex != null) Object.DestroyImmediate(tex);
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
            }
        }
    }
}