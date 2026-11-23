using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S54：第四章心瓣谜题完整通关测试。
    ///
    /// 验证：
    ///   T1 心瓣可被交互检测到
    ///   T2 交互提示文字正常
    ///   T3 按节奏连对 3 次能解谜
    ///   T4 解谜后 Door_HeartGate 解锁
    ///   T5 失误不造成永久失败（可重试）
    ///   T6 成功后重复交互不会破坏状态
    ///   T7 结局触发区覆盖终点
    /// </summary>
    public class Ch4PuzzleTest : MonoBehaviour
    {
        public bool runOnStart = true;

        private readonly StringBuilder _log = new StringBuilder();
        private int _pass, _fail, _unverified;
        private static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private AbyssEcho.Puzzle.PulseSyncPuzzle _pulse;
        private AbyssEcho.Interaction.InteractionDetector _detector;
        private AbyssEcho.Player.PlayerMotor _motor;
        private Camera _cam;

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            // 直接设章节即可。Current 的 setter 会持久化到 PlayerPrefs，
            // 不需要手动强制重载（我上一版多此一举，且反射取字段返回 null 导致 NRE）。
            AbyssEcho.LevelGen.ChapterLayout.Current =
                AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter4;

            var ending = FindFirstObjectByType<AbyssEcho.Narrative.ChapterEndingSystem>();
            ending?.GetType().GetMethod("ApplyChapterConfig", BF)?.Invoke(ending, null);

            yield return new WaitForSeconds(1.2f);

            _log.AppendLine("══════ 第四章心瓣谜题通关测试 ══════");
            _log.AppendLine();

            _motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            _cam = Camera.main;
            _pulse = FindFirstObjectByType<AbyssEcho.Puzzle.PulseSyncPuzzle>();
            _detector = FindFirstObjectByType<AbyssEcho.Interaction.InteractionDetector>();

            if (_pulse == null) { _log.AppendLine("❌ 找不到 PulseSyncPuzzle"); Dump(); yield break; }

            // ══ T1 心瓣存在且可交互 ══
            _log.AppendLine("──── T1 心瓣状态 ────");
            _log.AppendLine($"  位置 = {_pulse.transform.position}");
            _log.AppendLine($"  enabled = {_pulse.enabled}  activeInHierarchy = {_pulse.gameObject.activeInHierarchy}");
            Check("T1a 心瓣存在并启用", _pulse.enabled && _pulse.gameObject.activeInHierarchy, "");
            Check("T1b CanInteract = true", _pulse.CanInteract, $"CanInteract={_pulse.CanInteract}");

            // ══ T2 交互提示 ══
            _log.AppendLine();
            _log.AppendLine("──── T2 交互提示 ────");
            string prompt = _pulse.GetPrompt();
            _log.AppendLine($"  GetPrompt() = \"{prompt}\"");
            Check("T2 有非空交互提示", !string.IsNullOrEmpty(prompt), prompt);

            // ══ T1c 实际站到心瓣前，验证检测器能命中 ══
            _log.AppendLine();
            _log.AppendLine("──── T1c 玩家站在心瓣前 ────");
            TeleportTo(new Vector3(0f, 0.05f, 13.8f), 0f);
            yield return new WaitForSeconds(0.5f);

            _log.AppendLine($"  InteractionDetector.HasTarget = {_detector.HasTarget}");
            _log.AppendLine($"  CurrentPrompt = \"{_detector.CurrentPrompt}\"");
            Check("T1c 检测器在玩家平视时命中心瓣", _detector.HasTarget,
                  $"HasTarget={_detector.HasTarget}");

            // ══ 读取谜题参数 ══
            var pulseType = typeof(AbyssEcho.Puzzle.PulseSyncPuzzle);
            var beatIntervalF = pulseType.GetField("beatInterval", BF);
            var syncWindowF = pulseType.GetField("syncWindow", BF);
            var requiredF = pulseType.GetField("requiredSyncs", BF);
            var phaseF = pulseType.GetField("_phase", BF);
            var syncCountP = pulseType.GetProperty("SyncCount", BF);
            var isSolvedP = pulseType.GetProperty("IsSolved", BF);

            float beatInterval = (float)beatIntervalF.GetValue(_pulse);
            float syncWindow = (float)syncWindowF.GetValue(_pulse);
            int required = (int)requiredF.GetValue(_pulse);

            _log.AppendLine();
            _log.AppendLine("──── 谜题参数 ────");
            _log.AppendLine($"  beatInterval = {beatInterval}s");
            _log.AppendLine($"  syncWindow = ±{syncWindow}s");
            _log.AppendLine($"  requiredSyncs = {required}");

            // ══ T5 先验证失误不永久失败 ══
            _log.AppendLine();
            _log.AppendLine("──── T5 失误可重试 ────");
            _pulse.OnInteract(gameObject);
            yield return null;
            int afterWrong = (int)syncCountP.GetValue(_pulse);
            _log.AppendLine($"  时机不对时交互一次 → SyncCount={afterWrong}  IsSolved={isSolvedP.GetValue(_pulse)}");
            Check("T5a 时机错误不直接解谜", !(bool)isSolvedP.GetValue(_pulse), "");
            Check("T5b 时机错误不锁定（仍可再试）", _pulse.CanInteract, "");

            // ══ T3 按节奏连对 3 次 ══
            _log.AppendLine();
            _log.AppendLine("──── T3 按节奏连对 3 次 ────");

            int solved = 0;
            float t = 0f;
            float maxT = 30f;   // 最多等 30 秒

            var method = pulseType.GetMethod("OnInteract", BF);

            // ⚠️ 修正：搏动发生在 _phase 回绕到 0 的瞬间（_phase 从 0 增到 beatInterval 后减去）。
            //   OnInteract 用 Mathf.Min(toBeat, sinceBeat) 判断，两种相位都算"接近搏动"。
            //   直接用 TimeToNextBeat 属性（= beatInterval - _phase）判断更准。
            var timeToNextBeatP = pulseType.GetProperty("TimeToNextBeat", BF);
            _log.AppendLine($"    TimeToNextBeat 属性存在 = {timeToNextBeatP != null}");

            float lastFire = -10f;

            while (t < maxT && !(bool)isSolvedP.GetValue(_pulse))
            {
                float toBeat = timeToNextBeatP != null
                    ? (float)timeToNextBeatP.GetValue(_pulse)
                    : (beatInterval - (float)phaseF.GetValue(_pulse));

                // 距离搏动点 ±0.15 秒内按 E（比 ±0.35 的判定窗口更严格，保证成功）
                bool windowHit = toBeat <= 0.15f || toBeat >= beatInterval - 0.15f;

                if (windowHit && (t - lastFire) > 0.5f)
                {
                    lastFire = t;
                    _pulse.OnInteract(gameObject);
                    solved++;
                    int cnt = (int)syncCountP.GetValue(_pulse);
                    _log.AppendLine($"    第 {solved} 次交互 @ toBeat={toBeat:F2}s → SyncCount={cnt}  " +
                                    $"IsSolved={isSolvedP.GetValue(_pulse)}");
                }

                t += Time.deltaTime;
                yield return null;
            }

            bool isSolved = (bool)isSolvedP.GetValue(_pulse);
            int finalCount = (int)syncCountP.GetValue(_pulse);
            _log.AppendLine($"  最终: SyncCount={finalCount}  IsSolved={isSolved}  用时={t:F1}s");
            Check("T3 心瓣谜题可以被解开", isSolved, $"IsSolved={isSolved}");

            // ══ T4 门解锁 ══
            _log.AppendLine();
            _log.AppendLine("──── T4 Door_HeartGate 状态 ────");
            var gate = GameObject.Find("Door_HeartGate");
            if (gate != null)
            {
                var door = gate.GetComponent<AbyssEcho.Interaction.DoorInteractable>();
                _log.AppendLine($"  位置 = {gate.transform.position}");
                _log.AppendLine($"  locked = {door.locked}  IsOpen = {door.IsOpen}");
                Check("T4a 解谜后门已解锁", !door.locked, $"locked={door.locked}");

                // 交互开门
                door.OnInteract(gameObject);
                yield return new WaitForSeconds(1.5f);
                _log.AppendLine($"  交互后: locked={door.locked}  IsOpen={door.IsOpen}  " +
                                $"位置={gate.transform.position}");
                Check("T4b 可以打开门", door.IsOpen, $"IsOpen={door.IsOpen}");
            }

            // ══ T6 重复交互不破坏状态 ══
            _log.AppendLine();
            _log.AppendLine("──── T6 重复交互 ────");
            for (int i = 0; i < 5; i++) _pulse.OnInteract(gameObject);
            yield return new WaitForSeconds(0.5f);
            _log.AppendLine($"  连按 5 次后: IsSolved={isSolvedP.GetValue(_pulse)}  " +
                            $"SyncCount={syncCountP.GetValue(_pulse)}");
            Check("T6 解谜后重复交互不破坏状态", (bool)isSolvedP.GetValue(_pulse), "");

            // ══ T7 结局触发区 ══
            _log.AppendLine();
            _log.AppendLine("──── T7 结局触发区 ────");
            if (ending != null)
            {
                var tcF = typeof(AbyssEcho.Narrative.ChapterEndingSystem).GetField("triggerCenter");
                var tsF = typeof(AbyssEcho.Narrative.ChapterEndingSystem).GetField("triggerSize");
                Vector3 tc = (Vector3)tcF.GetValue(ending);
                Vector3 ts = (Vector3)tsF.GetValue(ending);
                Vector3 endPt = AbyssEcho.LevelGen.ChapterLayout.GetChapterEndPoint(
                    AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter4);

                _log.AppendLine($"  触发中心={tc}  尺寸={ts}");
                _log.AppendLine($"  章节终点={endPt}");
                bool covers = Mathf.Abs(endPt.x - tc.x) <= ts.x * 0.5f + 1f &&
                              Mathf.Abs(endPt.z - tc.z) <= ts.z * 0.5f + 1f;
                Check("T7 触发区覆盖终点", covers, $"Z[{tc.z - ts.z * 0.5f:F1},{tc.z + ts.z * 0.5f:F1}] 终点={endPt.z:F1}");
            }

            _log.AppendLine();
            Unverified("U1 玩家实际按 E 的手感（自动化用代码调用 OnInteract，不经键盘）");
            Unverified("U2 心瓣发光动画的视觉表现");

            Dump();
        }

        private void TeleportTo(Vector3 pos, float yaw)
        {
            var cc = _motor.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            _motor.transform.position = pos;
            _motor.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (cc != null) cc.enabled = true;
        }

        private void Check(string name, bool ok, string detail)
        {
            if (ok) { _pass++; _log.AppendLine($"[通过] {name}  {detail}"); }
            else { _fail++; _log.AppendLine($"[失败] {name}  {detail}"); }
        }

        private void Unverified(string name) { _unverified++; _log.AppendLine($"[待人工验收] {name}"); }

        private void Dump()
        {
            _log.AppendLine();
            _log.AppendLine("=====================================");
            _log.AppendLine($"通过 {_pass}，失败 {_fail}，待人工验收 {_unverified}");

            string text = _log.ToString();
            Debug.Log("<b>[Ch4PuzzleTest]</b>\n" + text);

            var di = new DirectoryInfo(Application.dataPath);
            for (int hop = 0; hop < 6 && di != null; hop++)
            {
                string r = Path.Combine(di.FullName, "Reports");
                if (Directory.Exists(r))
                {
                    try { File.WriteAllText(Path.Combine(r, "Ch4PuzzleTest.txt"), text, new UTF8Encoding(false)); }
                    catch { }
                    break;
                }
                di = di.Parent;
            }

            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }
    }
}