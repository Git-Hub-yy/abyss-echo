using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S53 章节流程 + 怪物行为运行时诊断。
    ///
    /// ══════════════════ 诊断三个问题 ══════════════════
    ///
    ///   问题 1：Ch3 完成后能否进入 Ch4
    ///     · 检查 ChapterEndingSystem 的触发区是否在终点房间内
    ///     · 检查 GoToNextChapter 的目标章节
    ///     · 检查场景能否加载
    ///
    ///   问题 2：Ch4/Ch5 玩家是否知道要做什么
    ///     · 检查是否有任务提示 UI
    ///     · 检查交互物是否有 prompt
    ///
    ///   问题 3：怪物是否穿墙 / 凭空出现
    ///     · 检查怪物位置是否在可行走区域
    ///     · 检查重生点是否在墙里
    ///     · 检查移动方式是否有碰撞检测
    /// </summary>
    public class ProgressionDiag : MonoBehaviour
    {
        public bool runOnStart = true;
        public int forceChapter = 3;

        private readonly StringBuilder _log = new StringBuilder();
        private static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            // ⚠️ 时序问题：ChapterEndingSystem.Awake() 已经读过章节了。
            //   这里先按 forceChapter 重新设置，再**手动调用一次 ApplyChapterConfig**
            //   让它按正确章节刷新触发区。
            AbyssEcho.LevelGen.ChapterLayout.Current =
                (AbyssEcho.LevelGen.ChapterLayout.Chapter)forceChapter;

            var endingForced = FindFirstObjectByType<AbyssEcho.Narrative.ChapterEndingSystem>();
            if (endingForced != null)
            {
                var applyM = typeof(AbyssEcho.Narrative.ChapterEndingSystem)
                    .GetMethod("ApplyChapterConfig", BF);
                applyM?.Invoke(endingForced, null);
                _log.AppendLine($"[时序修正] 已按 Chapter{forceChapter} 重新应用结局配置");
            }

            yield return new WaitForSeconds(1.2f);

            _log.AppendLine("══════ S53 章节流程 + 怪物诊断 ══════");
            _log.AppendLine($"章节 = {forceChapter}  {AbyssEcho.LevelGen.ChapterLayout.GetChapterName(AbyssEcho.LevelGen.ChapterLayout.Current)}");
            _log.AppendLine($"场景 = {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            _log.AppendLine();

            // ══════ 问题 1：章节结束 ══════
            _log.AppendLine("──── [1] 章节结束系统 ────");
            var ending = FindFirstObjectByType<AbyssEcho.Narrative.ChapterEndingSystem>();
            if (ending == null)
            {
                _log.AppendLine("  ❌ 场景中没有 ChapterEndingSystem");
            }
            else
            {
                var tcField = typeof(AbyssEcho.Narrative.ChapterEndingSystem).GetField("triggerCenter");
                var tsField = typeof(AbyssEcho.Narrative.ChapterEndingSystem).GetField("triggerSize");

                Vector3 tc = (Vector3)tcField.GetValue(ending);
                Vector3 ts = (Vector3)tsField.GetValue(ending);

                _log.AppendLine($"  触发中心 = {tc}");
                _log.AppendLine($"  触发尺寸 = {ts}");
                _log.AppendLine($"  触发范围 X[{tc.x - ts.x * 0.5f:F1}, {tc.x + ts.x * 0.5f:F1}] " +
                                $"Y[{tc.y - ts.y * 0.5f:F1}, {tc.y + ts.y * 0.5f:F1}] " +
                                $"Z[{tc.z - ts.z * 0.5f:F1}, {tc.z + ts.z * 0.5f:F1}]");

                // 终点坐标
                Vector3 endPt = AbyssEcho.LevelGen.ChapterLayout.GetChapterEndPoint();
                _log.AppendLine($"  章节终点坐标 = {endPt}");

                // 触发区是否覆盖终点
                bool covers = Mathf.Abs(endPt.x - tc.x) <= ts.x * 0.5f + 0.5f &&
                              Mathf.Abs(endPt.z - tc.z) <= ts.z * 0.5f + 0.5f;
                _log.AppendLine($"  触发区是否覆盖终点 = {(covers ? "✅ 是" : "❌ 否")}");

                // 终点房间
                var rooms = AbyssEcho.LevelGen.ChapterLayout.GetRooms(AbyssEcho.LevelGen.ChapterLayout.Current);
                var endRoom = rooms.Find(r => endPt.x >= r.MinX - 0.5f && endPt.x <= r.MaxX + 0.5f &&
                                              endPt.z >= r.MinZ - 0.5f && endPt.z <= r.MaxZ + 0.5f);
                if (endRoom != null)
                    _log.AppendLine($"  终点所在房间 = {endRoom.Id} ({endRoom.DisplayName})  " +
                                    $"x[{endRoom.MinX:F1},{endRoom.MaxX:F1}] z[{endRoom.MinZ:F1},{endRoom.MaxZ:F1}]");
                else
                    _log.AppendLine($"  ❌ 终点不在任何房间内！");
            }

            // ══════ 问题 2：任务提示 ══════
            _log.AppendLine();
            _log.AppendLine("──── [2] 任务提示系统 ────");
            var crosshair = FindFirstObjectByType<AbyssEcho.UI.CrosshairUI>();
            var narrative = FindFirstObjectByType<AbyssEcho.UI.NarrativeUI>();
            var titleCard = FindFirstObjectByType<AbyssEcho.UI.ChapterTitleCard>();
            var invUI = FindFirstObjectByType<AbyssEcho.UI.InventoryUI>();

            _log.AppendLine($"  CrosshairUI   = {(crosshair != null ? "✅" : "❌")}");
            _log.AppendLine($"  NarrativeUI   = {(narrative != null ? "✅" : "❌")}");
            _log.AppendLine($"  ChapterTitleCard = {(titleCard != null ? "✅" : "❌")}");
            _log.AppendLine($"  InventoryUI   = {(invUI != null ? "✅" : "❌")}");

            // 交互物
            var detectors = FindObjectsByType<AbyssEcho.Interaction.InteractionDetector>(FindObjectsSortMode.None);
            _log.AppendLine($"  InteractionDetector = {detectors.Length}");

            var keypads = FindObjectsByType<AbyssEcho.Puzzle.KeypadPanel>(FindObjectsSortMode.None);
            var power = FindObjectsByType<AbyssEcho.Puzzle.PowerRestorePanel>(FindObjectsSortMode.None);
            var pump = FindObjectsByType<AbyssEcho.Puzzle.WaterPumpSwitch>(FindObjectsSortMode.None);
            var freq = FindObjectsByType<AbyssEcho.Puzzle.FrequencyConsole>(FindObjectsSortMode.None);
            var altar = FindObjectsByType<AbyssEcho.Puzzle.OfferingAltar>(FindObjectsSortMode.None);
            var pulse = FindObjectsByType<AbyssEcho.Puzzle.PulseSyncPuzzle>(FindObjectsSortMode.None);
            var evac = FindObjectsByType<AbyssEcho.Puzzle.EvacuationConsole>(FindObjectsSortMode.None);
            var doors = FindObjectsByType<AbyssEcho.Interaction.DoorInteractable>(FindObjectsSortMode.None);
            var pickups = FindObjectsByType<AbyssEcho.Interaction.PickupItem>(FindObjectsSortMode.None);

            _log.AppendLine($"  键盘={keypads.Length} 配电箱={power.Length} 水泵={pump.Length} 频率台={freq.Length} " +
                            $"供奉台={altar.Length} 脉搏={pulse.Length} 撤离台={evac.Length}");
            _log.AppendLine($"  门={doors.Length} 拾取物={pickups.Length}");

            _log.AppendLine("  交互物详情：");
            foreach (var k in keypads)
                _log.AppendLine($"    [键盘] {k.gameObject.name} @ {k.transform.position}");
            foreach (var p in power)
                _log.AppendLine($"    [配电箱] {p.gameObject.name} @ {p.transform.position}");
            foreach (var p in pump)
                _log.AppendLine($"    [水泵] {p.gameObject.name} @ {p.transform.position}");
            foreach (var f in freq)
                _log.AppendLine($"    [频率台] {f.gameObject.name} @ {f.transform.position}");
            foreach (var a in altar)
                _log.AppendLine($"    [供奉台] {a.gameObject.name} @ {a.transform.position}");
            foreach (var p in pulse)
                _log.AppendLine($"    [脉搏] {p.gameObject.name} @ {p.transform.position}");
            foreach (var e in evac)
                _log.AppendLine($"    [撤离台] {e.gameObject.name} @ {e.transform.position}");
            foreach (var d in doors)
                _log.AppendLine($"    [门] {d.gameObject.name} @ {d.transform.position} locked={d.locked}");
            foreach (var p in pickups)
                _log.AppendLine($"    [拾取] {p.gameObject.name} {p.displayName} @ {p.transform.position}");

            // ══════ 问题 3：怪物 ══════
            _log.AppendLine();
            _log.AppendLine("──── [3] 怪物行为 ────");
            var mimics = FindObjectsByType<AbyssEcho.Horror.MimicStalker>(FindObjectsSortMode.None);
            _log.AppendLine($"  MimicStalker 数量 = {mimics.Length}");

            var monsterAi = FindObjectsByType<AbyssEcho.Monster.MonsterAI>(FindObjectsSortMode.None);
            _log.AppendLine($"  MonsterAI 数量 = {monsterAi.Length}");

            var motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            Vector3 playerPos = motor != null ? motor.transform.position : Vector3.zero;

            foreach (var m in mimics)
            {
                Vector3 p = m.transform.position;
                float distToPlayer = Vector3.Distance(p, playerPos);

                var col = m.GetComponent<Collider>();
                var cc = m.GetComponent<CharacterController>();
                var rb = m.GetComponent<Rigidbody>();
                var agent = m.GetComponent<UnityEngine.AI.NavMeshAgent>();

                _log.AppendLine($"    {m.gameObject.name} @ {p}");
                _log.AppendLine($"      距玩家={distToPlayer:F1}m  active={m.gameObject.activeInHierarchy}");
                _log.AppendLine($"      Collider={(col != null ? col.GetType().Name : "❌无")} " +
                                $"CharacterController={(cc != null ? "有" : "无")} " +
                                $"Rigidbody={(rb != null ? "有" : "无")} " +
                                $"NavMeshAgent={(agent != null ? "有" : "无")}");

                // 位置是否在墙体内部
                var hits = Physics.OverlapSphere(p + Vector3.up * 0.9f, 0.3f);
                int wallHits = 0;
                foreach (var h in hits)
                {
                    if (h.transform.IsChildOf(m.transform)) continue;
                    if (h.isTrigger) continue;
                    wallHits++;
                }
                _log.AppendLine($"      位置重叠的非触发器碰撞体 = {wallHits} {(wallHits > 0 ? "⚠️ 可能在墙里" : "✅")}");

                // 脚下是否有地面
                bool hasGround = Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, 3f);
                _log.AppendLine($"      脚下有地面 = {(hasGround ? "✅" : "❌ 悬空/在墙里")}");

                // 到玩家的直线是否被墙挡住
                if (distToPlayer > 0.5f)
                {
                    Vector3 dir = (playerPos + Vector3.up * 1f) - (p + Vector3.up * 1f);
                    bool blocked = Physics.Raycast(p + Vector3.up * 1f, dir.normalized,
                                                   out var hh, dir.magnitude,
                                                   ~0, QueryTriggerInteraction.Ignore);
                    bool hitsPlayer = blocked && hh.collider != null &&
                                      (hh.collider.GetComponent<AbyssEcho.Player.PlayerMotor>() != null ||
                                       hh.collider.transform.IsChildOf(playerPos == Vector3.zero ? m.transform : motor.transform));
                    _log.AppendLine($"      与玩家之间有无遮挡 = {(blocked && !hitsPlayer ? "有墙 ⚠️" : "视线通畅")}");
                }
            }

            // 移动方式检查
            _log.AppendLine();
            _log.AppendLine("  移动方式检查：");
            if (mimics.Length > 0)
            {
                var fl = typeof(AbyssEcho.Horror.MimicStalker);
                var moveM = fl.GetMethod("MoveTowardPlayer", BF);
                var pickM = fl.GetMethod("PickRespawnPoint", BF);
                _log.AppendLine($"    MimicStalker.MoveTowardPlayer 存在 = {moveM != null}");
                _log.AppendLine($"    MimicStalker.PickRespawnPoint 存在 = {pickM != null}");
                _log.AppendLine($"    → 这两个方法都直接改 transform.position，不经物理碰撞");

                var m0 = mimics[0];
                var respawnMin = fl.GetField("respawnMinDist", BF)?.GetValue(m0);
                var respawnMax = fl.GetField("respawnMaxDist", BF)?.GetValue(m0);
                var approachSpeed = fl.GetField("approachSpeed", BF)?.GetValue(m0);
                var vanishDist = fl.GetField("vanishDistance", BF)?.GetValue(m0);
                var attackDist = fl.GetField("attackDistance", BF)?.GetValue(m0);
                _log.AppendLine($"    respawnMinDist={respawnMin} respawnMaxDist={respawnMax}");
                _log.AppendLine($"    approachSpeed={approachSpeed} vanishDistance={vanishDist} attackDistance={attackDist}");
            }

            // ══════ 是否可以继续下一章 ══════
            _log.AppendLine();
            _log.AppendLine("──── [4] 下一章跳转 ────");
            var cur = AbyssEcho.LevelGen.ChapterLayout.Current;
            _log.AppendLine($"  当前章节 = {cur}");
            _log.AppendLine($"  是否有下一章 = {(cur != AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter5 ? "有" : "无（末章）")}");

            string note = "（GoToNextChapter 的 switch 只处理 Ch1→Ch2、Ch2→Ch3，" +
                          "Ch3/Ch4 走 default 分支 → next = current → 重新加载自己）";
            _log.AppendLine($"  ⚠️ 代码检查：{note}");

            Dump();
        }

        private void Dump()
        {
            string text = _log.ToString();
            Debug.Log("<b>[ProgressionDiag]</b>\n" + text);

            var di = new DirectoryInfo(Application.dataPath);
            for (int hop = 0; hop < 6 && di != null; hop++)
            {
                string reports = Path.Combine(di.FullName, "Reports");
                if (Directory.Exists(reports))
                {
                    try { File.WriteAllText(Path.Combine(reports, $"ProgressionDiag_Ch{forceChapter}.txt"), text, new UTF8Encoding(false)); }
                    catch { }
                    break;
                }
                di = di.Parent;
            }

            if (!Application.isEditor) Application.Quit(0);
        }
    }
}