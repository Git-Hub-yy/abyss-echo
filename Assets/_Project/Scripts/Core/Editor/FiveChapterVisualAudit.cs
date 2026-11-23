using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S73d：五章视觉审计截图（真实 GPU 渲染，不用 -nographics）。
    ///
    /// 每章拍：
    ///   1. 场景整体远景
    ///   2. 主要走廊
    ///   3. 墙壁/地板/天花板
    ///   4. 门和门框
    ///   5. 大型设备 / 道具
    ///   6. 手电筒近距离照射
    ///   7. 暗部材质细节
    ///   8. 典型特殊区域
    ///
    /// 用临时相机，不改场景里的任何东西。
    /// </summary>
    public static class FiveChapterVisualAudit
    {
        private static readonly (int ch, string path)[] Scenes =
        {
            (1, "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity"),
            (2, "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity"),
            (3, "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity"),
            (4, "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity"),
            (5, "Assets/_Project/Scenes/Level_Chapter5_Graybox.unity"),
        };

        [MenuItem("ABYSS ECHO/审计 v10.0/五章视觉审计截图")]
        public static void Run()
        {
            var log = new StringBuilder();
            log.AppendLine("═══════════════════════════════════════════════");
            log.AppendLine("  五章视觉审计截图");
            log.AppendLine("═══════════════════════════════════════════════");

            string rootDir = @"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\AuditShots";

            foreach (var (ch, path) in Scenes)
            {
                if (!File.Exists(path)) continue;
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Physics.SyncTransforms();

                string dir = Path.Combine(rootDir, $"Ch{ch}");
                Directory.CreateDirectory(dir);

                // 临时相机
                var tempGo = new GameObject("__AUDIT_CAM__");
                var cam = tempGo.AddComponent<Camera>();

                // 复制主相机的参数
                Camera main = null;
                foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                {
                    if (c.CompareTag("MainCamera") || c.name.Contains("Main")) { main = c; break; }
                }
                if (main != null) cam.CopyFrom(main);
                cam.fieldOfView = 60f;

                log.AppendLine();
                log.AppendLine($"────────── 第{ch}章 ──────────");

                // 计算场景范围
                float minZ = 9999, maxZ = -9999, minX = 9999, maxX = -9999;
                var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    var p = r.transform.position;
                    minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z);
                    minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                }

                var shots = new List<(string name, Vector3 pos, Vector3 look, float fov)>();

                // 1. 全景（场景中部）
                shots.Add(("01_全景", new Vector3(0f, 1.62f, Mathf.Lerp(minZ, maxZ, 0.15f)),
                           new Vector3(0f, 1.2f, Mathf.Lerp(minZ, maxZ, 0.85f)), 70f));

                // 2. 走廊（找最窄的延伸方向）
                shots.Add(("02_走廊", new Vector3(0f, 1.62f, Mathf.Lerp(minZ, maxZ, 0.35f)),
                           new Vector3(0f, 1.4f, Mathf.Lerp(minZ, maxZ, 0.65f)), 65f));

                // 3. 墙壁地板（低头看）
                shots.Add(("03_墙壁地板_低头", new Vector3(0f, 1.62f, Mathf.Lerp(minZ, maxZ, 0.5f)),
                           new Vector3(0f, -0.5f, Mathf.Lerp(minZ, maxZ, 0.5f) + 4f), 65f));

                // 4. 天花板（抬头）
                shots.Add(("04_天花板_抬头", new Vector3(0f, 1.62f, Mathf.Lerp(minZ, maxZ, 0.5f)),
                           new Vector3(0f, 4.0f, Mathf.Lerp(minZ, maxZ, 0.5f) + 3f), 70f));

                // 5. 侧墙近距离（手电筒感）
                shots.Add(("05_侧墙近景", new Vector3(0f, 1.62f, Mathf.Lerp(minZ, maxZ, 0.6f)),
                           new Vector3(-6f, 1.4f, Mathf.Lerp(minZ, maxZ, 0.6f)), 60f));

                // 6. 地面近距离
                shots.Add(("06_地面近景", new Vector3(0f, 1.62f, Mathf.Lerp(minZ, maxZ, 0.55f)),
                           new Vector3(0f, 0f, Mathf.Lerp(minZ, maxZ, 0.55f) + 2.5f), 60f));

                // 7. 门（找第一个 DoorInteractable）
                AbyssEcho.Interaction.DoorInteractable firstDoor = null;
                foreach (var d in Object.FindObjectsByType<AbyssEcho.Interaction.DoorInteractable>(FindObjectsSortMode.None))
                { firstDoor = d; break; }
                if (firstDoor != null)
                {
                    var dp = firstDoor.transform.position;
                    shots.Add(("07_门", new Vector3(dp.x, 1.62f, dp.z - 2.5f),
                               new Vector3(dp.x, 1.2f, dp.z), 60f));
                }

                // 8. 道具（找第一个 PickupItem）
                AbyssEcho.Interaction.PickupItem firstPickup = null;
                foreach (var p in Object.FindObjectsByType<AbyssEcho.Interaction.PickupItem>(FindObjectsSortMode.None))
                { firstPickup = p; break; }
                if (firstPickup != null)
                {
                    var pp = firstPickup.transform.position;
                    shots.Add(("08_道具", new Vector3(pp.x, 1.62f, pp.z - 1.5f),
                               new Vector3(pp.x, pp.y, pp.z), 55f));
                }

                // 9. 场景最深处（特殊区域）
                shots.Add(("09_特殊区域", new Vector3(0f, 1.62f, Mathf.Lerp(minZ, maxZ, 0.9f)),
                           new Vector3(0f, 1.2f, maxZ + 3f), 70f));

                // 10. 暗部（朝角落）
                shots.Add(("10_暗部细节", new Vector3(-2f, 1.62f, Mathf.Lerp(minZ, maxZ, 0.45f)),
                           new Vector3(-8f, 0.5f, Mathf.Lerp(minZ, maxZ, 0.45f) + 2f), 60f));

                int ok = 0;
                foreach (var s in shots)
                {
                    cam.transform.position = s.pos;
                    cam.transform.LookAt(s.look);
                    cam.fieldOfView = s.fov;

                    if (RenderOne(cam, Path.Combine(dir, s.name + ".png"), out string err))
                    {
                        ok++;
                        log.AppendLine($"  [OK] {s.name}");
                    }
                    else
                    {
                        log.AppendLine($"  [FAIL] {s.name}: {err}");
                    }
                }

                log.AppendLine($"  第{ch}章: {ok}/{shots.Count} 张");

                Object.DestroyImmediate(tempGo);
            }

            string text = log.ToString();
            Debug.Log(text);
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\AuditShots.txt",
                              text, new UTF8Encoding(false));
        }

        private static int _counter;

        private static bool RenderOne(Camera cam, string path, out string err)
        {
            err = "";
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
            catch (System.Exception e)
            {
                err = e.Message;
                return false;
            }
            finally
            {
                cam.targetTexture = null;
                if (tex != null) Object.DestroyImmediate(tex);
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
            }
        }
    }
}