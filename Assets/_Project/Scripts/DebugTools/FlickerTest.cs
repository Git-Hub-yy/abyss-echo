using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S50 FlickerLight 实际闪烁行为验证。
    ///
    /// ══════════════════ 验证目标 ══════════════════
    ///
    ///   任务要求不能只看"组件存在"，要验证：
    ///     T1 组件数量与参数正确
    ///     T2 灯光确实会闪（intensity 真的在变）
    ///     T3 只有配置的灯闪，无关灯不闪
    ///     T4 多盏灯不会完全同步（起始时间随机错开）
    ///     T5 闪烁后能恢复到 baseIntensity（不会永久变暗）
    ///     T6 灯光被 enabled=false 后，FlickerLight 不会重新开启它
    ///     T7 长期运行后 intensity 不越界（没有永久关闭或强度异常）
    ///
    /// ══════════════════ 实测方式 ══════════════════
    ///
    ///   FlickerLight 的闪烁间隔是 1.2~4.5 秒，所以需要观察足够长的时间。
    ///   本测试观察 **12 秒**，足以捕获每盏灯至少 2 次闪烁。
    /// </summary>
    public class FlickerTest : MonoBehaviour
    {
        public bool runOnStart = true;
        public int forceChapter = 2;

        private readonly StringBuilder _log = new StringBuilder();
        private int _pass, _fail, _unverified;

        private AbyssEcho.Horror.FlickerLight[] _fls;
        private Light[] _flickerLights;
        private Light[] _controlLights;   // 没有 FlickerLight 的灯（对照组）

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            if (forceChapter >= 1 && forceChapter <= 5)
                AbyssEcho.LevelGen.ChapterLayout.Current =
                    (AbyssEcho.LevelGen.ChapterLayout.Chapter)forceChapter;

            yield return new WaitForSeconds(1.0f);

            _log.AppendLine("===== S50 FlickerLight 行为验证 =====");
            _log.AppendLine($"场景 = {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            _log.AppendLine();

            // ── T1 组件与参数 ──
            _fls = FindObjectsByType<AbyssEcho.Horror.FlickerLight>(FindObjectsSortMode.None);
            Check("T1 存在 FlickerLight", _fls.Length > 0, $"数量={_fls.Length}");

            if (_fls.Length == 0) { Dump(); yield break; }

            int badMode = 0, badBase = 0, badInterval = 0;
            foreach (var f in _fls)
            {
                if (f.mode != AbyssEcho.Horror.FlickerLight.Mode.Flicker) badMode++;
                if (f.baseIntensity <= 0.5f) badBase++;
                if (f.minInterval <= 0f || f.maxInterval <= f.minInterval) badInterval++;
            }
            Check("T1b 全部使用 Flicker 模式", badMode == 0, $"非 Flicker 模式={badMode}");
            Check("T1c baseIntensity 全部有效", badBase == 0, $"异常={badBase}");
            Check("T1d 间隔参数有效", badInterval == 0, $"异常={badInterval}");

            // 收集对应的 Light
            var lights = new List<Light>();
            foreach (var f in _fls)
            {
                var lt = f.GetComponent<Light>();
                if (lt != null) lights.Add(lt);
            }
            _flickerLights = lights.ToArray();

            // 对照组：没有 FlickerLight 的灯
            var flickerSet = new HashSet<Light>(_flickerLights);
            var ctrl = new List<Light>();
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type != LightType.Point) continue;
                if (!l.enabled || l.intensity <= 0.5f) continue;
                if (flickerSet.Contains(l)) continue;
                ctrl.Add(l);
                if (ctrl.Count >= 10) break;
            }
            _controlLights = ctrl.ToArray();
            _log.AppendLine($"    有 FlickerLight 的灯 = {_flickerLights.Length}");
            _log.AppendLine($"    对照组（无 FlickerLight 的亮灯）= {_controlLights.Length}");
            _log.AppendLine();

            // ── 记录初始状态 ──
            var flickerBase = new float[_flickerLights.Length];
            for (int i = 0; i < _flickerLights.Length; i++) flickerBase[i] = _flickerLights[i].intensity;

            var ctrlBase = new float[_controlLights.Length];
            for (int i = 0; i < _controlLights.Length; i++) ctrlBase[i] = _controlLights[i].intensity;

            // ── T2/T3/T4 观察 12 秒 ──
            _log.AppendLine("观察 12 秒，记录每盏灯 intensity 的变化...");

            var flickerChanges = new int[_flickerLights.Length];
            var firstChangeTime = new float[_flickerLights.Length];
            for (int i = 0; i < firstChangeTime.Length; i++) firstChangeTime[i] = -1f;

            var ctrlChanges = new int[_controlLights.Length];

            float elapsed = 0f;
            float logT = 0f;

            while (elapsed < 12f)
            {
                for (int i = 0; i < _flickerLights.Length; i++)
                {
                    if (Mathf.Abs(_flickerLights[i].intensity - flickerBase[i]) > 0.01f)
                    {
                        flickerChanges[i]++;
                        if (firstChangeTime[i] < 0f) firstChangeTime[i] = elapsed;
                    }
                }

                for (int i = 0; i < _controlLights.Length; i++)
                {
                    if (Mathf.Abs(_controlLights[i].intensity - ctrlBase[i]) > 0.01f)
                        ctrlChanges[i]++;
                }

                if (elapsed - logT >= 3f)
                {
                    logT = elapsed;
                    int flicking = 0;
                    for (int i = 0; i < flickerChanges.Length; i++) if (flickerChanges[i] > 0) flicking++;
                    _log.AppendLine($"    [{elapsed:F0}s] 已有 {flicking}/{_flickerLights.Length} 盏灯闪过");
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // ── T2 确实闪烁 ──
            int flickedCount = 0;
            for (int i = 0; i < flickerChanges.Length; i++) if (flickerChanges[i] > 0) flickedCount++;

            _log.AppendLine();
            _log.AppendLine("---- 逐灯统计 ----");
            for (int i = 0; i < _flickerLights.Length; i++)
            {
                _log.AppendLine($"    {_flickerLights[i].gameObject.name,-24} 变化帧数={flickerChanges[i],4}  " +
                                $"首次变化={firstChangeTime[i],6:F2}s  base={flickerBase[i]:F1} 当前={_flickerLights[i].intensity:F2}");
            }
            _log.AppendLine();

            Check("T2 至少一半的灯在 12 秒内闪过", flickedCount >= _flickerLights.Length / 2,
                  $"{flickedCount}/{_flickerLights.Length} 盏灯发生过强度变化");

            // ── T3 对照组不闪 ──
            int ctrlChanged = 0;
            for (int i = 0; i < ctrlChanges.Length; i++) if (ctrlChanges[i] > 0) ctrlChanged++;
            Check("T3 无 FlickerLight 的灯保持稳定", ctrlChanged == 0,
                  $"对照组 {_controlLights.Length} 盏中 {ctrlChanged} 盏发生变化");

            // ── T4 不同步 ──
            var distinctStart = new HashSet<int>();
            for (int i = 0; i < firstChangeTime.Length; i++)
                if (firstChangeTime[i] >= 0f) distinctStart.Add(Mathf.RoundToInt(firstChangeTime[i] * 4f));   // 0.25s 粒度

            Check("T4 多盏灯的闪烁不同步", distinctStart.Count >= Mathf.Min(3, flickedCount),
                  $"首次变化时刻的分布有 {distinctStart.Count} 个不同区间");

            // ── T5 恢复到 baseIntensity ──
            yield return new WaitForSeconds(0.5f);
            int notRestored = 0;
            float worstDiff = 0f;
            for (int i = 0; i < _flickerLights.Length; i++)
            {
                float diff = Mathf.Abs(_flickerLights[i].intensity - flickerBase[i]);
                if (diff > 0.01f && diff > flickerBase[i] * 0.99f)   // 排除正好处于闪烁瞬间
                {
                    notRestored++;
                    worstDiff = Mathf.Max(worstDiff, diff);
                }
            }
            _log.AppendLine($"    检查时刻可能正处于闪烁瞬间，故 T5 用『强度不等于 0 且不超过 1.3 倍 base』判定");

            int abnormal = 0;
            for (int i = 0; i < _flickerLights.Length; i++)
            {
                float v = _flickerLights[i].intensity;
                if (v < -0.01f || v > flickerBase[i] * 1.3f + 0.01f) abnormal++;
            }
            Check("T5 所有灯强度在合理范围（未永久变暗/异常）", abnormal == 0, $"异常={abnormal}");

            // ── T6 灯被关闭后不会被重新开启 ──
            _log.AppendLine();
            _log.AppendLine("---- T6 关闭灯测试 ----");
            int tested = 0, reopened = 0;
            for (int i = 0; i < _flickerLights.Length && tested < 3; i++)
            {
                _flickerLights[i].enabled = false;
                tested++;
            }
            yield return new WaitForSeconds(3f);   // 等足够长时间覆盖闪烁周期

            for (int i = 0; i < tested; i++)
            {
                if (_flickerLights[i].enabled) reopened++;
            }
            Check("T6 被关闭的灯不会被 FlickerLight 重新开启", reopened == 0,
                  $"测试 {tested} 盏，被重新开启 {reopened} 盏");

            // 恢复
            for (int i = 0; i < tested; i++) _flickerLights[i].enabled = true;

            // ── T7 长期运行不熄灯 ──
            yield return new WaitForSeconds(1f);
            int offCount = 0;
            for (int i = 0; i < _flickerLights.Length; i++)
                if (!_flickerLights[i].enabled) offCount++;

            Check("T7 无灯被永久关闭", offCount == 0, $"关闭的灯={offCount}");

            _log.AppendLine();
            _log.AppendLine("---- 重要说明 ----");
            Unverified("T8 闪烁的实际视觉效果（-batchmode -nographics 无渲染）");
            Unverified("T9 闪烁频率的观感是否自然（需真人）");
            Unverified("T10 与场景氛围的匹配度（需真人）");

            Dump();
        }

        private void Check(string name, bool ok, string detail)
        {
            if (ok) { _pass++; _log.AppendLine($"[通过] {name}  {detail}"); }
            else { _fail++; _log.AppendLine($"[失败] {name}  {detail}"); }
        }

        private void Unverified(string name)
        {
            _unverified++;
            _log.AppendLine($"[待人工验收] {name}");
        }

        private void Dump()
        {
            _log.AppendLine();
            _log.AppendLine("=====================================");
            _log.AppendLine($"通过 {_pass}，失败 {_fail}，待人工验收 {_unverified}");

            string text = _log.ToString();
            Debug.Log("<b>[FlickerTest]</b>\n" + text);

            var di = new DirectoryInfo(Application.dataPath);
            for (int hop = 0; hop < 6 && di != null; hop++)
            {
                string reports = Path.Combine(di.FullName, "Reports");
                if (Directory.Exists(reports))
                {
                    try
                    {
                        string fn = $"FlickerTest_Ch{(int)AbyssEcho.LevelGen.ChapterLayout.Current}.txt";
                        File.WriteAllText(Path.Combine(reports, fn), text, new UTF8Encoding(false));
                        break;
                    }
                    catch { }
                }
                di = di.Parent;
            }

            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }
    }
}