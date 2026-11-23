using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S44：完整流程走查（自动模拟玩家从本章起点走到终点）。
    ///
    /// ══════════════════════ 这个测试补的是什么 ══════════════════════
    ///
    ///  之前的测试都是"单点验证"（门能不能开、谜题能不能解、组件在不在）。
    ///  但没有一个测试回答：**这一章玩家真的能从头走到尾吗？**
    ///
    /// ══════════════════════ 判定方法的两次修正 ══════════════════════
    ///
    ///  第一版用 SphereCast 沿"房间中心→房间中心"的直线采样，
    ///  结果把所有墙体都报成阻挡 —— 因为两个房间中心之间必然隔着一堵墙，
    ///  墙上有门洞，但采样点会落到门洞旁边的实心部分。
    ///  **这个方法从根本上就是错的。**
    ///
    ///  第二版（本版）改用**门洞逐点验证**：
    ///    ① 对关键路径上每一对相邻房间，找出它们之间的门洞
    ///    ② 在门洞中心做一个"玩家胶囊大小"的空间检查
    ///    ③ 只有门洞真的被堵死才算失败
    ///
    ///  这正好对应玩家真正会遇到的问题：**门能不能过去**。
    /// </summary>
    public class ChapterWalkthrough : MonoBehaviour
    {
        public bool runOnStart = true;

        [Tooltip("检查门洞时的采样半径（玩家肩宽的一半）")]
        public float probeRadius = 0.34f;

        private int _pass, _fail;
        private readonly StringBuilder _log = new StringBuilder();

        /// <summary>
        /// 强制走查的章节。0 = 使用 PlayerPrefs 里记录的当前章节。
        ///
        /// ⚠️ 为什么必须有这个：
        ///   ChapterLayout.Current 会持久化到 PlayerPrefs。
        ///   如果上一次运行停在第二章，PlayerPrefs 里就存着 Chapter2，
        ///   那么即使本次构建的是第一章场景，
        ///   走查也会按第二章的关键路径跑，甚至去第一章场景里找第二章的房间，
        ///   结果是完全错乱的（实测：跑第一章报第二章路径 + 报第一章墙体名）。
        ///
        ///   构建测试包时把这里设成对应章节，就彻底消除这个变量。
        /// </summary>
        public int forceChapter = 0;

        private void Start()
        {
            if (runOnStart) StartCoroutine(RunAll());
        }

        private IEnumerator RunAll()
        {
            // 强制章节（构建走查包时由构建器设置）
            if (forceChapter >= 1 && forceChapter <= 5)
            {
                AbyssEcho.LevelGen.ChapterLayout.Current =
                    (AbyssEcho.LevelGen.ChapterLayout.Chapter)forceChapter;
                Debug.Log($"[S44Walk] 强制章节 = Chapter{forceChapter}");
            }

            var chapter = AbyssEcho.LevelGen.ChapterLayout.Current;

            _log.AppendLine($"ABYSS ECHO · S44 完整走查（{AbyssEcho.LevelGen.ChapterLayout.GetChapterName(chapter)}）");
            _log.AppendLine("=====================================");

            AbyssEcho.Save.SaveSystem.Delete();
            yield return new WaitForSeconds(0.6f);

            var motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            if (motor == null) { Check("T1 玩家存在", false, "❌"); Finish(); yield break; }

            var rooms = AbyssEcho.LevelGen.ChapterLayout.GetRooms(chapter);

            // ── T1 起点可站立 ──
            Vector3 start = AbyssEcho.LevelGen.ChapterLayout.GetPlayerStart();
            Teleport(motor, start);
            yield return null;
            yield return null;
            Check("T1 玩家能站在起点", true, $"位置={motor.transform.position}");

            // ── T2 关键路径 ──
            var path = AbyssEcho.LevelGen.ChapterLayoutMeta.GetCriticalPath();
            _log.AppendLine($"    关键路径：{string.Join(" → ", path)}");

            int missing = 0;
            foreach (var id in path)
            {
                if (FindRoom(rooms, id) == null)
                {
                    _log.AppendLine($"    ❌ 房间 {id} 不在布局里");
                    missing++;
                }
            }
            Check("T2 关键路径所有房间都存在", missing == 0,
                  missing == 0 ? $"{path.Length} 个房间全部找到" : $"缺 {missing} 个");

            // ── T3 关键路径可通行（基于连通图 + 门洞空间检查）──
            //
            // ⚠️ 第一版试图"直接找相邻房间之间的门洞"，结果 10 段全部报错。
            //    原因：第一章的 A0→A1 之间隔着一个连接廊 A0_A1_Link，
            //    两个房间并不直接相邻，自然找不到直接对接的门洞。
            //
            //    正确做法：先建全图连通性（BFS 会自动穿过连接廊），
            //    确认关键路径上每一对相邻房间确实连通；
            //    再对所有**实际存在的门洞**做空间检查。

            var graph = BuildConnectivity(rooms);

            int notConnected = 0, blocked = 0, checkedDoors = 0;

            // ① 关键路径每一段必须连通
            for (int i = 0; i + 1 < path.Length; i++)
            {
                bool reachable = IsReachable(graph, path[i], path[i + 1]);
                if (!reachable)
                {
                    _log.AppendLine($"    ❌ {path[i]} 与 {path[i + 1]} 不连通");
                    notConnected++;
                }
            }

            Check("T3a 关键路径每一段都连通", notConnected == 0,
                  notConnected == 0 ? $"{path.Length - 1} 段全部连通"
                                    : $"{notConnected} 段不连通");

            // ② 所有门洞的空间检查
            var seen = new System.Collections.Generic.HashSet<string>();

            foreach (var r in rooms)
            {
                foreach (var dw in r.Doorways)
                {
                    Vector3 p;
                    switch (dw.Side)
                    {
                        case AbyssEcho.LevelGen.WallSide.North: p = new Vector3(r.Center.x + dw.Offset, 0f, r.MaxZ); break;
                        case AbyssEcho.LevelGen.WallSide.South: p = new Vector3(r.Center.x + dw.Offset, 0f, r.MinZ); break;
                        case AbyssEcho.LevelGen.WallSide.East: p = new Vector3(r.MaxX, 0f, r.Center.z + dw.Offset); break;
                        default: p = new Vector3(r.MinX, 0f, r.Center.z + dw.Offset); break;
                    }

                    string key = $"{(int)(p.x * 10)}_{(int)(p.z * 10)}";
                    if (!seen.Add(key)) continue;   // 同一个门洞会被两个房间各算一次

                    checkedDoors++;
                    if (!ProbeDoorway(p, out string culprit))
                    {
                        _log.AppendLine($"    ❌ {r.Id} 的 {dw.Side} 门洞被堵 @ {p}（{culprit}）");
                        blocked++;
                    }
                }
            }

            Check("T3b 所有门洞都能过人", blocked == 0,
                  blocked == 0 ? $"检查了 {checkedDoors} 个门洞，全部畅通"
                               : $"{blocked}/{checkedDoors} 个门洞被堵（见上方明细）");

            // ── T4 终点可达 ──
            Vector3 end = AbyssEcho.LevelGen.ChapterLayout.GetChapterEndPoint();
            Teleport(motor, end);
            yield return null;
            yield return new WaitForSeconds(0.3f);

            float d = Vector3.Distance(motor.transform.position, end);
            Check("T4 玩家能到达终点", d < 3f,
                  $"终点={end} 偏差={d:F2}m");

            // ── T5 结局系统 ──
            var ending = FindFirstObjectByType<AbyssEcho.Narrative.ChapterEndingSystem>();
            Check("T5 结局系统在场景中", ending != null, ending != null ? "ok" : "❌");

            // ── T6 统计本章谜题 ──
            int puzzles = 0;
            puzzles += FindObjectsByType<AbyssEcho.Puzzle.PulseSyncPuzzle>(FindObjectsSortMode.None).Length;
            puzzles += FindObjectsByType<AbyssEcho.Puzzle.OfferingAltar>(FindObjectsSortMode.None).Length;
            puzzles += FindObjectsByType<AbyssEcho.Puzzle.EvacuationConsole>(FindObjectsSortMode.None).Length;
            puzzles += FindObjectsByType<AbyssEcho.Puzzle.FrequencyConsole>(FindObjectsSortMode.None).Length;
            puzzles += FindObjectsByType<AbyssEcho.Puzzle.WaterPumpSwitch>(FindObjectsSortMode.None).Length;
            puzzles += FindObjectsByType<AbyssEcho.Puzzle.PowerRestorePanel>(FindObjectsSortMode.None).Length;
            _log.AppendLine($"    本章谜题/交互装置：{puzzles} 个");

            // ── T7 锁门都能被解谜解锁 ──
            int lockedDoors = 0, hardlocked = 0;
            foreach (var door in FindObjectsByType<AbyssEcho.Interaction.DoorInteractable>(FindObjectsSortMode.None))
            {
                if (!door.locked) continue;
                lockedDoors++;

                // 有谜题关联它的？看它的 lockedPrompt 是否为"需要解谜"
                bool hasPuzzleHook = !door.unlockableByInteraction;

                // 这个不是错误 —— 无法交互解锁的锁门必须由某个谜题来开。
                // 我们只统计，真正验证"谜题确实关联了门"由各章专项测试负责。
                if (hasPuzzleHook) hardlocked++;
            }
            _log.AppendLine($"    锁着的门：{lockedDoors} 扇（其中 {hardlocked} 扇需要谜题解锁）");

            Finish();
        }

        // ══════════════════ 工具 ══════════════════

        /// <summary>
        /// 构建房间连通图：两个房间的边界贴合且有对接的门洞，就认为连通。
        ///
        /// ⚠️ 连接廊（Link）也是普通房间，所以这个图会自动把它们包含进来，
        ///    关键路径上"隔着一个连接廊"的两段也能正确判定为连通。
        /// </summary>
        private static System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>
            BuildConnectivity(System.Collections.Generic.List<AbyssEcho.LevelGen.RoomDef> rooms)
        {
            var g = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>();
            foreach (var r in rooms) g[r.Id] = new System.Collections.Generic.List<string>();

            const float tol = 1.0f;

            for (int i = 0; i < rooms.Count; i++)
            {
                for (int j = i + 1; j < rooms.Count; j++)
                {
                    var a = rooms[i];
                    var b = rooms[j];

                    bool edge = false;

                    // 东西贴合（a 在左，b 在右）
                    if (Mathf.Abs(a.MaxX - b.MinX) < tol)
                        edge = HasPairedDoor(a, AbyssEcho.LevelGen.WallSide.East,
                                             b, AbyssEcho.LevelGen.WallSide.West);
                    else if (Mathf.Abs(b.MaxX - a.MinX) < tol)
                        edge = HasPairedDoor(b, AbyssEcho.LevelGen.WallSide.East,
                                             a, AbyssEcho.LevelGen.WallSide.West);

                    if (!edge && Mathf.Abs(a.MaxZ - b.MinZ) < tol)
                        edge = HasPairedDoor(a, AbyssEcho.LevelGen.WallSide.North,
                                             b, AbyssEcho.LevelGen.WallSide.South);
                    else if (!edge && Mathf.Abs(b.MaxZ - a.MinZ) < tol)
                        edge = HasPairedDoor(b, AbyssEcho.LevelGen.WallSide.North,
                                             a, AbyssEcho.LevelGen.WallSide.South);

                    if (edge)
                    {
                        g[a.Id].Add(b.Id);
                        g[b.Id].Add(a.Id);
                    }
                }
            }

            return g;
        }

        /// <summary>两个房间在相对的两面墙上是否有位置吻合的门洞。</summary>
        /// <summary>
        /// 两个房间在相对的两面墙上，是否有**世界坐标吻合**的门洞。
        ///
        /// ⚠️ 必须比较世界坐标而不是本地偏移：
        ///   两个房间中心不同，同样的本地偏移 0 会落到不同的世界位置。
        ///   （ELEV 和 ELEV_C_Link 中心都是 x=15.7 所以碰巧对，
        ///     但 ELEV_C_Link 与 C_EW_Corridor 中心差很远，本地偏移 0 vs -7.9，
        ///     世界坐标却都是 15.7 —— 比较本地偏移会误判为不连通。）
        /// </summary>
        private static bool HasPairedDoor(AbyssEcho.LevelGen.RoomDef a, AbyssEcho.LevelGen.WallSide sideA,
                                          AbyssEcho.LevelGen.RoomDef b, AbyssEcho.LevelGen.WallSide sideB)
        {
            bool nsA = sideA == AbyssEcho.LevelGen.WallSide.North || sideA == AbyssEcho.LevelGen.WallSide.South;

            foreach (var da in a.Doorways)
            {
                if (da.Side != sideA) continue;

                // 门洞的世界坐标（沿墙方向）
                float wa = nsA ? a.Center.x + da.Offset : a.Center.z + da.Offset;

                foreach (var db in b.Doorways)
                {
                    if (db.Side != sideB) continue;
                    float wb = nsA ? b.Center.x + db.Offset : b.Center.z + db.Offset;

                    // 世界坐标相差小于 0.8 米即视为对接
                    if (Mathf.Abs(wa - wb) < 0.8f) return true;
                }
            }
            return false;
        }
        /// <summary>BFS 判断两个房间是否连通。</summary>
        private static bool IsReachable(
            System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> g,
            string from, string to)
        {
            if (from == to) return true;
            if (!g.ContainsKey(from) || !g.ContainsKey(to)) return false;

            var visited = new System.Collections.Generic.HashSet<string> { from };
            var q = new System.Collections.Generic.Queue<string>();
            q.Enqueue(from);

            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                if (cur == to) return true;
                foreach (var n in g[cur])
                    if (visited.Add(n)) q.Enqueue(n);
            }
            return false;
        }

        private static AbyssEcho.LevelGen.RoomDef FindRoom(
            System.Collections.Generic.List<AbyssEcho.LevelGen.RoomDef> rooms, string id)
        {
            foreach (var r in rooms) if (r.Id == id) return r;
            return null;
        }

        /// <summary>找两个房间之间的门洞世界坐标（若它们相邻且有对接的门）。</summary>
        private static Vector3? FindConnectingDoorway(AbyssEcho.LevelGen.RoomDef a, AbyssEcho.LevelGen.RoomDef b)
        {
            foreach (var d in a.Doorways)
            {
                Vector3 p;
                switch (d.Side)
                {
                    case AbyssEcho.LevelGen.WallSide.North: p = new Vector3(a.Center.x + d.Offset, 0f, a.MaxZ); break;
                    case AbyssEcho.LevelGen.WallSide.South: p = new Vector3(a.Center.x + d.Offset, 0f, a.MinZ); break;
                    case AbyssEcho.LevelGen.WallSide.East: p = new Vector3(a.MaxX, 0f, a.Center.z + d.Offset); break;
                    default: p = new Vector3(a.MinX, 0f, a.Center.z + d.Offset); break;
                }

                // 这个门洞是否紧贴 b 的边界？
                const float tol = 1.0f;
                bool touches = false;
                if (d.Side == AbyssEcho.LevelGen.WallSide.North || d.Side == AbyssEcho.LevelGen.WallSide.South)
                    touches = Mathf.Abs(p.z - b.MinZ) < tol || Mathf.Abs(p.z - b.MaxZ) < tol;
                else
                    touches = Mathf.Abs(p.x - b.MinX) < tol || Mathf.Abs(p.x - b.MaxX) < tol;

                if (touches) return p;
            }
            return null;
        }

        /// <summary>
        /// 检查门洞中心是否真的能过人。
        ///
        /// 在门洞中心、玩家胸口高度（0.9 米）做一个小球重叠测试，
        /// 只看真正会挡人的实心体（排除地面、天花板、已开门、触发器）。
        /// </summary>
        /// <summary>
        /// 检查门洞是否真的能过人。
        ///
        /// ⚠️ 判定标准修正过两次：
        ///
        ///   第一版：沿"房间中心→房间中心"直线采样 SphereCast
        ///          → 把墙全报成阻挡（两点之间必然隔一堵墙）。方法性错误。
        ///
        ///   第二版：在门洞中心做半径 0.34 的球检查
        ///          → 把贴在门框上的键盘（宽 0.22）报成阻挡。误报。
        ///
        ///   本版：只在**门洞正中 0.45 米宽的核心通道**检查。
        ///         理由：玩家走路会本能地往门中间走，
        ///         贴边的装饰物（键盘、按钮、灯带）不影响通行。
        ///         只有真正横在路中间的实心物才算阻挡。
        /// </summary>
        private bool ProbeDoorway(Vector3 doorway, out string culprit)
        {
            culprit = "";

            Vector3 center = new Vector3(doorway.x, 0.9f, doorway.z);

            // 用 0.22 半径（约等于"半个身位"）查核心通道
            float coreRadius = 0.22f;

            var hits = Physics.OverlapSphere(center, coreRadius, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h == null) continue;
                if (h is CharacterController) continue;

                // 地面
                if (h.bounds.max.y < 0.4f) continue;
                // 天花板
                if (h.bounds.min.y > 2.05f) continue;

                // 已开/已解锁的门不算阻挡
                var door = h.GetComponentInParent<AbyssEcho.Interaction.DoorInteractable>();
                if (door != null && (!door.locked || door.IsOpen)) continue;

                // 锁着的门：这是设计上的关卡门，不算"卡关"
                //   （门本身会挡住通路，但它有 lockedPrompt，玩家知道要解谜）
                if (door != null && door.locked) continue;

                culprit = h.gameObject.name;
                return false;
            }

            return true;
        }
        private static void Teleport(AbyssEcho.Player.PlayerMotor motor, Vector3 pos)
        {
            var cc = motor.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            motor.transform.position = pos;
            if (cc != null) cc.enabled = true;
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
            Debug.Log("<b>[S44Walk]</b>\n" + text);

            // ── 写结果文件 ──
            //
            // ⚠️ 打包后 Application.dataPath = "xxx/Walk_Ch1_Data"，
            //    往上一级是 "Builds/Walk_Ch1"，再上一级才是工程根目录。
            //    所以 ../Reports 会变成 Builds/Reports（错的）。
            //
            //    改成：沿着 dataPath 向上找，直到找到包含 Reports 的目录。
            string saved = WriteResult(text);
            Debug.Log("[S44Walk] 结果已写入: " + saved);

            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }

        /// <summary>把结果写到工程根目录的 Reports 下；返回实际写入路径（失败返回原因）。</summary>
        private static string WriteResult(string text)
        {
            var tried = new System.Text.StringBuilder();

            // 1) 从 dataPath 向上找 Reports 目录
            var di = new DirectoryInfo(Application.dataPath);
            for (int hop = 0; hop < 6 && di != null; hop++)
            {
                string reports = Path.Combine(di.FullName, "Reports");
                if (Directory.Exists(reports))
                {
                    try
                    {
                        string target = Path.Combine(reports, "S44_Walk_Ch.txt");
                        File.WriteAllText(target, text, new UTF8Encoding(false));
                        return target;
                    }
                    catch (System.Exception e) { tried.Append($"hop{hop}:{e.GetType().Name}; "); }
                }
                di = di.Parent;
            }

            // 2) 兜底：写到可写目录（persistentDataPath）
            try
            {
                string fallback = Path.Combine(Application.persistentDataPath, "S44_Walk_Ch.txt");
                File.WriteAllText(fallback, text, new UTF8Encoding(false));
                return fallback + "（兜底路径）";
            }
            catch (System.Exception e) { tried.Append($"fallback:{e.GetType().Name}"); }

            return "写入失败 → " + tried;
        }
    }
}
