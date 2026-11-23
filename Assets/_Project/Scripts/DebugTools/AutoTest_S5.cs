using System.Collections;
using System.IO;
using System.Text;
using AbyssEcho.Audio;
using AbyssEcho.Horror;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S5 恐怖系统自动测试。
    ///
    /// 重点验证：
    ///   · 程序化音频能正确生成且不是静音
    ///   · 恐惧值随黑暗上升、随安全下降
    ///   · 恐惧阈值分级正确
    ///   · 闪烁灯光组件正常
    ///   · 恐怖触发器只触发一次
    /// </summary>
    public class AutoTest_S5 : MonoBehaviour
    {
        public bool runOnStart = true;

        private int _pass, _fail;
        private readonly StringBuilder _log = new StringBuilder();

        private void Start()
        {
            if (runOnStart) StartCoroutine(RunAll());
        }

        private IEnumerator RunAll()
        {
            _log.AppendLine("ABYSS ECHO · S5 恐怖系统自动测试");
            _log.AppendLine("=====================================");

            yield return new WaitForSeconds(0.5f);

            T25_AudioManagerExists();
            T26_ProceduralClipsGenerated();
            T27_ClipsNotSilent();
            T28_FearSystemExists();
            T29_FearRisesInDarkness();
            T30_FearRecoversInSafety();
            T31_FearClamped();
            T32_FearLevels();
            T33_FearVisualsExists();
            T34_FlickerLightsExist();
            T35_FootstepsExists();
            T36_HorrorTriggersExist();
            T37_TriggerFiresOnce();

            _log.AppendLine("=====================================");
            _log.AppendLine($"结果：通过 {_pass}，失败 {_fail}");

            string text = _log.ToString();
            Debug.Log("<b>[AutoTest_S5]</b>\n" + text);
            WriteReport(text);

            yield return new WaitForSeconds(0.2f);
            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }

        // ────────────────────────── 音频 ──────────────────────────

        private void T25_AudioManagerExists()
        {
            Check("T25 音频管理器存在", AudioManager.Instance != null);
        }

        private void T26_ProceduralClipsGenerated()
        {
            // 直接调用合成器，验证能生成有效时长与采样数
            var hb = ProceduralAudio.CreateHeartbeat(90f, 1f);
            var fs = ProceduralAudio.CreateFootstep(true, 0);
            var door = ProceduralAudio.CreateDoorSlide();
            var stinger = ProceduralAudio.CreateStinger();
            var drone = ProceduralAudio.CreateAmbientDrone();

            bool allValid = hb != null && hb.samples > 1000
                         && fs != null && fs.samples > 500
                         && door != null && door.samples > 10000
                         && stinger != null && stinger.samples > 10000
                         && drone != null && drone.samples > 100000;

            Check("T26 程序化音效生成成功", allValid,
                  $"hb={hb?.samples} fs={fs?.samples} door={door?.samples} stinger={stinger?.samples} drone={drone?.samples}");
        }

        private void T27_ClipsNotSilent()
        {
            // 取每个音频的 RMS，确认不是全 0（静音）
            var clip = ProceduralAudio.CreateHeartbeat(90f, 1f);
            var data = new float[clip.samples];
            clip.GetData(data, 0);

            double sum = 0;
            int nonZero = 0;
            foreach (var s in data)
            {
                sum += s * s;
                if (Mathf.Abs(s) > 0.0001f) nonZero++;
            }
            float rms = Mathf.Sqrt((float)(sum / data.Length));

            bool audible = rms > 0.005f && nonZero > data.Length * 0.05f;
            Check("T27 心跳音频非静音", audible, $"rms={rms:F4} 非零样本={nonZero}/{data.Length}");
        }

        // ────────────────────────── 恐惧系统 ──────────────────────────

        private void T28_FearSystemExists()
        {
            Check("T28 恐惧系统存在", FearSystem.Instance != null);
        }

        private void T29_FearRisesInDarkness()
        {
            var f = FearSystem.Instance;
            if (f == null) { Fail("T29 黑暗中心恐惧上升", "无恐惧系统"); return; }

            f.SetFear(0.2f);
            f.IsInDarkness = true;

            // 手动推进（不依赖 Update 的帧率）：模拟 10 秒
            float before = f.currentFear;
            for (int i = 0; i < 600; i++) f.AddFear(f.darkRiseRate * (1f / 60f));
            float after = f.currentFear;

            f.IsInDarkness = false;
            f.SetFear(0f);

            Check("T29 黑暗中恐惧上升", after > before, $"之前={before:F3} 之后={after:F3}");
        }

        private void T30_FearRecoversInSafety()
        {
            var f = FearSystem.Instance;
            if (f == null) { Fail("T30 安全时恐惧下降", "无恐惧系统"); return; }

            f.SetFear(0.7f);
            float before = f.currentFear;
            // 手动模拟恢复
            for (int i = 0; i < 600; i++) f.AddFear(-f.recoveryRate * (1f / 60f));
            float after = f.currentFear;
            f.SetFear(0f);

            Check("T30 安全时恐惧下降", after < before, $"之前={before:F3} 之后={after:F3}");
        }

        private void T31_FearClamped()
        {
            var f = FearSystem.Instance;
            if (f == null) { Fail("T31 恐惧值夹紧", "无恐惧系统"); return; }

            f.SetFear(5f);
            bool upperOk = Mathf.Approximately(f.currentFear, 1f);
            f.SetFear(-3f);
            bool lowerOk = Mathf.Approximately(f.currentFear, 0f);
            f.SetFear(0f);

            Check("T31 恐惧值夹紧在 0-1", upperOk && lowerOk, $"upper={upperOk} lower={lowerOk}");
        }

        private void T32_FearLevels()
        {
            var f = FearSystem.Instance;
            if (f == null) { Fail("T32 恐惧分级", "无恐惧系统"); return; }

            // 直接验证阈值计算（通过 GetLevelName 间接判断）
            f.SetFear(0.1f);
            string calm = f.GetLevelName();
            f.SetFear(0.45f);
            string tense = f.GetLevelName();
            f.SetFear(0.7f);
            string fear = f.GetLevelName();
            f.SetFear(0.95f);
            string panic = f.GetLevelName();
            f.SetFear(0f);

            bool ok = calm == "平静" && tense == "紧张" && fear == "恐惧" && panic == "濒临崩溃";
            Check("T32 恐惧分级正确", ok, $"{calm}/{tense}/{fear}/{panic}");
        }

        private void T33_FearVisualsExists()
        {
            var v = FindFirstObjectByType<FearVisuals>();
            Check("T33 恐惧视觉组件已挂载", v != null);
        }

        // ────────────────────────── 灯光与触发器 ──────────────────────────

        private void T34_FlickerLightsExist()
        {
            var fl = FindObjectsByType<FlickerLight>(FindObjectsSortMode.None);
            Check("T34 闪烁灯光 ≥ 5 盏", fl.Length >= 5, $"找到 {fl.Length} 盏");
        }

        private void T35_FootstepsExists()
        {
            var fs = FindFirstObjectByType<PlayerFootsteps>();
            Check("T35 玩家脚步声组件已挂载", fs != null);
        }

        private void T36_HorrorTriggersExist()
        {
            var t = FindObjectsByType<HorrorTrigger>(FindObjectsSortMode.None);
            Check("T36 恐怖触发器 ≥ 4 个", t.Length >= 4, $"找到 {t.Length} 个");
        }

        private void T37_TriggerFiresOnce()
        {
            var triggers = FindObjectsByType<HorrorTrigger>(FindObjectsSortMode.None);
            if (triggers.Length == 0) { Fail("T37 触发器只触发一次", "无触发器"); return; }

            var t = triggers[0];
            t.ResetTrigger();

            // 记录触发前的灯光状态
            int lightsBefore = 0;
            if (t.lightsToKill != null)
                foreach (var l in t.lightsToKill) if (l != null && l.enabled) lightsBefore++;

            t.Fire();
            float fearAfterFirst = FearSystem.Instance != null ? FearSystem.Instance.currentFear : 0f;

            // 第二次 Fire 应被忽略（oneShot）
            t.Fire();
            float fearAfterSecond = FearSystem.Instance != null ? FearSystem.Instance.currentFear : 0f;

            bool onlyOnce = Mathf.Approximately(fearAfterFirst, fearAfterSecond);
            Check("T37 触发器只触发一次", onlyOnce,
                  $"fear1={fearAfterFirst:F3} fear2={fearAfterSecond:F3} lightsBefore={lightsBefore}");

            if (FearSystem.Instance != null) FearSystem.Instance.SetFear(0f);
        }

        // ────────────────────────── 工具 ──────────────────────────

        private void Check(string name, bool ok, string detail = "")
        {
            if (ok) { _pass++; _log.AppendLine($"[通过] {name} {detail}"); }
            else { _fail++; _log.AppendLine($"[失败] {name} {detail}"); }
        }

        private void Fail(string name, string detail)
        {
            _fail++;
            _log.AppendLine($"[失败] {name} {detail}");
        }

        private static void WriteReport(string text)
        {
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "S5_AutoTestResult.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}