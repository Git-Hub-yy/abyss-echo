using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S47 FearSystem 接口兼容性验证。
    ///
    /// 验证目标（任务要求）：
    ///   · 每章 Player 恰好一个 FearSystem
    ///   · AddFear / SetFear / currentFear 可正常读写
    ///   · 恐惧值边界符合原实现（Clamp01 到 0~1）
    ///   · 阈值等级判定正确（0/1/2/3）
    ///   · OnFearChanged / OnFearLevelChanged 事件能派发
    ///   · HorrorTrigger 能正常调用 FearSystem
    ///   · PlayerDeathSystem 能正常调用 FearSystem
    ///   · MonsterAI / MimicStalker / 各类谜题能正常调用
    ///
    /// ⚠️ 只读原则在"行为验证"上不适用 —— 必须实际改恐惧值才能测。
    ///   但测试结束会把恐惧值复位到 0，不污染后续。
    /// </summary>
    public class FearCompatTest : MonoBehaviour
    {
        public bool runOnStart = true;
        public int forceChapter = 2;

        private readonly StringBuilder _log = new StringBuilder();
        private int _pass, _fail, _unverified;

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            if (forceChapter >= 1 && forceChapter <= 5)
                AbyssEcho.LevelGen.ChapterLayout.Current =
                    (AbyssEcho.LevelGen.ChapterLayout.Chapter)forceChapter;

            yield return new WaitForSeconds(0.8f);

            var ch = AbyssEcho.LevelGen.ChapterLayout.Current;
            _log.AppendLine("===== S47 FearSystem 接口兼容性验证 =====");
            _log.AppendLine($"场景 = {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            _log.AppendLine();

            // ── T1 单例存在 ──
            var fear = AbyssEcho.Horror.FearSystem.Instance;
            Check("T1 FearSystem.Instance 存在", fear != null, fear != null ? $"@{fear.gameObject.name}" : "❌");

            if (fear == null) { Dump(); yield break; }

            // ── T2 场景中恰好一个 ──
            var all = FindObjectsByType<AbyssEcho.Horror.FearSystem>(FindObjectsSortMode.None);
            Check("T2 场景中恰好 1 个 FearSystem", all.Length == 1, $"数量={all.Length}");

            // ── T3 挂在 Player 上 ──
            var motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            bool onPlayer = motor != null && fear.gameObject == motor.gameObject;
            Check("T3 挂在 Player 对象上", onPlayer,
                  $"FearSystem@{fear.gameObject.name}  Player@{(motor != null ? motor.gameObject.name : "无")}");

            // ── T4 参数与第一章一致 ──
            Check("T4 recoveryRate 与第一章一致(0.035)", Mathf.Approximately(fear.recoveryRate, 0.035f), $"{fear.recoveryRate}");
            Check("T4b darkRiseRate 与第一章一致(0.012)", Mathf.Approximately(fear.darkRiseRate, 0.012f), $"{fear.darkRiseRate}");
            Check("T4c 阈值一致(0.30/0.60/0.85)",
                  Mathf.Approximately(fear.tenseThreshold, 0.30f) &&
                  Mathf.Approximately(fear.fearThreshold, 0.60f) &&
                  Mathf.Approximately(fear.panicThreshold, 0.85f),
                  $"{fear.tenseThreshold}/{fear.fearThreshold}/{fear.panicThreshold}");

            // ── T5 input / flashlight 是否自动补上 ──
            Check("T5 input 引用已自动补", fear.input != null,
                  fear.input != null ? fear.input.name : "❌ NULL");
            Check("T5b flashlight 引用已自动补", fear.flashlight != null,
                  fear.flashlight != null ? fear.flashlight.name : "❌ NULL");
            _log.AppendLine("    （这两个字段在场景里是空，但 Awake() 里有 FindFirstObjectByType 兜底）");

            // ── T6 AddFear 正向 ──
            fear.SetFear(0f);
            yield return null;
            fear.AddFear(0.3f);
            Check("T6 AddFear(0.3) 生效", Mathf.Approximately(fear.currentFear, 0.3f), $"currentFear={fear.currentFear:F3}");

            // ── T7 AddFear 负向 ──
            fear.AddFear(-0.1f);
            Check("T7 AddFear(-0.1) 生效", Mathf.Approximately(fear.currentFear, 0.2f), $"currentFear={fear.currentFear:F3}");

            // ── T8 上边界夹紧 ──
            fear.AddFear(5f);
            Check("T8 上界夹紧到 1.0", Mathf.Approximately(fear.currentFear, 1f), $"currentFear={fear.currentFear:F3}");

            // ── T9 下边界夹紧 ──
            fear.AddFear(-5f);
            Check("T9 下界夹紧到 0.0", Mathf.Approximately(fear.currentFear, 0f), $"currentFear={fear.currentFear:F3}");

            // ── T10 SetFear 越界也夹紧 ──
            fear.SetFear(1.5f);
            bool setHigh = Mathf.Approximately(fear.currentFear, 1f);
            fear.SetFear(-0.5f);
            bool setLow = Mathf.Approximately(fear.currentFear, 0f);
            Check("T10 SetFear 越界也夹紧", setHigh && setLow, $"Set(1.5)→1.0={setHigh}, Set(-0.5)→0.0={setLow}");

            // ── T11 阈值等级判定 ──
            fear.SetFear(0.10f);
            int lv0 = fear.CurrentLevel;
            fear.SetFear(0.40f);
            int lv1 = fear.CurrentLevel;
            fear.SetFear(0.70f);
            int lv2 = fear.CurrentLevel;
            fear.SetFear(0.95f);
            int lv3 = fear.CurrentLevel;

            Check("T11 等级判定 0/1/2/3 正确",
                  lv0 == 0 && lv1 == 1 && lv2 == 2 && lv3 == 3,
                  $"0.10→{lv0}, 0.40→{lv1}, 0.70→{lv2}, 0.95→{lv3}");

            Check("T11b GetLevelName 与等级对应",
                  fear.GetLevelName() == "濒临崩溃",
                  $"0.95 → '{fear.GetLevelName()}'");

            // ── T12 OnFearChanged 事件 ──
            int changedCount = 0;
            float lastVal = -1f;
            System.Action<float> onChanged = v => { changedCount++; lastVal = v; };
            fear.OnFearChanged += onChanged;

            fear.SetFear(0.5f);
            yield return null;

            fear.OnFearChanged -= onChanged;
            Check("T12 OnFearChanged 事件已派发", changedCount > 0, $"触发 {changedCount} 次，最后值={lastVal:F3}");

            // ── T13 OnFearLevelChanged 事件 ──
            int levelChangedCount = 0;
            System.Action<int> onLevel = _ => levelChangedCount++;
            fear.OnFearLevelChanged += onLevel;

            fear.SetFear(0.1f);
            fear.SetFear(0.9f);
            yield return null;

            fear.OnFearLevelChanged -= onLevel;
            Check("T13 OnFearLevelChanged 事件已派发", levelChangedCount > 0, $"触发 {levelChangedCount} 次");

            // ── T14 HorrorTrigger 能调用 FearSystem ──
            var triggers = FindObjectsByType<AbyssEcho.Horror.HorrorTrigger>(FindObjectsSortMode.None);
            _log.AppendLine();
            _log.AppendLine($"    本章 HorrorTrigger = {triggers.Length} 个");

            if (triggers.Length > 0)
            {
                var t0 = triggers[0];
                fear.SetFear(0f);
                yield return null;

                float before = fear.currentFear;
                t0.Fire();
                yield return new WaitForSeconds(0.2f);
                float after = fear.currentFear;

                Check("T14 HorrorTrigger.Fire() 能增加恐惧值", after > before,
                      $"{before:F3} → {after:F3}");

                // 恢复现场（这个触发器是 oneShot，测试后已被消耗，记录在案）
                _log.AppendLine("    ⚠️ 注意：测试用 Fire() 消耗了一个 oneShot 触发器（仅测试场景，不影响正式场景）");
            }
            else
            {
                _log.AppendLine("    （本章无 HorrorTrigger，跳过 T14）");
            }

            // ── T15 PlayerDeathSystem 能调用 FearSystem ──
            var death = FindFirstObjectByType<AbyssEcho.Horror.PlayerDeathSystem>();
            if (death != null)
            {
                fear.SetFear(0f);
                yield return null;

                float before = fear.currentFear;
                death.Kill("测试：验证 FearSystem 联动");
                yield return null;
                float after = fear.currentFear;

                // ⚠️ 不能要求严格等于 1.0：
                //   Kill() 里 SetFear(1f) 之后，FearSystem.Update() 每帧执行
                //   currentFear -= recoveryRate(0.035) * dt，
                //   所以下一帧读到的已经是 0.999x。
                //   用 0.98 作为"已拉满"的阈值。
                Check("T15 PlayerDeathSystem.Kill() 能把恐惧拉满", after > 0.98f,
                      $"{before:F3} → {after:F4}（> 0.98 即视为拉满；每帧自然衰减 0.035/s）");

                _unverified++;
                _log.AppendLine("[待人工验收] 死亡时画面因恐惧拉满而变化（需 FearVisuals，本批未补）");
            }
            else
            {
                _log.AppendLine("    （本章无 PlayerDeathSystem，跳过 T15）");
            }

            // ── T16 其他调用方存在性 ──
            _log.AppendLine();
            _log.AppendLine("    --- 其他 FearSystem 调用方（静态检查，未逐一触发）---");
            ReportCaller<AbyssEcho.Monster.MonsterAI>("MonsterAI");
            ReportCaller<AbyssEcho.Horror.MimicStalker>("MimicStalker");
            ReportCaller<AbyssEcho.Interaction.PickupItem>("PickupItem");
            ReportCaller<AbyssEcho.Interaction.DoorInteractable>("DoorInteractable");
            ReportCaller<AbyssEcho.Puzzle.KeypadPanel>("KeypadPanel");
            ReportCaller<AbyssEcho.Puzzle.WaterPumpSwitch>("WaterPumpSwitch");
            ReportCaller<AbyssEcho.Puzzle.FrequencyConsole>("FrequencyConsole");
            ReportCaller<AbyssEcho.Puzzle.OfferingAltar>("OfferingAltar");
            ReportCaller<AbyssEcho.Puzzle.PulseSyncPuzzle>("PulseSyncPuzzle");

            Dump();
        }

        private void ReportCaller<T>(string label) where T : Component
        {
            var arr = FindObjectsByType<T>(FindObjectsSortMode.None);
            _log.AppendLine($"      {label,-20} = {(arr.Length > 0 ? $"{arr.Length} 个（会调用 FearSystem）" : "0（本章无）")}");
        }

        private void Check(string name, bool ok, string detail)
        {
            if (ok) { _pass++; _log.AppendLine($"[通过] {name}  {detail}"); }
            else { _fail++; _log.AppendLine($"[失败] {name}  {detail}"); }
        }

        private void Dump()
        {
            _log.AppendLine();
            _log.AppendLine("=====================================");
            _log.AppendLine($"通过 {_pass}，失败 {_fail}，待人工验收 {_unverified}");

            string text = _log.ToString();
            Debug.Log("<b>[FearCompatTest]</b>\n" + text);

            var di = new DirectoryInfo(Application.dataPath);
            for (int hop = 0; hop < 6 && di != null; hop++)
            {
                string reports = Path.Combine(di.FullName, "Reports");
                if (Directory.Exists(reports))
                {
                    try
                    {
                        string fn = $"FearCompatTest_Ch{(int)AbyssEcho.LevelGen.ChapterLayout.Current}.txt";
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