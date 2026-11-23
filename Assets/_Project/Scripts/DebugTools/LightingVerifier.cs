using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S3B 灯光验证工具：用**像素级对比**客观判断实时灯光是否生效。
    ///
    /// 为什么需要：
    ///   "看起来暗"是主观判断。本工具在完全相同的位置、相机、后处理下，
    ///   分别截取"灯全开"与"灯全关"两张图，比较平均亮度差。
    ///   - 亮度差明显（>5%）→ 实时灯光确实在渲染
    ///   - 亮度差接近 0 → 灯光没有参与光照计算
    ///
    /// 这样无论截图通道是否可靠，都能得到可判定的结论。
    /// </summary>
    public class LightingVerifier : MonoBehaviour
    {
        [Tooltip("是否在启动时自动运行")]
        public bool runOnStart = true;

        public int width = 640;
        public int height = 360;

        private void Start()
        {
            if (runOnStart) StartCoroutine(RunAll());
        }

        public IEnumerator RunAll()
        {
            var builder = new StringBuilder();
            builder.AppendLine("ABYSS ECHO · S3B 灯光验证（像素对比法）");
            builder.AppendLine("=====================================");

            var cam = Camera.main;
            if (cam == null) { Debug.LogError("[LightingVerifier] 找不到相机"); yield break; }

            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            int activeLights = 0;
            foreach (var l in lights) if (l.enabled && l.gameObject.activeInHierarchy && l.intensity > 0.01f) activeLights++;
            builder.AppendLine($"场景光源：共 {lights.Length}，有效 {activeLights}");
            builder.AppendLine($"环境光：{RenderSettings.ambientLight} / mode={RenderSettings.ambientMode}");

            // ── 关键实验：在相机正前方 3m 放一块白板，打一盏近距离点光 ──
            //   如果这块板在"灯开/灯关"下亮度没变化，说明这台机器上 URP 实时灯光完全不工作，
            //   与本项目的关卡、材质、灯摆放都无关。
            yield return ControlledLightTest(builder, cam);

            var testPositions = new[]
            {
                ("A1 入口走廊", new Vector3(0f, 0.05f, -36.2f), 0f),
                ("B2 宿舍走廊", new Vector3(-10f, 0.05f, 10.9f), 90f),
                ("C1 主厅",     new Vector3(0f, 0.05f, 30.8f), 0f),
            };

            var motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            var look = FindFirstObjectByType<AbyssEcho.Player.PlayerLook>();
            var cc = motor != null ? motor.GetComponent<CharacterController>() : null;

            // 关闭手电筒，只测场景灯
            var flash = FindFirstObjectByType<AbyssEcho.Tools.FlashlightSystem>();
            if (flash != null && flash.IsOn) flash.Toggle();

            yield return new WaitForSeconds(0.5f);

            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);

            foreach (var (name, pos, yaw) in testPositions)
            {
                if (cc != null) cc.enabled = false;
                if (motor != null) motor.transform.position = pos;
                if (cc != null) cc.enabled = true;
                if (look != null) { look.SetYaw(yaw); look.SetPitch(0f); }
                yield return null;
                yield return null;

                // ① 灯全开
                SetLightsEnabled(true);
                yield return null;
                float brightOn = CaptureAndMeasure(cam, rt, tex, Path.Combine(OutDir(), $"{Sanitize(name)}_灯开.png"));

                // ② 灯全关（注意：延迟应用，确保 Camera.Render 读到的是禁用状态）
                SetLightsEnabled(false);
                yield return null;
                yield return null;   // 多等一帧，确保 CullingResults 刷新
                float brightOff = CaptureAndMeasure(cam, rt, tex, Path.Combine(OutDir(), $"{Sanitize(name)}_灯关.png"));

                SetLightsEnabled(true);
                yield return null;

                float delta = brightOn - brightOff;
                float pct = brightOff > 0.0001f ? delta / brightOff * 100f : (delta > 0 ? 999f : 0f);

                string verdict = delta > 0.008f ? "✅ 灯光生效" : "❌ 灯光几乎无贡献";
                builder.AppendLine($"\n[{name}]");
                builder.AppendLine($"  灯全开亮度：{brightOn:F4}");
                builder.AppendLine($"  灯全关亮度：{brightOff:F4}");
                builder.AppendLine($"  差值：{delta:F4}（{pct:F1}%）  {verdict}");
            }

            rt.Release();
            Destroy(rt);
            Destroy(tex);

            builder.AppendLine("\n=====================================");
            builder.AppendLine("判定标准：差值 > 0.008 视为灯光生效");

            string text = builder.ToString();
            Debug.Log("<b>[LightingVerifier]</b>\n" + text);
            WriteReport(text);

            if (!Application.isEditor) Application.Quit(0);
        }

        /// <summary>
        /// 受控实验：在相机正前方放一块白板 + 一盏近距离点光。
        /// 这是判断"这台机器上的 URP 实时灯光是否工作"的最小可复现用例，
        /// 不受关卡布局、材质、灯摆放影响。
        /// </summary>
        private IEnumerator ControlledLightTest(StringBuilder sb, Camera cam)
        {
            sb.AppendLine("\n===== 受控实验：白板 + 近距离点光 =====");

            // 临时环境光归零，排除干扰
            var savedAmbient = RenderSettings.ambientLight;
            var savedMode = RenderSettings.ambientMode;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;

            // 临时白板
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "LightTest_Board";
            board.transform.position = cam.transform.position + cam.transform.forward * 3f;
            board.transform.localScale = new Vector3(2f, 2f, 0.1f);
            board.transform.rotation = Quaternion.LookRotation(-cam.transform.forward);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = Color.white;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
            board.GetComponent<Renderer>().sharedMaterial = mat;

            // 临时点光
            var lightGo = new GameObject("LightTest_PointLight");
            lightGo.transform.position = cam.transform.position + cam.transform.forward * 2f + Vector3.up * 0.5f;
            var testLight = lightGo.AddComponent<Light>();
            testLight.type = LightType.Point;
            testLight.color = Color.white;
            testLight.intensity = 8f;
            testLight.range = 10f;
            testLight.enabled = false;

            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);

            yield return null;
            yield return null;

            testLight.enabled = false;
            yield return null; yield return null;
            float off = CaptureAndMeasure(cam, rt, tex, Path.Combine(OutDir(), "受控_灯关.png"));

            testLight.enabled = true;
            yield return null; yield return null;
            float on = CaptureAndMeasure(cam, rt, tex, Path.Combine(OutDir(), "受控_灯开.png"));

            float delta = on - off;
            sb.AppendLine($"  白板·灯关亮度：{off:F4}");
            sb.AppendLine($"  白板·灯开亮度：{on:F4}");
            sb.AppendLine($"  差值：{delta:F4}  {(delta > 0.02f ? "✅ URP 实时灯光正常工作" : "❌ URP 实时灯光在本机不可用")}");

            // 清理
            Object.Destroy(board);
            Object.Destroy(lightGo);
            Object.Destroy(mat);
            rt.Release(); Object.Destroy(rt); Object.Destroy(tex);

            RenderSettings.ambientLight = savedAmbient;
            RenderSettings.ambientMode = savedMode;

            yield return null;
        }

        private void SetLightsEnabled(bool on)
        {
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                // 跳过手电筒（它由 FlashlightSystem 管理）
                if (l.GetComponentInParent<AbyssEcho.Tools.FlashlightSystem>() != null) continue;
                if (l.type == LightType.Directional) continue;   // 方向光保持
                l.enabled = on;
            }
        }

        private float CaptureAndMeasure(Camera cam, RenderTexture rt, Texture2D tex, string path)
        {
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prev;

            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;

            // 平均亮度（相对亮度 Rec.709）
            var px = tex.GetPixels();
            double sum = 0;
            for (int i = 0; i < px.Length; i++)
                sum += 0.2126 * px[i].r + 0.7152 * px[i].g + 0.0722 * px[i].b;

            try { File.WriteAllBytes(path, tex.EncodeToPNG()); } catch { }

            return (float)(sum / px.Length);
        }

        private static string Sanitize(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(' ', '_');
        }

        private static string OutDir()
        {
            string dir = Application.isEditor
                ? Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports/S3B_LightTest"))
                : Path.Combine(Path.GetDirectoryName(Application.dataPath), "LightTest");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void WriteReport(string text)
        {
            try
            {
                File.WriteAllText(Path.Combine(OutDir(), "灯光验证报告.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}