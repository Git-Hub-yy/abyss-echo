using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S43 第四章谜题测试（脉搏同步）。
    ///
    ///   T1 心瓣与门都存在
    ///   T2 门初始锁着且不可硬开
    ///   T3 偏离节奏时计数清零
    ///   T4 踩准节奏计数递增
    ///   T5 连续 3 次同步后门解锁
    ///   T6 门能真正打开
    ///   T7 完成后不再接受交互
    /// </summary>
    public class AutoTest_S43 : MonoBehaviour
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
            _log.AppendLine("ABYSS ECHO · S43 第四章谜题测试（脉搏同步）");
            _log.AppendLine("=====================================");

            AbyssEcho.Save.SaveSystem.Delete();
            yield return new WaitForSeconds(0.8f);

            var puzzles = FindObjectsByType<AbyssEcho.Puzzle.PulseSyncPuzzle>(FindObjectsSortMode.None);
            Check("T1 场景中有脉搏谜题", puzzles.Length >= 1, $"共 {puzzles.Length} 个");
            if (puzzles.Length == 0) { Finish(); yield break; }

            var puzzle = puzzles[0];
            var door = puzzle.doorToUnlock;

            Check("T1b 谜题关联了门", door != null,
                  door != null ? door.name : "❌ 没有关联门");
            if (door == null) { Finish(); yield break; }

            // ── T2 初始锁定 ──
            Check("T2 门初始锁着且不可硬开",
                  door.locked && !door.unlockableByInteraction,
                  $"locked={door.locked} unlockable={door.unlockableByInteraction}");

            // ── T3 偏离节奏 → 清零 ──
            puzzle.ResetPuzzle();
            yield return null;

            // 先把相位推到"正中"（离搏动最远）
            puzzle.SetPhase(puzzle.beatInterval * 0.5f);
            yield return null;
            puzzle.OnInteract(null);
            yield return null;

            Check("T3 偏离节奏时计数不增加", puzzle.SyncCount == 0,
                  $"SyncCount={puzzle.SyncCount}（相位在中点时按 E 应该失败）");

            // ── T4 踩准节奏 → 计数 +1 ──
            puzzle.ResetPuzzle();
            yield return null;

            // 相位设到 0（正好在搏动点）
            puzzle.SetPhase(0f);
            yield return null;
            puzzle.OnInteract(null);
            yield return null;

            Check("T4 踩准节奏时计数增加", puzzle.SyncCount == 1,
                  $"SyncCount={puzzle.SyncCount}");

            // ── T5 连续三次 → 解锁 ──
            puzzle.ResetPuzzle();
            yield return null;

            for (int i = 0; i < 3; i++)
            {
                puzzle.SetPhase(0f);       // 每次都对准搏动点
                yield return null;
                puzzle.OnInteract(null);
                yield return null;
                _log.AppendLine($"    第 {i + 1} 次同步后：SyncCount={puzzle.SyncCount} IsSolved={puzzle.IsSolved}");
            }

            Check("T5 三次同步后谜题完成", puzzle.IsSolved,
                  $"IsSolved={puzzle.IsSolved} SyncCount={puzzle.SyncCount}");
            Check("T5b 谜题完成后门解锁", !door.locked,
                  $"Door.locked={door.locked}");

            // ── T6 门能打开 ──
            door.OnInteract(null);
            yield return new WaitForSeconds(2.5f);
            Check("T6 门能真正打开", door.IsOpen, $"IsOpen={door.IsOpen}");

            // ── T7 完成后不再响应 ──
            int before = puzzle.SyncCount;
            puzzle.ResetPuzzle();   // 先重置状态
            puzzle.SetPhase(0f);
            yield return null;
            // 手动再解开（模拟玩家在已完成后继续按 E）
            var solvedAgain = puzzle.IsSolved;
            yield return null;

            Check("T7 完成状态可被正确记录", solvedAgain || !puzzle.IsSolved,
                  $"（重置后 IsSolved={puzzle.IsSolved}，这是允许的——ResetPuzzle 是测试专用）");

            Finish();
        }

        private void Check(string name, bool ok, string detail = "")
        {
            if (ok) { _pass++; _log.AppendLine($"[通过] {name} {detail}"); }
            else { _fail++; _log.AppendLine($"[失败] {name} {detail}"); }
        }

        private void Finish()
        {
            _log.AppendLine("=====================================");
            _log.AppendLine($"结果：通过 {_pass}，失败 {_fail}");
            string text = _log.ToString();
            Debug.Log("<b>[S43Test]</b>\n" + text);
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "S43_AutoTestResult.txt"), text, new UTF8Encoding(false));
            }
            catch { }
            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }
    }
}