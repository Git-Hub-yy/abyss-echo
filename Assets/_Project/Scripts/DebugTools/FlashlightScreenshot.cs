using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S52 截图验证：把手电筒开/关的真实画面保存成 PNG，
    /// 用像素统计 + 图片文件判断"到底有没有可见光斑"。
    ///
    /// 这是唯一能证明"玩家真的能看见光"的方式。
    /// </summary>
    public class FlashlightScreenshot : MonoBehaviour
    {
        public bool runOnStart = true;
        public int forceChapter = 3;
        public int shotWidth = 640;
        public int shotHeight = 360;

        private readonly StringBuilder _log = new StringBuilder();
        private static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            if (forceChapter >= 1 && forceChapter <= 5)
                AbyssEcho.LevelGen.ChapterLayout.Current =
                    (AbyssEcho.LevelGen.ChapterLayout.Chapter)forceChapter;

            yield return new WaitForSeconds(1.5f);

            _log.AppendLine("══════ S52 手电筒截图验证 ══════");
            _log.AppendLine($"场景 = {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            _log.AppendLine($"图形设备 = {SystemInfo.graphicsDeviceType}");
            _log.AppendLine();

            var fl = FindFirstObjectByType<AbyssEcho.Tools.FlashlightSystem>();
            var cam = Camera.main;
            if (fl == null || cam == null) { _log.AppendLine("❌ 缺少 FlashlightSystem 或 Camera"); Dump(); yield break; }

            var flType = typeof(AbyssEcho.Tools.FlashlightSystem);
            var setOn = flType.GetMethod("SetLightOn", BF);

            // 让玩家看向一个明确的墙面
            var motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            var look = FindFirstObjectByType<AbyssEcho.Player.PlayerLook>();

            // 关闭 PlayerLook 防止它覆盖朝向
            if (look != null) look.enabled = false;

            // 转向（Ch3 起点在 F1 坠落天井，朝北看）
            if (motor != null)
            {
                motor.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                _log.AppendLine($"玩家位置 = {motor.transform.position}  朝向 = 北");
            }

            yield return new WaitForSeconds(0.4f);

            // ── 场景 A：正常环境，手电筒关 ──
            setOn?.Invoke(fl, new object[] { false });
            yield return new WaitForSeconds(0.3f);
            var statsA = new float[4];
            yield return Capture(cam, "A_正常环境_手电关", statsA);
            float aAvg = statsA[0], aCenter = statsA[1], aMax = statsA[2];

            // ── 场景 B：正常环境，手电筒开 ──
            setOn?.Invoke(fl, new object[] { true });
            yield return new WaitForSeconds(0.3f);
            var statsB = new float[4];
            yield return Capture(cam, "B_正常环境_手电开", statsB);
            float bAvg = statsB[0], bCenter = statsB[1], bMax = statsB[2];

            _log.AppendLine();
            _log.AppendLine($"  正常环境 手电关: 平均={aAvg:F4} 中心={aCenter:F4} 最大={aMax:F4}");
            _log.AppendLine($"  正常环境 手电开: 平均={bAvg:F4} 中心={bCenter:F4} 最大={bMax:F4}");
            _log.AppendLine($"  中心亮度提升 = {(bCenter - aCenter):F4}  ({(aCenter > 0.001f ? ((bCenter / aCenter - 1f) * 100f) : 0f):F1}%)");

            // ── 场景 C：压暗环境后手电关（模拟恐怖事件）──
            var oldAmbient = RenderSettings.ambientLight;
            var oldAmbientIntensity = RenderSettings.ambientIntensity;
            var oldFog = RenderSettings.fog;

            RenderSettings.ambientLight = new Color(0.02f, 0.02f, 0.03f);
            RenderSettings.ambientIntensity = 0.1f;
            RenderSettings.fog = false;

            // 同时关掉所有场景灯（模拟恐怖事件灭灯）
            int killed = 0;
            var allLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            var savedStates = new System.Collections.Generic.List<(Light, bool)>();
            foreach (var l in allLights)
            {
                if (l.type == LightType.Directional) continue;
                savedStates.Add((l, l.enabled));
                if (l.enabled) { l.enabled = false; killed++; }
            }

            setOn?.Invoke(fl, new object[] { false });
            yield return new WaitForSeconds(0.5f);
            var flLight = flType.GetField("spotLight", BF)?.GetValue(fl) as Light;
            if (flLight != null) flLight.enabled = false;

            yield return new WaitForSeconds(0.2f);
            var statsC = new float[4];
            yield return Capture(cam, "C_黑暗环境_手电关", statsC);
            float cAvg = statsC[0], cCenter = statsC[1];

            // ── 场景 D：压暗环境后手电开 ──
            setOn?.Invoke(fl, new object[] { true });
            yield return new WaitForSeconds(0.4f);
            var statsD = new float[4];
            yield return Capture(cam, "D_黑暗环境_手电开", statsD);
            float dAvg = statsD[0], dCenter = statsD[1], dMax = statsD[2];

            _log.AppendLine();
            _log.AppendLine($"  黑暗环境 手电关: 平均={cAvg:F4} 中心={cCenter:F4}");
            _log.AppendLine($"  黑暗环境 手电开: 平均={dAvg:F4} 中心={dCenter:F4} 最大={dMax:F4}");
            _log.AppendLine($"  ★ 黑暗中心亮度提升 = {(dCenter - cCenter):F4}  " +
                            $"({(cCenter > 0.001f ? ((dCenter / cCenter - 1f) * 100f) : 9999f):F1}%)");

            _log.AppendLine();
            _log.AppendLine("──── 判定 ────");
            bool darkVisible = (dCenter - cCenter) > 0.02f;
            _log.AppendLine(darkVisible
                ? $"  ✅ 黑暗环境下手电筒**确实照亮了画面**（中心亮度 {cCenter:F4} → {dCenter:F4}）"
                : $"  ❌ 黑暗环境下手电筒**没有产生可见照明**（中心亮度 {cCenter:F4} → {dCenter:F4}）");

            // 恢复
            RenderSettings.ambientLight = oldAmbient;
            RenderSettings.ambientIntensity = oldAmbientIntensity;
            RenderSettings.fog = oldFog;
            foreach (var (l, s) in savedStates) { if (l != null) l.enabled = s; }

            _log.AppendLine();
            _log.AppendLine($"  （已关闭 {killed} 盏场景灯做黑暗测试，测试后已恢复）");
            _log.AppendLine($"  截图保存在 Reports/FlashlightShots/ 目录");

            Dump();
        }

        private IEnumerator Capture(Camera cam, string name, float[] outStats)
        {
            yield return new WaitForEndOfFrame();

            int w = shotWidth, h = shotHeight;
            RenderTexture rt = null;
            Texture2D tex = null;

            try
            {
                rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
                var prevTarget = cam.targetTexture;
                var prevActive = RenderTexture.active;

                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = prevTarget;

                RenderTexture.active = rt;
                tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = prevActive;

                // 保存 PNG
                byte[] png = tex.EncodeToPNG();
                string dir = FindReportsDir();
                if (dir != null)
                {
                    string shots = Path.Combine(dir, "FlashlightShots");
                    Directory.CreateDirectory(shots);
                    string fn = Path.Combine(shots, $"Ch{forceChapter}_{name}.png");
                    File.WriteAllBytes(fn, png);
                }

                // 统计
                float sum = 0f, max = 0f;
                Color[] px = tex.GetPixels();
                foreach (var p in px)
                {
                    float lum = 0.299f * p.r + 0.587f * p.g + 0.114f * p.b;
                    sum += lum; if (lum > max) max = lum;
                }

                // 中心区域（手电筒照的地方）
                float cSum = 0f; int cN = 0;
                int x0 = w * 2 / 5, x1 = w * 3 / 5;
                int y0 = h * 2 / 5, y1 = h * 3 / 5;
                for (int y = y0; y < y1; y++)
                    for (int x = x0; x < x1; x++)
                    {
                        var p = tex.GetPixel(x, y);
                        cSum += 0.299f * p.r + 0.587f * p.g + 0.114f * p.b;
                        cN++;
                    }

                outStats[0] = sum / px.Length;
                outStats[1] = cSum / Mathf.Max(1, cN);
                outStats[2] = max;

                _log.AppendLine($"  [截图] {name}  平均={outStats[0]:F4} 中心={outStats[1]:F4} 最大={outStats[2]:F4}");
            }
            catch (System.Exception e)
            {
                _log.AppendLine($"  [截图] {name} 失败: {e.GetType().Name}: {e.Message}");
            }
            finally
            {
                if (tex != null) Destroy(tex);
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
            }
        }

        private string FindReportsDir()
        {
            var di = new DirectoryInfo(Application.dataPath);
            for (int hop = 0; hop < 6 && di != null; hop++)
            {
                string reports = Path.Combine(di.FullName, "Reports");
                if (Directory.Exists(reports)) return reports;
                di = di.Parent;
            }
            return null;
        }

        private void Dump()
        {
            string text = _log.ToString();
            Debug.Log("<b>[FlashlightScreenshot]</b>\n" + text);

            string dir = FindReportsDir();
            if (dir != null)
            {
                try { File.WriteAllText(Path.Combine(dir, $"FlashlightScreenshot_Ch{forceChapter}.txt"), text, new UTF8Encoding(false)); }
                catch { }
            }

            if (!Application.isEditor) Application.Quit(0);
        }
    }
}