using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S72d：直接在 Editor 里渲染 G5 北侧走廊的截图，验证墙体与地面。
    ///
    /// 之前的 exe 截图脚本因为构建缓存问题没跑新代码，
    /// 这里改用 Editor 端直接渲染 —— 更可靠。
    ///
    /// 注意：必须**不带 -nographics** 运行，否则 GPU 不可用。
    /// </summary>
    public static class Ch4CorridorShotBuilder
    {
        [MenuItem("ABYSS ECHO/修复 v9.6/渲染走廊截图")]
        public static void Build() => Run();

        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════");
            sb.AppendLine("  G5 北侧走廊渲染验证");
            sb.AppendLine("═══════════════════════════════════════════");

            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter4_Graybox.unity",
                                         OpenSceneMode.Single);
            Physics.SyncTransforms();

            // 找主相机
            Camera cam = Camera.main;
            if (cam == null)
            {
                foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                {
                    if (c.name.Contains("Main") || c.CompareTag("MainCamera")) { cam = c; break; }
                }
            }
            if (cam == null)
            {
                var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
                if (cams.Length > 0) cam = cams[0];
            }
            if (cam == null) { sb.AppendLine("❌ 找不到相机"); Finish(sb); return; }

            sb.AppendLine($"相机 = {cam.name} @ {cam.transform.position}");

            // 临时相机（避免改场景里的相机）
            var tempGo = new GameObject("__TEMP_SHOT_CAM__");
            var shotCam = tempGo.AddComponent<Camera>();
            shotCam.CopyFrom(cam);
            shotCam.transform.SetParent(null);

            string dir = @"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch4CorridorShots";
            Directory.CreateDirectory(dir);

            // 机位列表
            var shots = new (string name, Vector3 pos, Vector3 look)[]
            {
                // 站在 G5 内，朝北门看
                ("01_G5内_朝北门",  new Vector3(-19f, 1.62f, 1.0f),  new Vector3(-19f, 1.0f, 8.0f)),
                // 站在北门门框处，朝北（走廊内）
                ("02_北门_朝走廊",   new Vector3(-19f, 1.62f, 5.8f),  new Vector3(-14f, 0.8f, 9.0f)),
                // 站在过渡区中间，朝东（看东墙与补的地板）
                ("03_过渡区_朝东",   new Vector3(-17f, 1.62f, 7.5f),  new Vector3(-9f, 0.8f, 8.0f)),
                // 站在过渡区，朝北（看北墙）
                ("04_过渡区_朝北",   new Vector3(-16f, 1.62f, 6.5f),  new Vector3(-15f, 0.8f, 12f)),
                // 俯瞰（看地面是否完整）
                ("05_俯瞰地面",      new Vector3(-16f, 4.0f, 4.0f),   new Vector3(-15f, 0f, 8.0f)),
                // 朝西看（看西墙）
                ("06_过渡区_朝西",   new Vector3(-14f, 1.62f, 7.5f),  new Vector3(-22f, 0.8f, 7.5f)),
            };

            int ok = 0;
            foreach (var s in shots)
            {
                shotCam.transform.position = s.pos;
                shotCam.transform.LookAt(s.look);

                int w = 960, h = 540;
                RenderTexture rt = null;
                Texture2D tex = null;
                try
                {
                    rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
                    shotCam.targetTexture = rt;
                    shotCam.Render();

                    RenderTexture.active = rt;
                    tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;

                    string path = Path.Combine(dir, s.name + ".png");
                    File.WriteAllBytes(path, tex.EncodeToPNG());

                    // 亮度统计
                    var px = tex.GetPixels();
                    float sum = 0f;
                    foreach (var p in px) sum += 0.299f * p.r + 0.587f * p.g + 0.114f * p.b;

                    sb.AppendLine($"  [OK] {s.name}  亮度={sum / px.Length:F4}");
                    ok++;
                }
                catch (System.Exception e)
                {
                    sb.AppendLine($"  [FAIL] {s.name}  {e.GetType().Name}: {e.Message}");
                }
                finally
                {
                    shotCam.targetTexture = null;
                    if (tex != null) Object.DestroyImmediate(tex);
                    if (rt != null) RenderTexture.ReleaseTemporary(rt);
                }
            }

            Object.DestroyImmediate(tempGo);
            sb.AppendLine();
            sb.AppendLine($"成功 {ok}/{shots.Length} 张");
            Finish(sb);
        }

        private static void Finish(StringBuilder sb)
        {
            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch4CorridorShots.txt",
                              text, new UTF8Encoding(false));
        }
    }
}