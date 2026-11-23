using System.Collections.Generic;
using UnityEngine;

namespace AbyssEcho.UI
{
    /// <summary>
    /// 章节目标提示 HUD（纯 OnGUI 绘制，不需要 Canvas / Prefab / 贴图）。
    ///
    /// ══════════════════ 为什么需要它 ══════════════════
    ///
    ///   实际试玩反馈：第三/四/五章"不知道该干什么"。
    ///
    ///   排查发现：项目里**完全没有任务系统**。
    ///   交互物上虽然有提示文字（如"供奉台是空的——把一样东西放上去"），
    ///   但那是"走到跟前才看得到"的局部提示，
    ///   玩家不知道：
    ///     · 本章要达成什么目标
    ///     · 该往哪个方向走
    ///     · 现在是第几步
    ///
    ///   本组件补充这三件事，不改动任何关卡结构。
    ///
    /// ══════════════════ 设计原则 ══════════════════
    ///
    ///   · 只做"引导"，不做"剧透"：不告诉玩家谜题答案
    ///   · 目标状态自动从场景里的实际组件读取，不靠玩家手动勾选
    ///   · 放在屏幕右上角，不挡视线
    ///   · 可按键 H 隐藏/显示
    /// </summary>
    public class ObjectiveHUD : MonoBehaviour
    {
        [Header("显示")]
        public bool visible = true;
        public KeyCode toggleKey = KeyCode.H;
        public float width = 320f;
        public float margin = 18f;

        [Header("样式")]
        public int titleFontSize = 15;
        public int bodyFontSize = 14;
        public int hintFontSize = 12;
        public Color titleColor = new Color(0.55f, 0.85f, 1f, 0.95f);
        public Color bodyColor = new Color(0.90f, 0.94f, 0.98f, 0.95f);
        public Color doneColor = new Color(0.50f, 0.85f, 0.60f, 0.95f);
        public Color hintColor = new Color(0.55f, 0.62f, 0.72f, 0.85f);
        public Color panelColor = new Color(0.03f, 0.05f, 0.07f, 0.72f);

        // ── 运行时状态 ──
        private string _chapterTitle = "";
        private readonly List<(string text, bool done)> _objectives = new List<(string, bool)>();
        private string _nextStepHint = "";
        private float _openTimer;          // 开局自动显示多久后淡出

        private GUIStyle _titleStyle, _bodyStyle, _hintStyle;
        private Texture2D _white;

        // ── 场景引用（每帧刷新，组件事先可能存在也可能不存在） ──
        private AbyssEcho.Puzzle.OfferingAltar _altar;
        private AbyssEcho.Puzzle.PulseSyncPuzzle _pulse;
        private AbyssEcho.Puzzle.EvacuationConsole _evac;
        private AbyssEcho.Inventory.PlayerInventory _inventory;
        private AbyssEcho.Player.PlayerMotor _motor;

        // ── S71：到达型目标的判定状态 ──
        //
        //   玩家反馈："进入核心舱之后，右上角的任务没有显示完成"
        //
        //   原因：这两项目标被硬编码成 false，从不检查玩家位置。
        //
        //       _objectives.Add(("进入核心腔", false));     // ← 永远 false
        //       _objectives.Add(("抵达出口舱", false));     // ← 永远 false
        //
        //   修复：用区域判定 + 一次性锁存（进入过就算完成，离开不撤销）。
        private bool _reachedCoreChamber;
        private bool _reachedExitBay;

        /// <summary>第四章 G7 核心腔的水平范围（来自 ChapterFourLayout 的 RoomDef G7）</summary>
        private static readonly Vector2 CoreChamberMin = new Vector2(-10f, 7f);
        private static readonly Vector2 CoreChamberMax = new Vector2(10f, 25f);

        /// <summary>第五章出口舱范围。运行时从场景对象自动推导</summary>
        private Vector2 _exitBayMin;
        private Vector2 _exitBayMax;
        private bool _exitBayBoundsResolved;

        private void Awake()
        {
            _white = Texture2D.whiteTexture;
            _openTimer = 8f;   // 开局显示 8 秒，之后淡化为半透明常驻
        }

        private void Start()
        {
            EnsureChapterMatchesScene();
            RefreshRefs();
            BuildObjectives();
        }

        /// <summary>
        /// S78：**按场景名校正章节**，与 ChapterEndingSystem 的同类逻辑一致。
        ///
        /// 玩家反馈："到达第五章终点时，右上角目标没有显示'抵达出口舱'完成"
        ///
        /// 根因：
        ///   ObjectiveHUD 完全依赖 ChapterLayout.Current，
        ///   而后者来自 PlayerPrefs["game.currentChapter"]。
        ///
        ///   在编辑器里直接 Play 第五章场景时，PlayerPrefs 可能是旧值（如 4）。
        ///   → BuildCh5() 不会被调用，而是走了 BuildCh3/BuildCh4 的文案
        ///   → 屏幕上根本没有"抵达出口舱"这一项
        ///
        ///   虽然 ChapterEndingSystem.Awake() 也会校正章节，
        ///   但 Unity **不保证跨对象的 Awake/Start 顺序** —
        ///   ObjectiveHUD.Start() 可能先于 ChapterEndingSystem.Awake() 执行。
        ///
        ///   所以这里必须独立校正一次，不能依赖别的脚本。
        /// </summary>
        private void EnsureChapterMatchesScene()
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(sceneName)) return;

            int chapterNum = ParseChapterFromSceneName(sceneName);
            if (chapterNum < 1 || chapterNum > 5) return;

            var fromScene = (AbyssEcho.LevelGen.ChapterLayout.Chapter)chapterNum;
            if (fromScene != AbyssEcho.LevelGen.ChapterLayout.Current)
            {
                Debug.Log($"[Objective] 场景 '{sceneName}' → 校正章节为 {fromScene}");
                AbyssEcho.LevelGen.ChapterLayout.Current = fromScene;
            }
        }

        /// <summary>从场景名解析章节号；支持 Level_ChapterN_Graybox 与 Walk_ChN。</summary>
        private static int ParseChapterFromSceneName(string sceneName)
        {
            int idx = sceneName.IndexOf("Chapter", System.StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                int p = idx + "Chapter".Length;
                if (p < sceneName.Length && sceneName[p] >= '1' && sceneName[p] <= '9')
                    return sceneName[p] - '0';
            }

            int chIdx = sceneName.IndexOf("_Ch", System.StringComparison.OrdinalIgnoreCase);
            if (chIdx >= 0)
            {
                int p = chIdx + "_Ch".Length;
                if (p < sceneName.Length && sceneName[p] >= '1' && sceneName[p] <= '9')
                    return sceneName[p] - '0';
            }

            return 0;
        }

        private void RefreshRefs()
        {
            if (_altar == null) _altar = FindFirstObjectByType<AbyssEcho.Puzzle.OfferingAltar>();
            if (_pulse == null) _pulse = FindFirstObjectByType<AbyssEcho.Puzzle.PulseSyncPuzzle>();
            if (_evac == null) _evac = FindFirstObjectByType<AbyssEcho.Puzzle.EvacuationConsole>();
            if (_inventory == null) _inventory = FindFirstObjectByType<AbyssEcho.Inventory.PlayerInventory>();
            if (_motor == null) _motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) visible = !visible;

            if (_openTimer > 0f) _openTimer -= Time.deltaTime;

            // 每秒刷新一次目标状态即可（不需要每帧）
            _refreshTimer -= Time.deltaTime;
            if (_refreshTimer <= 0f)
            {
                _refreshTimer = 0.5f;
                RefreshRefs();
                CheckReachObjectives();     // S71：先检查是否到达目标区域
                BuildObjectives();
            }
        }

        private float _refreshTimer;

        /// <summary>
        /// S71：检查"到达型"目标。
        ///
        /// 这类目标（进入核心腔 / 抵达出口舱）原本被硬编码成 false，
        /// 导致玩家到达后任务框永远不显示完成。
        ///
        /// 现在每 0.5 秒检查一次玩家位置，进入区域就锁存为 true。
        /// 锁存的意义：离开区域后不会退回未完成状态。
        /// </summary>
        private void CheckReachObjectives()
        {
            if (_motor == null) return;
            Vector3 p = _motor.transform.position;

            var chapter = AbyssEcho.LevelGen.ChapterLayout.Current;

            // ── 第四章：进入核心腔（G7）──
            if (chapter == AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter4)
            {
                if (!_reachedCoreChamber)
                {
                    bool inside = p.x >= CoreChamberMin.x && p.x <= CoreChamberMax.x &&
                                  p.z >= CoreChamberMin.y && p.z <= CoreChamberMax.y;
                    if (inside)
                    {
                        _reachedCoreChamber = true;
                        Debug.Log($"[Objective] 已进入核心腔 @ ({p.x:F1}, {p.z:F1})");
                    }
                }
            }

            // ── 第五章：抵达出口舱 ──
            if (chapter == AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter5)
            {
                EnsureExitBayBounds();

                if (!_reachedExitBay && _exitBayBoundsResolved)
                {
                    bool inside = p.x >= _exitBayMin.x && p.x <= _exitBayMax.x &&
                                  p.z >= _exitBayMin.y && p.z <= _exitBayMax.y;
                    if (inside)
                    {
                        _reachedExitBay = true;
                        Debug.Log($"[Objective] 已抵达出口舱 @ ({p.x:F1}, {p.z:F1})");
                    }
                }
            }
        }

        /// <summary>
        /// 第五章出口舱范围：优先从场景里的 ExitBay 类对象推导，
        /// 推导不出来时回退到一个保守的默认范围。
        /// </summary>
        private void EnsureExitBayBounds()
        {
            if (_exitBayBoundsResolved) return;

            // ══════════════════ S78：锚点查找修正 ══════════════════
            //
            //   原逻辑：
            //     foreach (Transform) if (n == "Floor_H7" || n.Contains("ExitBay") || n.Contains("H7"))
            //         if (有 Renderer) { anchor = 它; break; }
            //
            //   问题：Contains("H7") 会先匹配到 **H6_H7_Link** 的墙、天花板等对象，
            //         它们的名字里也有 "H7"，而且遍历顺序不确定。
            //         一旦锚到 H6_H7_Link（Z[44,48]），
            //         出口舱范围就变成通道区域 → 玩家走到通道就算"抵达出口舱"
            //         → 目标可能在错误位置完成，或在 H7 里反而不完成。
            //
            //   修正：
            //     ① 优先精确匹配 "Floor_H7"（出口舱地板）
            //     ② 其次匹配名字以 "H7" 开头的对象
            //     ③ 都不行再用默认范围

            GameObject anchor = null;

            // ① 精确匹配 Floor_H7
            var floors = GameObject.Find("Floor_H7");
            if (floors != null && floors.GetComponent<Renderer>() != null)
                anchor = floors;

            // ② 退而求其次：名字严格以 "H7" 开头且有 Renderer
            if (anchor == null)
            {
                foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                {
                    if (!t.name.StartsWith("H7")) continue;
                    if (t.GetComponent<Renderer>() == null) continue;
                    anchor = t.gameObject;
                    break;
                }
            }

            if (anchor != null)
            {
                var b = anchor.GetComponent<Renderer>().bounds;
                _exitBayMin = new Vector2(b.min.x, b.min.z);
                _exitBayMax = new Vector2(b.max.x, b.max.z);
                _exitBayBoundsResolved = true;
                Debug.Log($"[Objective] 出口舱范围 = X[{_exitBayMin.x:F1},{_exitBayMax.x:F1}] " +
                          $"Z[{_exitBayMin.y:F1},{_exitBayMax.y:F1}] （来自 {anchor.name}）");
            }
            else
            {
                // 场景里没有可识别的锚点：用第五章终点房间的保守范围
                _exitBayMin = new Vector2(-30f, 30f);
                _exitBayMax = new Vector2(30f, 80f);
                _exitBayBoundsResolved = true;
                Debug.Log("[Objective] 出口舱范围使用默认值（未找到锚点对象）");
            }
        }

        /// <summary>按当前章节和场景实际状态生成目标列表。</summary>
        private void BuildObjectives()
        {
            _objectives.Clear();
            _nextStepHint = "";

            var chapter = AbyssEcho.LevelGen.ChapterLayout.Current;
            string chapterName = AbyssEcho.LevelGen.ChapterLayout.GetChapterName(chapter);

            switch (chapter)
            {
                case AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter3:
                    _chapterTitle = "第三章目标";
                    BuildCh3();
                    break;

                case AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter4:
                    _chapterTitle = "第四章目标";
                    BuildCh4();
                    break;

                case AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter5:
                    _chapterTitle = "第五章目标";
                    BuildCh5();
                    break;

                case AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter1:
                case AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter2:
                    // 第一、二章不在本批修复范围，但 HUD 挂在场景里时会走到这里。
                    // 给最基本的指引，不改变原有流程。
                    _chapterTitle = $"{chapterName} 目标";
                    _objectives.Add(("探索并找到通电设备", false));
                    _nextStepHint = "找到机关，解开后才能继续深入";
                    break;
            }
        }

        // ── 第三章 ──
        private void BuildCh3()
        {
            // 读实际状态
            bool offered = _altar != null && _altar.HasOffered;

            // 玩家背包里的供品
            bool hasTape = HasItem(AbyssEcho.Inventory.ItemIds.Tape);
            bool hasFuse = HasItem(AbyssEcho.Inventory.ItemIds.Fuse);
            bool hasBulb = HasItem(AbyssEcho.Inventory.ItemIds.Bulb);

            bool gotAny = hasTape || hasFuse || hasBulb;

            _objectives.Add(($"找到一件供品（录音带 / 保险丝 / 灯泡）", gotAny));

            if (offered)
            {
                _objectives.Add(("在祭室的供奉台放上供品", true));
                _objectives.Add(("穿过气闸，走进镜沼", false));
                _nextStepHint = "沿中央阶梯廊一路向北，穿过气闸就是镜沼";
            }
            else
            {
                _objectives.Add(("在祭室的供奉台放上供品", false));
                if (!gotAny)
                    _nextStepHint = "供品散落在：起居区（录音带）、旧档案室（保险丝）、工作间（灯泡）";
                else
                    _nextStepHint = "前往会客厅东侧的祭室，把供品放上供奉台";
            }
        }

        // ── 第四章 ──
        private void BuildCh4()
        {
            bool solved = _pulse != null && _pulse.IsSolved;

            // S71：不再是硬编码 false —— 用玩家的实际到达状态
            _objectives.Add(("进入核心腔", _reachedCoreChamber));
            _objectives.Add(("在心跳节拍上按 E 同步 3 次", solved));

            if (solved)
            {
                // 心门已开：如果玩家还没进核心腔，提示走过去
                if (!_reachedCoreChamber)
                    _nextStepHint = "心门已开，沿西门通道走进核心腔深处";
                else
                    _nextStepHint = "核心腔已进入，继续向前寻找出口";
            }
            else if (_reachedCoreChamber)
            {
                _nextStepHint = "核心腔中央的心瓣：看它亮起的那一刻按下 E，连对 3 次";
            }
            else
            {
                _nextStepHint = "从 G5 组织层向北，穿过核心西门进入核心腔";
            }
        }

        // ── 第五章 ──
        private void BuildCh5()
        {
            bool chosen = _evac != null && _evac.HasChosen;

            // S71：不再是硬编码 false
            _objectives.Add(("抵达出口舱", _reachedExitBay));
            _objectives.Add(("在撤离控制台做出选择", chosen));

            if (chosen)
            {
                _nextStepHint = "你已做出选择，走向出口";
            }
            else if (_reachedExitBay)
            {
                _nextStepHint = "出口舱内的撤离台：三个选项对应三种结局，选定不可更改";
            }
            else
            {
                _nextStepHint = "穿过深海核心区，前往最北端的出口舱";
            }
        }

        private bool HasItem(string id)
        {
            if (_inventory == null) return false;
            return _inventory.Has(id);
        }

        // ── 绘制 ──
        private void OnGUI()
        {
            if (!visible) return;
            EnsureStyles();

            // 开局 8 秒全亮，之后降为半透明
            float alpha = _openTimer > 0f ? 1f : 0.62f;

            float x = Screen.width - width - margin;
            float y = margin;

            // 面板底
            int lines = _objectives.Count + (_nextStepHint.Length > 0 ? 1 : 0);
            float panelH = 34f + lines * 24f + 16f;

            var prev = GUI.color;
            GUI.color = new Color(panelColor.r, panelColor.g, panelColor.b, panelColor.a * alpha);
            GUI.DrawTexture(new Rect(x - 10f, y - 8f, width + 20f, panelH), _white);

            // 左侧竖条（颜色标识）
            GUI.color = new Color(titleColor.r, titleColor.g, titleColor.b, 0.85f * alpha);
            GUI.DrawTexture(new Rect(x - 10f, y - 8f, 3f, panelH), _white);
            GUI.color = prev;

            // 标题
            _titleStyle.normal.textColor = Fade(titleColor, alpha);
            GUI.Label(new Rect(x, y, width, 22f), _chapterTitle, _titleStyle);
            y += 26f;

            // 目标条目
            foreach (var (text, done) in _objectives)
            {
                string mark = done ? "✓" : "○";
                _bodyStyle.normal.textColor = done ? Fade(doneColor, alpha) : Fade(bodyColor, alpha);
                GUI.Label(new Rect(x, y, width - 4f, 22f), $"{mark}  {text}", _bodyStyle);
                y += 24f;
            }

            // 下一步提示
            if (_nextStepHint.Length > 0)
            {
                y += 4f;
                _hintStyle.normal.textColor = Fade(hintColor, alpha);
                GUI.Label(new Rect(x, y, width - 4f, 40f), _nextStepHint, _hintStyle);
            }
        }

        private static Color Fade(Color c, float a) => new Color(c.r, c.g, c.b, c.a * a);

        private void EnsureStyles()
        {
            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = titleFontSize,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft
                };
                ChineseFont.Apply(_titleStyle);
            }
            if (_bodyStyle == null)
            {
                _bodyStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = bodyFontSize,
                    alignment = TextAnchor.MiddleLeft,
                    wordWrap = false
                };
                ChineseFont.Apply(_bodyStyle);
            }
            if (_hintStyle == null)
            {
                _hintStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = hintFontSize,
                    alignment = TextAnchor.UpperLeft,
                    wordWrap = true
                };
                ChineseFont.Apply(_hintStyle);
            }
        }
    }
}