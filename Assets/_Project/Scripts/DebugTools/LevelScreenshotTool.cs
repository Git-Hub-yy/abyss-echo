using System.Collections;
using System.IO;
using System.Text;
using AbyssEcho.Player;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// 多角度关卡截图（S3B 验证用）。
    ///
    /// 与 ScreenshotTool 的区别（重要）：
    ///   ScreenshotTool 使用 ScreenCapture / 单次 Camera.Render，在无头环境下不稳定。
    ///   本工具采用 BlackScreenDiagnoser 中**已经实测可用**的方式：
    ///     - 传送后等待 0.5 秒（让 CullingResults 与光照数据刷新）
    ///     - 渲染到 RenderTexture（连续两次 Render，确保稳定）
    ///     - ReadPixels 后直接写 PNG
    ///
    /// 输出：%USERPROFILE%/AppData/LocalLow/AbyssEchoStudio/Shots/
    /// </summary>
    public class LevelScreenshotTool : MonoBehaviour
    {
        public bool runOnStart = true;

        private const int W = 960;
        private const int H = 540;

        private struct Shot
        {
            public string Name;
            public Vector3 Position;
            public float Yaw;
            public float Pitch;
        }

        // 每个区域的代表视角（位置取自 ChapterOneLayout 的实际房间中心）
        private static readonly Shot[] Shots =
        {
            new Shot { Name = "01_A0_深潜舱",     Position = new Vector3(0f, 0.05f, -41.5f), Yaw = 0f,   Pitch = 0f },
            new Shot { Name = "02_A1_入口走廊",   Position = new Vector3(0f, 0.05f, -35.0f), Yaw = 0f,   Pitch = 0f },
            new Shot { Name = "03_A2_气密闸室",   Position = new Vector3(0f, 0.05f, -27.0f), Yaw = 0f,   Pitch = 0f },
            new Shot { Name = "04_A3_接待室",     Position = new Vector3(0f, 0.05f, -15.0f), Yaw = 0f,   Pitch = -5f },
            new Shot { Name = "05_B1_餐厅",       Position = new Vector3(0f, 0.05f, -2.0f),  Yaw = 0f,   Pitch = 0f },
            new Shot { Name = "06_B2_宿舍走廊",   Position = new Vector3(-10f, 0.05f, 10.9f), Yaw = 90f, Pitch = 0f },
            new Shot { Name = "07_B4_宿舍",       Position = new Vector3(0f, 0.05f, 13.9f),  Yaw = 0f,   Pitch = 0f },
            new Shot { Name = "08_B3_医疗室",     Position = new Vector3(11.5f, 0.05f, 16.0f), Yaw = 0f, Pitch = 0f },
            new Shot { Name = "09_C1_主厅",       Position = new Vector3(0f, 0.05f, 31.0f), Yaw = 0f,   Pitch = 5f },
            new Shot { Name = "10_C2_监控室",     Position = new Vector3(10f, 0.05f, 35f),  Yaw = 90f,  Pitch = 0f },
        };

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        public IEnumerator Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("ABYSS ECHO · 关卡多角度截图");

            var cam = Camera.main;
            if (cam == null) { Debug.LogError("[LevelShot] 无相机"); yield break; }

            var motor = FindFirstObjectByType<PlayerMotor>();
            var look = FindFirstObjectByType<PlayerLook>();
            var cc = motor != null ? motor.GetComponent<CharacterController>() : null;

            string dir = Path.Combine(Application.persistentDataPath, "Shots");
            Directory.CreateDirectory(dir);
            sb.AppendLine("输出目录: " + dir);

            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            rt.Create();
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);

            foreach (var hud in FindObjectsByType<PlayerDebugHUD>(FindObjectsSortMode.None))
                hud.showHud = false;

            yield return new WaitForSeconds(1.0f);

            foreach (var shot in Shots)
            {
                if (cc != null) cc.enabled = false;
                if (motor != null) motor.transform.position = shot.Position;
                if (cc != null) cc.enabled = true;
                if (look != null) { look.SetYaw(shot.Yaw); look.SetPitch(shot.Pitch); }

                // 关键：等待足够时间让 CullingResults 刷新（实测 0.5s 足够）
                yield return new WaitForSeconds(0.5f);

                var prev = cam.targetTexture;
                cam.targetTexture = rt;
                cam.Render();
                cam.Render();
                cam.targetTexture = prev;

                var prevActive = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply();
                RenderTexture.active = prevActive;

                byte[] png = tex.EncodeToPNG();
                string file = Path.Combine(dir, shot.Name + ".png");
                File.WriteAllBytes(file, png);
                sb.AppendLine($"{shot.Name}: {png.Length / 1024} KB");
                Debug.Log("[LevelShot] " + shot.Name + " -> " + (png.Length / 1024) + " KB");
            }

            rt.Release(); Destroy(rt); Destroy(tex);

            WriteReport(sb.ToString());
            Debug.Log("<b>[LevelShot] 完成</b>\n" + sb);

            yield return new WaitForSeconds(0.3f);
            if (!Application.isEditor) Application.Quit(0);
        }

        private static void WriteReport(string text)
        {
            try
            {
                string file = Path.Combine(Application.persistentDataPath, "Shots", "截图报告.txt");
                File.WriteAllText(file, text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}