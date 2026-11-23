using System.Collections;
using AbyssEcho.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbyssEcho.Narrative
{
    /// <summary>
    /// 第一章结局系统：玩家走到终点触发收尾演出。
    ///
    /// 演出流程（共约 12 秒，可按 Esc 跳过）：
    ///   1. 冻结玩家输入
    ///   2. 画面渐暗至全黑（2.5 秒）
    ///   3. 逐段显示结局文字（每段停留 2.5 秒）
    ///   4. 显示「第一章 · 完」+「未完待续」
    ///   5. 底部提供「返回」按钮（重载场景）
    ///
    /// 结局文案设计：
    ///   不解释怪物是什么、不给出全部答案——
    ///   第一章的任务是建立"这里不对劲"的感受和继续往下的理由。
    /// </summary>
    public class ChapterEndingSystem : MonoBehaviour
    {
        [Header("触发")]
        [Tooltip("终点触发区域中心")]
        public Vector3 triggerCenter = new Vector3(0f, 1f, 39f);

        [Tooltip("触发区域大小")]
        public Vector3 triggerSize = new Vector3(5f, 3f, 4f);

        [Tooltip("玩家进入后延迟多久触发（秒）")]
        public float triggerDelay = 0.8f;

        [Header("演出节奏")]
        public float fadeOutDuration = 2.5f;
        public float lineHoldDuration = 2.5f;
        public float lineFadeDuration = 0.8f;

        [Header("文案")]
        public string[] endingLines =
        {
            "你按下了广播键。",
            "频道里只有海水的压力声。",
            "然后有人回话了——",
            "「这里已经十九年了。」",
            "「你为什么才来？」"
        };

        public string chapterTitle = "第一章 · 深潜";
        public string chapterFooter = "未完待续";
        public string restartHint = "按 R 返回起点";

        [Header("样式")]
        public Color textColor = new Color(0.88f, 0.92f, 0.96f);
        public int lineFontSize = 26;
        public int titleFontSize = 40;
        public int footerFontSize = 20;

        public bool IsPlaying { get; private set; }
        public bool IsFinished { get; private set; }

        private float _fadeAlpha;
        private float _lineAlpha;
        private int _lineIndex;
        private float _timer;
        private bool _inputFrozen;

        private PlayerMotor _motor;
        private PlayerLook _look;
        private PlayerInputReader _input;
        private AbyssEcho.Interaction.InteractionDetector _detector;
        private AbyssEcho.UI.CrosshairUI _crosshair;
        private AbyssEcho.UI.InventoryUI _inventory;
        private AbyssEcho.UI.PauseMenuUI _pause;
        private AbyssEcho.Horror.FearVisuals _fearVisuals;

        private GUIStyle _lineStyle, _titleStyle, _footerStyle;
        private Texture2D _white;

        private enum Phase { Idle, Waiting, FadeOut, Lines, Finale, Done }
        private Phase _phase = Phase.Idle;
        private bool _triggered;
        private int _lineShownCount;

        private void Awake()
        {
            _white = Texture2D.whiteTexture;
            _motor = FindFirstObjectByType<PlayerMotor>();
            _look = FindFirstObjectByType<PlayerLook>();
            _input = FindFirstObjectByType<PlayerInputReader>();
            _detector = FindFirstObjectByType<AbyssEcho.Interaction.InteractionDetector>();
            _crosshair = FindFirstObjectByType<AbyssEcho.UI.CrosshairUI>();
            _inventory = FindFirstObjectByType<AbyssEcho.UI.InventoryUI>();
            _pause = FindFirstObjectByType<AbyssEcho.UI.PauseMenuUI>();
            _fearVisuals = FindFirstObjectByType<AbyssEcho.Horror.FearVisuals>();

            EnsureChapterMatchesScene();
            ApplyChapterConfig();
        }

        /// <summary>
        /// S77：**根据当前场景名校正章节**，避免用错触发点。
        ///
        /// ══════════════════════ 修复的问题 ══════════════════════
        ///
        ///   玩家反馈："第五章还没走到最后一间房间（H7 出口舱）就开始播结局。"
        ///
        ///   实测现象：结局文字在 H6 升降井（Z≈39）就滚动播放。
        ///
        ///   根因：
        ///     ChapterLayout.Current 从 PlayerPrefs["game.currentChapter"] 读取。
        ///     在 Unity 编辑器里直接打开第五章场景按 Play 时，
        ///     PlayerPrefs 里存的很可能是 3（第三章）或 4（第四章）。
        ///
        ///     于是 ApplyChapterConfig() 走了**第四章分支**：
        ///         triggerCenter = new Vector3(0f, 0.9f, 39f);   ← 第四章心腔门后通道
        ///         triggerSize   = new Vector3(5f, 4f, 4f);
        ///
        ///     而第五章的 H6 升降井正好在 Z[36, 44]，Z=39 落在其中
        ///     → 玩家一进 H6 就触发结局。
        ///
        ///   （独立 exe 里章节值是对的，所以之前的走查日志显示 Z=54 正常。
        ///     只有编辑器 Play 会暴露这个问题。）
        ///
        /// ══════════════════════ 修复方式 ══════════════════════
        ///
        ///   场景名是章节的**唯一可靠来源**：
        ///     Level_Chapter1_Graybox → Chapter1
        ///     Level_Chapter5_Graybox → Chapter5
        ///
        ///   如果场景名能解析出章节，就以场景为准，覆盖 PlayerPrefs 的值。
        ///   解析不出来时（例如测试场景）保持原值不动。
        /// </summary>
        private void EnsureChapterMatchesScene()
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(sceneName)) return;

            int chapterNum = ParseChapterFromSceneName(sceneName);
            if (chapterNum < 1 || chapterNum > 5) return;

            var fromScene = (AbyssEcho.LevelGen.ChapterLayout.Chapter)chapterNum;
            var fromPrefs = AbyssEcho.LevelGen.ChapterLayout.Current;

            if (fromScene != fromPrefs)
            {
                Debug.LogWarning($"[Ending] 场景 '{sceneName}' 属于 Chapter{chapterNum}，" +
                                 $"但 ChapterLayout.Current = {fromPrefs}（可能来自 PlayerPrefs）。" +
                                 $"已按场景校正为 {fromScene}。");
                AbyssEcho.LevelGen.ChapterLayout.Current = fromScene;
            }
        }

        /// <summary>
        /// 从场景名解析章节号。
        ///
        /// 支持两种命名：
        ///   · Level_Chapter5_Graybox   （正式场景）
        ///   · Walk_Ch5                 （走查临时副本，见 BuildToolsWalkthrough）
        ///
        /// 返回 0 表示无法识别。
        /// </summary>
        private static int ParseChapterFromSceneName(string sceneName)
        {
            // ① 正式场景：Level_ChapterN_Graybox
            int idx = sceneName.IndexOf("Chapter", System.StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                int p = idx + "Chapter".Length;
                if (p < sceneName.Length && sceneName[p] >= '1' && sceneName[p] <= '9')
                    return sceneName[p] - '0';
            }

            // ② 走查临时场景：Walk_ChN
            int chIdx = sceneName.IndexOf("_Ch", System.StringComparison.OrdinalIgnoreCase);
            if (chIdx >= 0)
            {
                int p = chIdx + "_Ch".Length;
                if (p < sceneName.Length && sceneName[p] >= '1' && sceneName[p] <= '9')
                    return sceneName[p] - '0';
            }

            return 0;
        }

        /// <summary>
        /// 按当前章节自动装载结局文案与触发点。
        ///
        /// 为什么放在 Awake 而不是 Inspector 配置：
        ///   同一个场景要服务多个章节，Inspector 里只能存一份值。
        ///   改成运行时按章节读取，切换章节时无需重新装配场景。
        /// </summary>
        /// <summary>
        /// 解析当前应该走哪个结局分支。
        ///
        /// 只有第三章有分支（A/B/C），其他章节返回空字符串。
        /// </summary>
        private string ResolveEndingId(AbyssEcho.LevelGen.ChapterLayout.Chapter chapter)
        {
            // ── 第五章（S43）：撤离协议三选项 ──
            if (chapter == AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter5)
            {
                var console = FindFirstObjectByType<AbyssEcho.Puzzle.EvacuationConsole>();
                if (console == null)
                {
                    Debug.Log("[Ending] 第五章未找到撤离控制台，使用默认结局 B");
                    return "B";
                }

                if (string.IsNullOrEmpty(console.ChosenEnding))
                {
                    Debug.Log("[Ending] 第五章玩家未做选择，使用默认结局 B");
                    return "B";
                }

                return console.ChosenEnding;
            }

            // ── 第三章（S35）：供奉三结局 ──
            // ── 第三章（S65）：新增灯座 → 结局 D「照亮」 ──
            //
            //   第三章场景里本来就有两个空的长明灯座，
            //   而玩家能在 F4 捡到「灯泡」—— 之前捡了没有任何用途。
            //
            //   现在：把灯泡装到灯座上 → 点亮 → 结局 D。
            //
            //   优先级：灯座和供奉台**谁先被激活就算谁**。
            //     灯座点亮 → "D"
            //     否则看供奉台 → A / B / C
            if (chapter != AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter3)
                return "";

            // 先看灯座（灯泡 → 结局 D）
            var sockets = FindObjectsByType<AbyssEcho.Puzzle.LampSocket>(FindObjectsSortMode.None);
            foreach (var s in sockets)
            {
                if (s != null && s.IsLit)
                {
                    Debug.Log("[Ending] 第三章：长明灯已点亮 → 结局 D（照亮）");
                    return "D";
                }
            }

            var altar = FindFirstObjectByType<AbyssEcho.Puzzle.OfferingAltar>();
            if (altar == null)
            {
                Debug.Log("[Ending] 第三章未找到供奉台，使用默认结局 B");
                return "B";
            }

            return altar.GetEndingId();
        }

        /// <summary>
        /// 读取并应用结局文案（章节标题 / 结语 / 逐行文本）。
        ///
        /// ══════════════════ S64：为什么单独抽出来 ══════════════════
        ///
        ///   玩家实测反馈："我明明放了保险丝，结局第一句却是『你什么也没有放』"
        ///
        ///   根因：
        ///     ApplyChapterConfig() 只在 Awake() 里调用一次，
        ///     而 Awake() 是**场景刚加载的那一刻**执行的。
        ///     第三章的结局由 OfferingAltar.GetEndingId() 决定，
        ///     它读的是 HasOffered —— 那一刻玩家还没进祭室，必然是 false，
        ///     于是永远返回 "B"（离开），endingLines 被定死成 B 的文案。
        ///     玩家之后放了保险丝，GetEndingId() 变成 "C"，
        ///     但没有任何代码重新读取，BeginEnding() 播放的仍是 B。
        ///
        ///   修复：
        ///     把「读文案」抽成本方法，
        ///     在 BeginEnding()（真正开始播结局时）再调用一次，
        ///     这样拿到的一定是玩家**最终选择**对应的文案。
        ///
        ///   为什么安全：
        ///     只有第三章有分支；其他四章 ResolveEndingId 返回空字符串，
        ///     ChapterEndingLines.Get 每次返回同一个定义，重复调用结果完全相同。
        /// </summary>
        private void ApplyEndingText()
        {
            var chapter = AbyssEcho.LevelGen.ChapterLayout.Current;

            // S35：第三章有 A/B/C 三个结局，取决于玩家在 F9 祭室供奉了什么。
            //   其他章节没有分支，endingId 传空字符串即可。
            string endingId = ResolveEndingId(chapter);
            var def = ChapterEndingLines.Get(chapter, endingId);

            if (def != null)
            {
                endingLines = def.Lines;
                chapterTitle = def.ChapterTitle;
                chapterFooter = def.Footer;
            }

            if (!string.IsNullOrEmpty(endingId))
                Debug.Log($"[Ending] {chapter} 结局分支 = {endingId}");
        }

        private void ApplyChapterConfig()
        {
            var chapter = AbyssEcho.LevelGen.ChapterLayout.Current;

            // S64：文案部分抽到 ApplyEndingText()，
            //   Awake 时先按当前状态读一次（保持原有行为），
            //   BeginEnding 时再读一次拿到玩家最终选择。
            ApplyEndingText();

            // 触发点跟随章节终点
            //
            // ⚠️ S23 修复（玩家实测"还没进闸门房间就到结局了"）：
            //
            //   原逻辑直接用 ChapterEndPoint = (0, 0.05, 39)。
            //   但 z=39 在 **C1 主厅内部**（C1 范围 z[30,40]），
            //   而触发区尺寸是 (5,3,4)，覆盖 z ∈ [37, 41] ——
            //   于是一半落在主厅里，玩家走到 C1 北侧就提前触发结局，
            //   根本没进 C1_D_Gate 那个房间。
            //
            //   修复：
            //     ① 把触发点移到"终点房间"内部（z 取房间中心）
            //     ② 触发区缩到房间尺寸的 60%，确保玩家必须真的走进去
            triggerCenter = AbyssEcho.LevelGen.ChapterLayout.GetChapterEndPoint() + Vector3.up * 0.9f;

            // 按章节找出终点对应房间，把触发点推进房间内部
            var rooms = AbyssEcho.LevelGen.ChapterLayout.GetRooms(chapter);

            // ══════════════════ S53 修复：终点房间名写死 ══════════════════
            //
            //   原代码：string endRoomId = (chapter == Chapter2) ? "E4" : "C1_D_Gate";
            //   → 只有 Ch1 用 C1_D_Gate、Ch2 用 E4，**Ch3/Ch4/Ch5 也落到 C1_D_Gate**，
            //     但这三章根本没有这个房间，于是找不到终点房间，
            //     触发区停留在硬编码的 (0, 0.9, 42) ——
            //
            //   实测各章实际终点房间：
            //     Ch3 → F7  镜沼     z[53, 67]  终点 z=60
            //     Ch4 → G7  核心腔   z[7, 25]   终点 z=16
            //     Ch5 → H7  出口舱   z[48, 60]  终点 z=54
            //
            //   而写死的触发区 (0,0.9,42) 作用域 Z[40.8, 43.2]，
            //   与上面三个终点**一个都没对上**，玩家走到终点也不会触发结局。
            string endRoomId;
            switch (chapter)
            {
                case AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter1: endRoomId = "C1_D_Gate"; break;
                case AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter2: endRoomId = "E4"; break;
                case AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter3: endRoomId = "F7"; break;
                case AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter4: endRoomId = "G7"; break;
                case AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter5: endRoomId = "H7"; break;
                default: endRoomId = "C1_D_Gate"; break;
            }

            var endRoom = rooms.Find(r => r.Id == endRoomId);
            if (endRoom == null)
            {
                // 找不到指定房间时，按终点坐标反查（兜底，避免再次落到错误位置）
                Vector3 endPt = AbyssEcho.LevelGen.ChapterLayout.GetChapterEndPoint();
                endRoom = rooms.Find(r => endPt.x >= r.MinX - 0.5f && endPt.x <= r.MaxX + 0.5f &&
                                          endPt.z >= r.MinZ - 0.5f && endPt.z <= r.MaxZ + 0.5f);
                if (endRoom != null)
                    Debug.LogWarning($"[Ending] 找不到房间 {endRoomId}，改按终点坐标反查到 {endRoom.Id}");
            }
            if (endRoom != null)
            {
                // ══════════════════ S58 修复：Ch4 触发区落在心瓣上 ══════════════════
                //
                //   问题：
                //     S53 把 Ch4 的 endRoomId 从写死的 "C1_D_Gate" 改成 "G7"，
                //     于是这里用「房间中心」作为触发点。
                //     但 G7 是 x[-10,10] z[7,25] 的整个核心腔，
                //     房间中心 z=16 —— **正好是 HeartValve 的位置**。
                //
                //   后果：
                //     玩家走进 G7 走到心瓣（本来就必须做的事）就立刻触发结局，
                //     不需要解谜、不需要开 Door_HeartGate，永远看不到门后通道。
                //
                //   修复：
                //     Ch4 单独处理，把触发区放到**门后通道内**（z=39，与通道 z[25.2,42.2] 对齐）。
                //     其他四章的 endRoomId 都是「专门的终点小房间」或「终点即房间」，保持原逻辑。
                bool useCustomTrigger =
                    chapter == AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter4;

                if (useCustomTrigger)
                {
                    // 门后通道 z[25.2, 42.2]，把触发区放在通道靠北段
                    triggerCenter = new Vector3(0f, 0.9f, 39f);
                    triggerSize = new Vector3(5f, 4f, 4f);
                    Debug.Log("[Ending] Ch4 使用门后通道触发点 z=39（避免落在心瓣 z=16 上）");
                }
                else
                {
                    // 触发点放在房间中心（保证一定在房间内部）
                    triggerCenter = new Vector3(endRoom.Center.x, endRoom.FloorY + 0.9f, endRoom.Center.z);

                    // 触发区不超过房间尺寸的 60%
                    triggerSize = new Vector3(
                        Mathf.Min(triggerSize.x, endRoom.SizeX * 0.6f),
                        triggerSize.y,
                        Mathf.Min(triggerSize.z, endRoom.SizeZ * 0.6f));

                    // ══════════════════ S78：第五章触发区重新定位 ══════════════════
                    //
                    //   玩家反馈："第五章貌似只有一个结局，我记得可以选择"
                    //
                    //   根因链：
                    //     ① 原触发区 Z[52, 56]，撤离控制台在 Z=57.8
                    //     ② 玩家走到 Z=52 就触发结局
                    //     ③ 结局一开始就禁用 _detector（交互检测）
                    //     ④ 玩家再也无法与控制台交互 → 只能拿默认结局 B
                    //     ⑤ 三个结局（A独行/B默认/C封门）实际只会出现 B
                    //
                    //   修复：触发区移到控制台之后
                    //     · 控制台前（Z < 57.8）留出完整交互空间
                    //     · 触发点 Z=59（控制台后方，靠近出口门 Z=59.6）
                    //     · 玩家必须先走到控制台做选择，才能走到出口触发结局
                    //
                    //   这样三个结局才真正可选。
                    if (chapter == AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter5)
                    {
                        // 出口门在 Z=59.6，触发区放在它前方
                        float exitZ = 59f;
                        triggerCenter = new Vector3(endRoom.Center.x, endRoom.FloorY + 0.9f, exitZ);
                        triggerSize = new Vector3(
                            endRoom.SizeX * 0.98f,   // 与 H7 同宽，不留缝隙
                            4f,
                            1.6f);                   // Z[58.2, 59.8]，刚好覆盖出口门

                        Debug.Log($"[Ending] Ch5 触发区移到出口门处：中心 z={exitZ:F1}，" +
                                  $"尺寸={triggerSize}，控制台(z=57.8)在触发区之前");
                    }
                }

                Debug.Log($"[Ending] 终点房间 {endRoomId}：z[{endRoom.MinZ:F1},{endRoom.MaxZ:F1}] " +
                          $"→ 触发点 z={triggerCenter.z:F1}，触发区尺寸={triggerSize}");
            }
            else
            {
                Debug.LogWarning($"[Ending] 找不到终点房间 {endRoomId}，沿用默认触发点");
            }

            string chapterName = AbyssEcho.LevelGen.ChapterLayout.GetChapterName(chapter);
            int lineCount = endingLines != null ? endingLines.Length : 0;
            Debug.Log("[Ending] 已装载 " + chapterName + " 结局，触发点=" + triggerCenter +
                      " 共 " + lineCount + " 句");
        }

        private void Update()
        {
            if (IsFinished)
            {
                if (Input.GetKeyDown(KeyCode.R)) RestartScene();
                return;
            }

            if (!_triggered && CheckPlayerInZone())
            {
                _triggered = true;
                _phase = Phase.Waiting;
                _timer = triggerDelay;
                Debug.Log($"[Ending] 玩家进入终点区域，位置={_motor.transform.position}，触发区={triggerCenter} 尺寸={triggerSize}");
            }

            // ══════════════════ S81：跳过结局 ══════════════════
            //
            //   需求：增加一个「跳过结局」的功能。
            //
            //   设计：
            //     · 结局正在播放时（Lines / FadeOut / Finale 阶段），按 **空格** 跳过
            //     · 跳过 = 直接终局（同结局正常播完的效果）
            //     · 不跳过「准备阶段」（Waiting = 触发后的 0.8 秒延迟），
            //       避免误触
            //
            //   为什么用空格：
            //     · 空格在其他面板里已经是"确认/继续"，玩家有直觉
            //     · 不与 R（重开）、Esc（暂停）冲突
            if ((_phase == Phase.Lines || _phase == Phase.FadeOut) &&
                Input.GetKeyDown(KeyCode.Space))
            {
                Debug.Log("[Ending] 玩家按空格跳过结局");
                SkipToFinale();
                return;
            }

            switch (_phase)
            {
                case Phase.Waiting:
                    _timer -= Time.deltaTime;
                    if (_timer <= 0f) BeginEnding();
                    break;

                case Phase.FadeOut:
                    IsPlaying = true;
                    _fadeAlpha = Mathf.MoveTowards(_fadeAlpha, 1f, Time.deltaTime / fadeOutDuration);
                    if (_fadeAlpha >= 1f) { _phase = Phase.Lines; _timer = 0f; _lineIndex = 0; _lineShownCount = 0; }
                    break;

                case Phase.Lines:
                    _timer += Time.deltaTime;

                    // 淡入当前句
                    if (_lineAlpha < 1f && _timer < lineFadeDuration)
                        _lineAlpha = Mathf.Clamp01(_timer / lineFadeDuration);

                    // 到时间进入下一句
                    if (_timer >= lineHoldDuration + lineFadeDuration)
                    {
                        _lineIndex++;
                        _lineShownCount = _lineIndex;
                        _timer = 0f;
                        _lineAlpha = 0f;

                        if (_lineIndex >= endingLines.Length)
                        {
                            _phase = Phase.Finale;
                            _timer = 0f;
                        }
                    }
                    break;

                case Phase.Finale:
                    _timer += Time.deltaTime;
                    if (_timer > 2.0f && !IsFinished)
                    {
                        IsFinished = true;
                        IsPlaying = false;
                        Debug.Log("[Ending] 结局播放完毕");
                    }
                    break;
            }
        }

        private bool CheckPlayerInZone()
        {
            if (_motor == null) return false;

            Vector3 p = _motor.transform.position;
            Vector3 half = triggerSize * 0.5f;

            return Mathf.Abs(p.x - triggerCenter.x) <= half.x
                && Mathf.Abs(p.y - triggerCenter.y) <= half.y
                && Mathf.Abs(p.z - triggerCenter.z) <= half.z;
        }

        /// <summary>
        /// S81：**跳过结局** —— 直接跳到终局画面。
        ///
        /// 效果等同于结局文字正常播完：
        ///   · 剩余文字不再显示
        ///   · 直接进入 Phase.Finale（终局黑屏 + 标题）
        ///   · 2 秒后 IsFinished = true（可通过 R 重开）
        ///
        /// 为什么不是"立刻结束"：
        ///   如果直接 IsFinished = true，玩家会跳过终局画面的标题和氛围，
        ///   失去"这一章结束了"的心理落点。
        ///   跳到 Finale 既省时间，又保留结局的分量。
        /// </summary>
        private void SkipToFinale()
        {
            // 文字淡出：让画面平滑过渡，不要瞬间闪
            _lineAlpha = 0f;
            _lineIndex = endingLines != null ? endingLines.Length : 0;
            _lineShownCount = _lineIndex;

            _phase = Phase.Finale;
            _timer = 0f;

            // 终局时黑幕必须完全不透明（跳过时可能还在淡入中）
            _fadeAlpha = 1f;
        }

        private void BeginEnding()
        {
            // ══════════════════ S64 修复：重新读取结局分支 ══════════════════
            //
            //   玩家实测："我明明放了保险丝，结局第一句却是『你什么也没有放』"
            //
            //   Awake() 时玩家还没做选择（HasOffered=false），
            //   所以 endingLines 被定死成结局 B。
            //   这里在**真正播放结局的那一刻**重新读一次，
            //   确保播放的是玩家最终选择对应的文案。
            ApplyEndingText();

            _phase = Phase.FadeOut;
            FreezePlayer();

            if (AbyssEcho.Audio.AudioManager.Instance != null)
            {
                AbyssEcho.Audio.AudioManager.Instance.StopAmbient();
                AbyssEcho.Audio.AudioManager.Instance.PlayStinger(0.5f);
            }

            Debug.Log("[Ending] 开始播放结局");
        }

        private void FreezePlayer()
        {
            if (_inputFrozen) return;
            _inputFrozen = true;

            if (_motor != null) _motor.enabled = false;
            if (_look != null) _look.enabled = false;
            if (_detector != null) _detector.enabled = false;
            if (_crosshair != null) _crosshair.enabled = false;
            if (_inventory != null) _inventory.enabled = false;
            if (_pause != null) _pause.enabled = false;
            if (_fearVisuals != null) _fearVisuals.enabled = false;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void RestartScene()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            var sc = SceneManager.GetActiveScene();
            if (sc.buildIndex >= 0) SceneManager.LoadScene(sc.buildIndex);
            else SceneManager.LoadScene(sc.name);
        }

        // ────────────────────────── 绘制 ──────────────────────────

        private void OnGUI()
        {
            if (_phase == Phase.Idle || _phase == Phase.Waiting) return;
            EnsureStyles();

            // 黑幕
            if (_fadeAlpha > 0.001f)
            {
                var pc = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, _fadeAlpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _white);
                GUI.color = pc;
            }

            float cy = Screen.height * 0.5f;

            // 逐行文字
            if (_phase == Phase.Lines && _lineIndex < endingLines.Length)
            {
                _lineStyle.normal.textColor = new Color(textColor.r, textColor.g, textColor.b, _lineAlpha);
                GUI.Label(new Rect(0f, cy - 24f, Screen.width, 50f),
                          endingLines[_lineIndex], _lineStyle);
            }

            // 结局标题
            if (_phase == Phase.Finale)
            {
                float a = Mathf.Clamp01(_timer / 1.2f);

                _titleStyle.normal.textColor = new Color(textColor.r, textColor.g, textColor.b, a);
                GUI.Label(new Rect(0f, cy - 80f, Screen.width, 56f), chapterTitle, _titleStyle);

                _footerStyle.normal.textColor = new Color(0.55f, 0.62f, 0.70f, a);
                GUI.Label(new Rect(0f, cy - 14f, Screen.width, 30f), chapterFooter, _footerStyle);

                if (IsFinished)
                {
                    _footerStyle.normal.textColor = new Color(0.45f, 0.72f, 0.58f, 0.9f);
                    GUI.Label(new Rect(0f, cy + 50f, Screen.width, 26f), restartHint, _footerStyle);

                    // S29：结局结束后提供"进入下一章 / 返回主菜单"按钮
                    DrawPostEndingButtons(cy);
                }
            }
        }

        /// <summary>
        /// 结局结束后的操作按钮。
        ///
        /// 为什么加这个：
        ///   以前结局放完后只剩一句"按 R 重新开始"，玩家想继续下一章
        ///   必须关掉游戏、重开、在主菜单重新选章节。
        /// </summary>
        private void DrawPostEndingButtons(float cy)
        {
            EnsureButtonStyles();

            float bw = 240f, bh = 52f, gap = 24f;
            float totalW = bw * 2 + gap;
            float x0 = (Screen.width - totalW) * 0.5f;
            float y = cy + 96f;

            var chapter = AbyssEcho.LevelGen.ChapterLayout.Current;
            bool hasNext = HasNextChapter(chapter);

            // 左：进入下一章（末章显示"重新开始冒险"）
            if (hasNext)
            {
                if (GUI.Button(new Rect(x0, y, bw, bh), "进入下一章", _buttonStyle))
                {
                    GoToNextChapter(chapter);
                }
            }
            else
            {
                // ══════════════════ S53 修复：末章应有"游戏结束"感 ══════════════════
                //
                //   原来是"重新探索本章" —— 玩家打完最终章又被丢回同一关，
                //   没有任何"结局"的收束感，像是卡住了。
                //
                //   改成"重新开始冒险"（回第一章）+ 明确的结局标题。
                if (GUI.Button(new Rect(x0, y, bw, bh), "重新开始冒险", _buttonStyle))
                {
                    // 回到第一章重新开始
                    AbyssEcho.LevelGen.ChapterLayout.Current =
                        AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter1;
                    AbyssEcho.Save.SaveSystem.Delete();
                    Time.timeScale = 1f;
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    UnityEngine.SceneManagement.SceneManager.LoadScene(
                        AbyssEcho.LevelGen.ChapterLayout.GetSceneName(
                            AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter1));
                }
            }

            // 右：返回主菜单
            if (GUI.Button(new Rect(x0 + bw + gap, y, bw, bh), "返回主菜单", _buttonStyle))
            {
                GoToMainMenu();
            }
        }

        private static bool HasNextChapter(AbyssEcho.LevelGen.ChapterLayout.Chapter c)
        {
            // 第一版共五章：1 → 2 → 3 → 4 → 5，第五章是终点（没有下一章）
            return c != AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter5;
        }

        private void GoToNextChapter(AbyssEcho.LevelGen.ChapterLayout.Chapter current)
        {
            // ══════════════════ S53 修复：第三章之后无法进入下一章 ══════════════════
            //
            //   原代码的 switch 只写了 Ch1→Ch2、Ch2→Ch3，其余全部落到 default：
            //
            //       default: next = current; break;      ← 第三章按下"进入下一章"时
            //                                              next 还是 Chapter3 自己
            //
            //   结果：第三章通关点"进入下一章"，游戏**重新加载了第三章**，
            //   玩家看到黑屏后回到同一章开头，以为卡死了。
            //   第四章同理，永远进不了第五章。
            //
            //   修复：补齐 Ch3→Ch4、Ch4→Ch5。第五章本来就是终点（HasNextChapter 返回 false），
            //   不会走到这里。
            var next = current;
            switch (current)
            {
                case AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter1:
                    next = AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter2;
                    break;
                case AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter2:
                    next = AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter3;
                    break;
                case AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter3:
                    next = AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter4;
                    break;
                case AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter4:
                    next = AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter5;
                    break;
                default:
                    // 第五章没有下一章，正常不会到这里。
                    // 万一被调用，返回主菜单而不是重新加载本章（避免"看起来卡死"）。
                    Debug.LogWarning($"[Ending] 章节 {current} 没有下一章，返回主菜单");
                    GoToMainMenu();
                    return;
            }

            AbyssEcho.LevelGen.ChapterLayout.Current = next;
            Debug.Log($"[Ending] 切换到下一章：{AbyssEcho.LevelGen.ChapterLayout.GetChapterName(next)}");

            // 清掉本章存档，让下一章从新游戏开始
            AbyssEcho.Save.SaveSystem.Delete();

            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            UnityEngine.SceneManagement.SceneManager.LoadScene(
                AbyssEcho.LevelGen.ChapterLayout.GetSceneName(next));
        }

        private void GoToMainMenu()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        private GUIStyle _buttonStyle;

        private void EnsureButtonStyles()
        {
            if (_buttonStyle == null)
            {
                _buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 22,
                    alignment = TextAnchor.MiddleCenter
                };
                AbyssEcho.UI.ChineseFont.Apply(_buttonStyle);
            }
            _buttonStyle.normal.textColor = new Color(0.85f, 0.90f, 0.95f);
            _buttonStyle.hover.textColor = new Color(1f, 0.85f, 0.45f);
        }

        private void EnsureStyles()
        {
            if (_lineStyle == null)
            {
                _lineStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = lineFontSize,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true
                };
            }
            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = titleFontSize,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }
            if (_footerStyle == null)
            {
                _footerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = footerFontSize,
                    alignment = TextAnchor.MiddleCenter
                };
            }
            AbyssEcho.UI.ChineseFont.Apply(_lineStyle);
            AbyssEcho.UI.ChineseFont.Apply(_titleStyle);
            AbyssEcho.UI.ChineseFont.Apply(_footerStyle);
        }
    }
}
