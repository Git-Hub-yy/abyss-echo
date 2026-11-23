using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S74c：**天花板 Metallic 修改的前后对比截图**。
    ///
    /// 通用审计相机拍到的天花板是全黑的（没有直接光照），
    /// 无法展示 Metallic 变化。
    ///
    /// 本工具：
    ///   · 找场景里每个 Ceiling_* 对象
    ///   · 在它下方 1.6 米处放相机，抬头 45° 看它
    ///   · 找最近的光源，确认有光照
    ///
    /// 对比方式：
    ///   运行两次，第一次是当前（已修复），
    ///   手动回滚材质后再运行一次（before）。
    ///   本工具通过 -before 参数区分输出目录。
    /// </summary>
    public static class CeilingMetallicShot
    {
        [MenuItem("ABYSS ECHO/修复 v10.1/天花板对比截图（修改后）")]
        public static void ShotAfter() => Run("AFTER");

        [MenuItem("ABYSS ECHO/修复 v10.1/天花板对比截图（修改前）")]
        public static void ShotBefore() => Run("BEFORE");

        private static void Run(string tag)
        {
            var log = new StringBuilder();
            log.AppendLine($"═══════════════════════════════════════════");
            log.AppendLine($"  天花板 Metallic 对比截图 —— {tag}");
            log.AppendLine($"═══════════════════════════════════════════");

            string dir = $@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\CeilingShots\{tag}";
            Directory.CreateDirectory(dir);

            // 检查当前材质值
            var mat = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Art/Materials/Architecture/M_Arch_Ceiling.mat");
            log.AppendLine($"  M_Arch_Ceiling Metallic = {mat?.GetFloat("_Metallic"):F2}");
            log.AppendLine();

            var scenes = new (int ch, string path)[]
            {
                (2, "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity"),
                (3, "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity"),
                (4, "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity"),
            };

            foreach (var (ch, path) in scenes)
            {
                if (!File.Exists(path)) continue;
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Physics.SyncTransforms();

                var tempGo = new GameObject("__CEIL_CAM__");
                var cam = tempGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.fieldOfView = 65f;
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 100f;

                // 找光照最充足的 Ceiling_*
                GameObject best = null;
                float bestScore = -1;

                foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                {
                    if (r == null) continue;
                    if (!r.gameObject.name.StartsWith("Ceiling")) continue;
                    if (r.sharedMaterial == null || r.sharedMaterial.name != "M_Arch_Ceiling") continue;

                    // 找 6 米内的点光源，算亮度分
                    float score = 0f;
                    foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    {
                        if (l == null || !l.enabled) continue;
                        float d = Vector3.Distance(l.transform.position, r.bounds.center);
                        if (d < 12f) score += l.intensity / (1f + d);
                    }
                    if (score > bestScore) { bestScore = score; best = r.gameObject; }
                }

                if (best == null)
                {
                    log.AppendLine($"  第{ch}章: ❌ 找不到有光的 Ceiling");
                    Object.DestroyImmediate(tempGo);
                    continue;
                }

                var bnd = best.GetComponent<Renderer>().bounds;
                // 相机在天花板下方 1.6 米，抬头看向天花板
                var pos = new Vector3(bnd.center.x, bnd.min.y - 1.6f, bnd.center.z);
                var look = new Vector3(bnd.center.x, bnd.min.y, bnd.center.z);

                cam.transform.position = pos;
                cam.transform.LookAt(look);

                string file = Path.Combine(dir, $"Ch{ch}_{best.name}.png");
                if (RenderOne(cam, file))
                    log.AppendLine($"  第{ch}章: ✅ {best.name}  光分={bestScore:F2}");
                else
                    log.AppendLine($"  第{ch}章: ❌ 渲染失败");

                Object.DestroyImmediate(tempGo);
            }

            string text = log.ToString();
            Debug.Log(text);
            File.WriteAllText($@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\CeilingShots_{tag}.txt",
                              text, new UTF8Encoding(false));
        }

        private static bool RenderOne(Camera cam, string path)
        {
            int w = 1280, h = 720;
            RenderTexture rt = null;
            Texture2D tex = null;
            try
            {
                rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = null;

                File.WriteAllBytes(path, tex.EncodeToPNG());
                return true;
            }
            catch { return false; }
            finally
            {
                cam.targetTexture = null;
                if (tex != null) Object.DestroyImmediate(tex);
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
            }
        }
    }
}