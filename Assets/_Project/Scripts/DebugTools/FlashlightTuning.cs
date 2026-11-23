using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S52 手电筒参数对比测试：用真实截图找出"能看见光斑"的参数组合。
    ///
    /// ══════════════════ 为什么需要对比测试 ══════════════════
    ///
    ///   任务明确要求"不要盲目调高所有灯光"，
    ///   所以本测试固定环境（黑暗），只改手电筒参数，
    ///   用中心区域亮度量化"光斑有多明显"，找出合理的组合。
    ///
    ///   测试维度：
    ///     · intensity: 3.2 / 8 / 15 / 25
    ///     · range:     22 / 12 / 8
    ///     · spotAngle: 55 / 45 / 35
    ///
    /// ══════════════════ 判定标准 ══════════════════
    ///
    ///   中心区域亮度提升 > 0.10（约 25/255）→ 人眼能明确看出光斑
    ///   提升 0.04~0.10 → 勉强可见
    ///   提升 < 0.04 → 几乎看不见
    /// </summary>
    public class FlashlightTuning : MonoBehaviour
    {
        public bool runOnStart = true;
        public int forceChapter = 3;
        public int shotWidth = 480;
        public int shotHeight = 270;

        private readonly StringBuilder _log = new StringBuilder();
        private static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private AbyssEcho.Tools.FlashlightSystem _fl;
        private Light _spot;
        private Camera _cam;

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            AbyssEcho.LevelGen.ChapterLayout.Current = AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter3;
            yield return new WaitForSeconds(1.5f);

            _log.AppendLine("══════ S52 手电筒参数对比 ══════");
            _log.AppendLine($"场景 = {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            _log.AppendLine();

            _fl = FindFirstObjectByType<AbyssEcho.Tools.FlashlightSystem>();
            _cam = Camera.main;
            if (_fl == null || _cam == null) { _log.AppendLine("❌ 缺少组件"); Dump(); yield break; }

            var flType = typeof(AbyssEcho.Tools.FlashlightSystem);
            _spot = flType.GetField("spotLight", BF)?.GetValue(_fl) as Light;
            if (_spot == null) { _log.AppendLine("❌ spotLight 为 null"); Dump(); yield break; }

            var setOn = flType.GetMethod("SetLightOn", BF);
            var normalIntensityField = flType.GetField("normalIntensity", BF);
            var rangeField = flType.GetField("range", BF);
            var spotAngleField = flType.GetField("spotAngle", BF);

            // 关闭 PlayerLook，固定朝向
            var look = FindFirstObjectByType<AbyssEcho.Player.PlayerLook>();
            if (look != null) look.enabled = false;
            var motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            if (motor != null) motor.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

            yield return new WaitForSeconds(0.4f);

            // ── 构建黑暗环境 ──
            var oldAmbient = RenderSettings.ambientLight;
            var oldAI = RenderSettings.ambientIntensity;
            var oldFog = RenderSettings.fog;

            RenderSettings.ambientLight = new Color(0.015f, 0.015f, 0.02f);
            RenderSettings.ambientIntensity = 0.1f;
            RenderSettings.fog = false;

            var saved = new System.Collections.Generic.List<(Light, bool)>();
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional) continue;
                saved.Add((l, l.enabled));
                l.enabled = false;
            }

            float origInt = (float)normalIntensityField.GetValue(_fl);
            float origRange = (float)rangeField.GetValue(_fl);
            float origAngle = (float)spotAngleField.GetValue(_fl);
            _log.AppendLine($"原参数: intensity={origInt} range={origRange} spotAngle={origAngle}");
            _log.AppendLine();

            // ── 基线：手电关 ──
            setOn.Invoke(_fl, new object[] { false });
            _spot.enabled = false;
            yield return new WaitForSeconds(0.3f);

            yield return Measure("基线_全黑");
            float baseCenter = _lastCenter;
            _log.AppendLine($"  基线中心亮度 = {baseCenter:F4}");
            _log.AppendLine();

            // ── 参数组合测试 ──
            var combos = new (float intensity, float range, float angle, string note)[]
            {
                (3.2f,  22f, 55f, "当前配置"),
                (8f,    22f, 55f, "只提高强度"),
                (15f,   22f, 55f, "强度 15"),
                (25f,   22f, 55f, "强度 25"),
                (8f,    12f, 55f, "强度8+射程12"),
                (8f,    8f,  55f, "强度8+射程8"),
                (15f,   12f, 55f, "强度15+射程12"),
                (15f,   8f,  50f, "强度15+射程8"),
                (25f,   12f, 50f, "强度25+射程12"),
                (4f,    8f,  50f, "强度4+射程8"),
                (6f,    10f, 50f, "强度6+射程10"),
            };

            _log.AppendLine("──── 参数组合测试 ────");
            _log.AppendLine("  强度  射程  角度 | 中心亮度  提升     判定");

            var results = new System.Collections.Generic.List<(float inten, float range, float ang, float center, float delta)>();

            foreach (var c in combos)
            {
                normalIntensityField.SetValue(_fl, c.intensity);
                rangeField.SetValue(_fl, c.range);
                spotAngleField.SetValue(_fl, c.angle);

                _spot.range = c.range;
                _spot.spotAngle = c.angle;

                setOn.Invoke(_fl, new object[] { true });
                yield return new WaitForSeconds(0.25f);

                yield return Measure($"I{c.intensity}_R{c.range}_A{c.angle}");
                float center = _lastCenter;

                float delta = center - baseCenter;
                string verdict = delta > 0.10f ? "✅ 明显" : (delta > 0.04f ? "△ 勉强" : "❌ 不可见");

                _log.AppendLine($"  {c.intensity,5:F1} {c.range,5:F0} {c.angle,5:F0} | {center,8:F4}  {delta,7:F4}   {verdict}  {c.note}");
                results.Add((c.intensity, c.range, c.angle, center, delta));

                setOn.Invoke(_fl, new object[] { false });
                _spot.enabled = false;
                yield return new WaitForSeconds(0.2f);
            }

            // ── 推荐 ──
            _log.AppendLine();
            _log.AppendLine("──── 分析 ────");

            // 找最小改动且效果明显的组合
            float bestDelta = 0f;
            float bestI = origInt, bestR = origRange, bestA = origAngle;
            foreach (var r in results)
            {
                if (r.delta > bestDelta && r.delta <= 0.45f)   // 不要太亮
                {
                    bestDelta = r.delta; bestI = r.inten; bestR = r.range; bestA = r.ang;
                }
            }

            _log.AppendLine($"  最佳组合（提升明显但不过曝）: intensity={bestI} range={bestR} spotAngle={bestA}  提升={bestDelta:F4}");
            _log.AppendLine($"  原配置提升: {results[0].delta:F4}");

            // 恢复
            normalIntensityField.SetValue(_fl, origInt);
            rangeField.SetValue(_fl, origRange);
            spotAngleField.SetValue(_fl, origAngle);
            _spot.range = origRange;
            _spot.spotAngle = origAngle;
            RenderSettings.ambientLight = oldAmbient;
            RenderSettings.ambientIntensity = oldAI;
            RenderSettings.fog = oldFog;
            foreach (var (l, s) in saved) { if (l != null) l.enabled = s; }

            Dump();
        }

        private float _lastCenter;

        private IEnumerator Measure(string name)
        {
            yield return new WaitForEndOfFrame();

            int w = shotWidth, h = shotHeight;
            RenderTexture rt = null;
            Texture2D tex = null;
            _lastCenter = 0f;

            try
            {
                rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
                var prevTarget = _cam.targetTexture;
                var prevActive = RenderTexture.active;

                _cam.targetTexture = rt;
                _cam.Render();
                _cam.targetTexture = prevTarget;

                RenderTexture.active = rt;
                tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = prevActive;

                byte[] png = tex.EncodeToPNG();
                string dir = FindReportsDir();
                if (dir != null)
                {
                    string shots = Path.Combine(dir, "FlashlightTuning");
                    Directory.CreateDirectory(shots);
                    File.WriteAllBytes(Path.Combine(shots, $"{name}.png"), png);
                }

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
                _lastCenter = cSum / Mathf.Max(1, cN);
            }
            catch (System.Exception e)
            {
                _log.AppendLine($"    测量 {name} 失败: {e.GetType().Name}");
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
            Debug.Log("<b>[FlashlightTuning]</b>\n" + text);

            string dir = FindReportsDir();
            if (dir != null)
            {
                try { File.WriteAllText(Path.Combine(dir, "FlashlightTuning_Ch3.txt"), text, new UTF8Encoding(false)); }
                catch { }
            }

            if (!Application.isEditor) Application.Quit(0);
        }
    }
}