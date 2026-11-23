using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class Ch5SpawnShot {
        [MenuItem("ABYSS ECHO/修复 v10.3/出生点截图 AFTER")]
        public static void After() => Run("AFTER");
        [MenuItem("ABYSS ECHO/修复 v10.3/出生点截图 BEFORE")]
        public static void Before() => Run("BEFORE");
        public static void Run(string tag) {
            var log = new StringBuilder();
            string dir = $"D:/chatgpt/Deepseek/ABYSS_ECHO/Reports/Ch5SpawnShots/{tag}";
            Directory.CreateDirectory(dir);

            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter5_Graybox.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();

            var tempGo = new GameObject("__SP_CAM__");
            var cam = tempGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.fieldOfView = 65f;
            cam.nearClipPlane = 0.05f;

            // 模拟玩家截图的位置：出生点 (0,1.62,-2) 朝北略抬头
            var shots = new (string n, Vector3 p, Vector3 l)[] {
                // 玩家截图的角度：从门洞看进去，能看到紫天花板、粉墙、绿墙
                ("01_出生点_玩家视角", new Vector3(0f, 1.62f, -2.0f), new Vector3(2.0f, 2.5f, 8.0f)),
                ("02_出生点_朝北", new Vector3(0f, 1.62f, -2.0f), new Vector3(0f, 1.6f, 12.0f)),
                ("03_紫膜天花板", new Vector3(0f, 1.62f, 3.0f), new Vector3(0f, 4.0f, 10.0f)),
                ("04_血肉墙近景", new Vector3(-3f, 1.62f, 5.0f), new Vector3(-8f, 1.5f, 9.0f)),
        ("05_绿色墙近景", new Vector3(3f, 1.62f, 7.0f), new Vector3(8f, 1.5f, 7.0f)),
                ("06_整体", new Vector3(0f, 1.62f, -4.0f), new Vector3(0f, 2.0f, 15.0f)),
            };

            foreach (var s in shots) {
                cam.transform.position = s.p;
                cam.transform.LookAt(s.l);
                Render(cam, Path.Combine(dir, s.n + ".png"));
                log.AppendLine($"  [OK] {s.n}");
            }
            Object.DestroyImmediate(tempGo);
            Debug.Log(log.ToString());
            File.WriteAllText($"D:/chatgpt/Deepseek/ABYSS_ECHO/Reports/Ch5SpawnShots_{tag}.txt", log.ToString(), new UTF8Encoding(false));
        }
        private static void Render(Camera cam, string path) {
            int w=1280,h=720; var rt=RenderTexture.GetTemporary(w,h,24,RenderTextureFormat.ARGB32);
            cam.targetTexture=rt; cam.Render(); RenderTexture.active=rt;
            var tex=new Texture2D(w,h,TextureFormat.RGB24,false);
            tex.ReadPixels(new Rect(0,0,w,h),0,0); tex.Apply(); RenderTexture.active=null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            cam.targetTexture=null; Object.DestroyImmediate(tex); RenderTexture.ReleaseTemporary(rt);
        }
    }
}