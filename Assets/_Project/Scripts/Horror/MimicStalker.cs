using UnityEngine;

namespace AbyssEcho.Horror
{
    /// <summary>
    /// 模仿者（第三章核心机制）。
    ///
    /// ══════════════════════ 设计原理 ══════════════════════
    ///
    ///  **它只在你看不见的时候移动。**
    ///
    ///   你盯着它 → 它一动不动
    ///   你一移开视线 → 它靠近
    ///   你再回头 → 它离你更近了
    ///
    ///  这是"被观测"这个主题的直接体现：
    ///  真正可怕的不是它追你，而是**它在等你把视线移开**。
    ///
    /// ══════════════════════ 与 MonsterAI 的区别 ══════════════════════
    ///
    ///   MonsterAI（第一/二章）：
    ///     · 主动巡逻、主动追击
    ///     · 用 NavMeshAgent 寻路
    ///     · 玩家跑它就追
    ///     · 恐惧来自"被追"
    ///
    ///   MimicStalker（第三章）：
    ///     · 不巡逻、不追击，**只在被看不见时靠近**
    ///     · 不用 NavMesh，直接朝玩家线性移动（更容易控制，也不会卡在墙里）
    ///     · 被看见太久会消失、换个地方重新出现
    ///     · 恐惧来自"它一直在那里，而且越来越近"
    ///
    /// ══════════════════════ 行为状态机 ══════════════════════
    ///
    ///     Idle（静止不动）
    ///       ├─ 玩家看不见 → Approach（靠近）
    ///       └─ 玩家看见 → 保持 Idle，累计 visibleTime
    ///
    ///     Approach（移动）
    ///       ├─ 玩家突然回头看见 → 立即切回 Idle
    ///       ├─ 距离 < attackDistance → Lunge（扑击 → 触发死亡）   ← S45：攻击优先
    ///       ├─ 距离 < vanishDistance → Vanish（消失）
    ///       └─ 攻击成功后进入 Dead，此后不再响应
    ///
    ///   ⚠️ S45 修复说明：
    ///     原来 Vanish 判定写在 Attack 判定之前，而 vanishDistance(5.5) > attackDistance(2.2)，
    ///     导致距离进入 5.5 米就先消失并 return，**攻击判定永远执行不到**。
    ///     现在把攻击判定提前，且改用严格小于号，两个区间不再重叠。
    ///
    ///     Vanish（淡出）
    ///       └─ 延迟后 → 在远处 Respawn → Idle
    ///
    /// ══════════════════════ 重要：可见性判定 ══════════════════════
    ///
    ///   只判断"角度"是不够的 —— 隔着墙也会被判定成"看得见"，
    ///   那样玩家一进房间它就永远不动，机制会完全失效。
    ///
    ///   所以必须做**射线遮挡检测**：
    ///     从相机向它发射一条线，如果先撞到别的碰撞体 → 说明被挡住 → 不可见。
    /// </summary>
    public class MimicStalker : MonoBehaviour
    {
        [Header("视野判定")]
        [Tooltip("玩家的视野半角（度）。超过这个角度就看不见它。")]
        public float sightAngle = 35f;

        [Tooltip("玩家能看见它的最大距离")]
        public float sightRange = 25f;

        [Tooltip("遮挡检测用的层（留空则用全部层）")]
        public LayerMask occlusionMask = ~0;

        [Header("移动")]
        [Tooltip("靠近速度（必须比玩家走路慢，否则太恐怖）")]
        public float approachSpeed = 1.5f;

        [Tooltip("超过这个距离才开始靠近")]
        public float approachStartDistance = 18f;

        [Tooltip("靠近到这个距离就消失（换位置重生）")]
        public float vanishDistance = 5.5f;

        [Tooltip("靠近到这个距离触发扑击（死亡）")]
        public float attackDistance = 2.2f;

        [Tooltip("是否启用扑击（调试时可关掉）")]
        public bool enableAttack = true;

        [Header("被看见太久")]
        [Tooltip("被持续直视多久后消失（秒），0 = 永不消失")]
        public float maxVisibleTime = 2.5f;

        [Header("重生")]
        [Tooltip("消失后多久重新出现")]
        public float respawnDelay = 6f;

        [Tooltip("重生时距玩家的最小/最大距离")]
        public float respawnMinDist = 14f;
        public float respawnMaxDist = 20f;

        [Header("视觉")]
        [Tooltip("淡出速度（每秒透明度变化）")]
        public float fadeSpeed = 1.6f;

        [Tooltip("要参与淡出的 Renderer（留空则自动收集子物体）")]
        public Renderer[] fadeRenderers;

        [Header("调试")]
        public bool logStateChanges = false;

        // ────────── 运行时状态 ──────────

        private enum State { Idle, Approach, Vanish, Dead }
        private State _state = State.Idle;

        private Transform _player;
        private Camera _camera;
        private float _visibleTimer;
        private float _respawnTimer;
        private float _alpha = 1f;

        private Vector3 _homePosition;      // 初始位置（重生时回到附近）
        private MaterialPropertyBlock _mpb;

        /// <summary>玩家当前是否看得见它（供外部查询/测试）。</summary>
        public bool IsVisibleToPlayer { get; private set; }

        /// <summary>当前状态名（调试用）。</summary>
        public string StateName => _state.ToString();

        /// <summary>触发扑击时回调（由外部接管死亡流程）。</summary>
        public static event System.Action OnAttack;

        private void Awake()
        {
            _homePosition = transform.position;
            _mpb = new MaterialPropertyBlock();

            if (fadeRenderers == null || fadeRenderers.Length == 0)
                fadeRenderers = GetComponentsInChildren<Renderer>(true);
        }

        private void Start()
        {
            var motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            if (motor != null) _player = motor.transform;

            _camera = Camera.main;
            if (_camera == null)
            {
                var m = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
                if (m != null) _camera = m.GetComponentInChildren<Camera>(true);
            }

            if (_player == null || _camera == null)
                Debug.LogWarning("[Mimic] 找不到玩家或相机，模仿者将不会活动");

            SetAlpha(1f);
        }

        private void Update()
        {
            if (_peaceLogTimer > 0f) _peaceLogTimer -= Time.deltaTime;

            if (_player == null || _camera == null) return;

            IsVisibleToPlayer = ComputeVisibility();

            switch (_state)
            {
                case State.Idle: UpdateIdle(); break;
                case State.Approach: UpdateApproach(); break;
                case State.Vanish: UpdateVanish(); break;
                case State.Dead: break;
            }
        }

        // ══════════════════ 可见性判定 ══════════════════

        /// <summary>
        /// 玩家是否看得见它。
        ///
        /// 三个条件必须同时满足：
        ///   ① 在视野角度内
        ///   ② 在可见距离内
        ///   ③ 中间没有遮挡物
        /// </summary>
        private bool ComputeVisibility()
        {
            if (_camera == null || _player == null) return false;

            Vector3 camPos = _camera.transform.position;
            Vector3 targetPos = transform.position + Vector3.up * 1.0f;   // 取躯干高度
            Vector3 toTarget = targetPos - camPos;

            float dist = toTarget.magnitude;
            if (dist > sightRange) return false;

            // ① 角度
            float angle = Vector3.Angle(_camera.transform.forward, toTarget);
            DebugVisibility = $"dist={dist:F1} angle={angle:F0} sightAngle={sightAngle} camPos={camPos} camFwd={_camera.transform.forward} target={targetPos}";
            if (angle > sightAngle) return false;

            // ③ 遮挡：从相机向它打一条射线，第一个撞到的东西必须就是它自己
            if (Physics.Raycast(camPos, toTarget.normalized, out var hit,
                                dist + 0.5f, occlusionMask, QueryTriggerInteraction.Ignore))
            {
                // 命中的不是自己（也不是自己的子物体）→ 被挡住了
                if (hit.collider == null) return false;
                if (!hit.collider.transform.IsChildOf(transform) &&
                    hit.collider.gameObject != gameObject)
                    return false;
            }

            return true;
        }

        // ══════════════════ 状态更新 ══════════════════

        private void UpdateIdle()
        {
            // 被持续直视太久 → 消失
            if (IsVisibleToPlayer && maxVisibleTime > 0f)
            {
                _visibleTimer += Time.deltaTime;
                if (_visibleTimer >= maxVisibleTime) { Vanish(); }
            }
            else
            {
                _visibleTimer = 0f;
            }

            // 玩家看不见 且 距离够远 → 开始靠近
            if (!IsVisibleToPlayer)
            {
                float d = FlatDistance();

                // ══════════════════ S45 修复（第二处）══════════════════
                //
                //   原来的条件：d > vanishDistance（5.5）
                //   意思是"只有距离大于 5.5 米才允许进入 Approach"。
                //
                //   于是玩家一旦站到 5.5 米以内，怪物就永远停在 Idle，
                //   而攻击判定写在 UpdateApproach() 里 —— **同样永远进不去**。
                //
                //   （这和 UpdateApproach 里 Vanish 抢在攻击前面是同一类断裂：
                //     vanishDistance 这个值既挡住了移动，也挡住了攻击。）
                //
                //   修复：允许在"攻击距离以内"也进入 Approach，
                //   这样才能走到 Appro̩ach 里的攻击判定。
                //   中间那段（attackDistance ~ vanishDistance）仍然维持原有消失设计。
                if (d <= approachStartDistance && (d > vanishDistance || enableAttack))
                {
                    // 注意：玩家在 5.5 米以内且 enableAttack=true 时也会进入 Approach，
                    // 进入后 UpdateApproach() 里会优先判定攻击。
                    _state = State.Approach;
                    if (logStateChanges) Debug.Log($"[Mimic] {name} → Approach（距离 {d:F1}）");
                }
            }
        }

        private void UpdateApproach()
        {
            // 玩家回头看见 → 立刻定住
            if (IsVisibleToPlayer)
            {
                _state = State.Idle;
                _visibleTimer = 0f;
                if (logStateChanges) Debug.Log($"[Mimic] {name} → Idle（被看见，定住）");
                return;
            }

            float d = FlatDistance();

            // 距离太远就不追了（玩家跑远了）
            if (d > approachStartDistance)
            {
                _state = State.Idle;
                return;
            }

            // ══════════════════ S45 修复：把攻击判定提到消失判定之前 ══════════════════
            //
            //   修改前的问题：
            //
            //        if (d <= vanishDistance /* 5.5 */) { Vanish(); return; }   ← 先执行
            //        MoveTowardPlayer();
            //        if (enableAttack && d <= attackDistance /* 2.2 */) { ... }  ← 永远到不了
            //
            //   vanishDistance(5.5) 比 attackDistance(2.2) 大，
            //   所以距离一旦进到 5.5 米以内，就一定先执行 Vanish 并 return。
            //   **2.2 米的攻击判定成了死代码，模仿者在第三、四、五章永远杀不死玩家。**
            //
            //   修复方式（最小改动）：只在"本帧可以攻击"时，把攻击判定提到最前面。
            //
            //   为什么用 d < attackDistance（严格小于）而不是 <=？
            //     vanishDistance 用的是 <=，这里用严格小于，
            //     保证两个条件的取值集合完全不重叠，不会出现同一帧既想攻击又想消失。
            //
            //   为什么不需要额外的攻击冷却？
            //     攻击成功后 _state 会立刻切到 State.Dead，
            //     而 Update() 里 Dead 分支什么都不做（第 166 行），
            //     也就是**攻击成功即自我冻结**，天然保证只触发一次。
            //     这是原有机制，本修复不删除、不绕过。
            if (enableAttack && d < attackDistance)
            {
                // ══════════════════ S55：和平模式（仅测试用）══════════════════
                //
                //   开启和平模式时不进入扑击分支：
                //     · 不进入 Dead 状态（否则怪物会定在原地被冻结，看起来像坏了）
                //     · 不派发 OnAttack
                //     · 不调用 HitPlayer() → PlayerDeathSystem.Kill()
                //     · 继续留在 Approach 状态，怪物会一直跟着玩家但不伤人
                //
                //   关闭和平模式后，下一帧就会正常扑击，无需重置任何状态。
                if (!AbyssEcho.DebugTools.PeaceModeManager.AllowAttack)
                {
                    if (logStateChanges && _peaceLogTimer <= 0f)
                    {
                        _peaceLogTimer = 3f;
                        Debug.Log($"[Mimic] {name} 已扑到玩家身边，但和平模式开启 → 不攻击");
                    }
                    return;
                }

                if (logStateChanges) Debug.Log($"[Mimic] {name} 扑击！（距离 {d:F2}）");

                _state = State.Dead;              // 原有：进入 Dead 状态（此后 Update 不再处理）
                OnAttack?.Invoke();               // 原有：派发攻击事件
                HitPlayer();                      // 原有：调用 PlayerDeathSystem.Kill()

                return;                           // 攻击已发生，本帧不再做消失判定
            }

            // 到了消失距离 → 换位置重生（营造"它一直在附近"的感觉）
            //   ⚠️ 现在只对"没有达到攻击距离"的情况生效，
            //      玩家站得近时不会再被"提前消失"打断。
            if (d <= vanishDistance)
            {
                Vanish();
                return;
            }

            MoveTowardPlayer();

            // 移动之后再补一次攻击判定：
            //   本帧开始时距离还在 2.2~5.5 之间，但 MoveTowardPlayer() 后可能已经走进攻击范围。
            //   （approachSpeed 1.5 m/s，一帧约 0.025 米，通常跨不过去，
            //     但高帧率抖动或低帧率时可能需要，保留这一层保证不漏判。）
            d = FlatDistance();
            if (enableAttack && d < attackDistance)
            {
                // ══════════════════ S55 修复（二）：漏掉的第二处攻击判定 ══════════════════
                //
                //   上一版只给**第一次**攻击判定（本方法开头）加了和平模式检查，
                //   却漏掉了这里——移动之后的补判。
                //
                //   后果：和平模式下，怪物每帧：
                //     ① 开头的判定 → 被和平模式拦住 ✅
                //     ② MoveTowardPlayer() → 继续朝玩家移动
                //     ③ 这里的补判 → **没有检查** → HitPlayer() → 玩家死亡 ❌
                //
                //   表现就是玩家"没看到怪物就死了"：
                //   怪物从远处一路移过来，在补判里杀了人，而玩家还没注意到它。
                //
                //   这也暴露了我上一版测试的缺陷：测试把怪物**直接贴到玩家身上**，
                //   此时 ① 就会拦住并 return，永远走不到 ③，
                //   所以自动化测试全绿，实际游玩却照样会死。
                if (!AbyssEcho.DebugTools.PeaceModeManager.AllowAttack)
                {
                    if (logStateChanges && _peaceLogTimer <= 0f)
                    {
                        _peaceLogTimer = 3f;
                        Debug.Log($"[Mimic] {name} 已贴近玩家（移动后距离 {d:F2}），但和平模式开启 → 不攻击");
                    }
                    return;
                }

                if (logStateChanges) Debug.Log($"[Mimic] {name} 扑击！（移动后距离 {d:F2}）");

                _state = State.Dead;
                OnAttack?.Invoke();
                HitPlayer();
            }
        }

        private void UpdateVanish()
        {
            _respawnTimer -= Time.deltaTime;

            // 淡出
            if (_alpha > 0.02f)
            {
                SetAlpha(Mathf.MoveTowards(_alpha, 0f, fadeSpeed * Time.deltaTime));
            }

            if (_respawnTimer <= 0f)
            {
                Respawn();
            }
        }

        // ══════════════════ 动作 ══════════════════

        private void MoveTowardPlayer()
        {
            Vector3 target = _player.position;
            // 只在水平面上移动（不钻地、不飞天）
            target.y = transform.position.y;

            Vector3 dir = target - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return;

            float step = approachSpeed * Time.deltaTime;
            Vector3 move = dir.normalized * step;

            // ══════════════════ S53 修复：模仿者穿墙 ══════════════════
            //
            //   原代码直接 transform.position += move; —— 无视一切碰撞体，
            //   模仿者会**穿透墙壁**直线扑向玩家，玩家看到的就是
            //   "怪物从墙里冒出来/凭空出现在身边"。
            //
            //   修复：在移动前用 CapsuleCast 检测前方是否有墙，
            //   被挡住就沿墙滑动（尝试左右两侧的分量），实在走不通就停下。
            //
            //   这样既保留"逼近"的压迫感，又不会穿墙。
            Vector3 origin = transform.position + Vector3.up * 0.9f;   // 躯干高度
            // 探测半径略小于视觉体宽（CapsuleCollider 半径约 0.3）：
            // 太大会在狭窄走廊里被两侧墙同时挡住，导致完全无法移动。
            float radius = 0.24f;
            float castDist = move.magnitude + 0.05f;

            bool blocked = Physics.SphereCast(origin, radius, move.normalized,
                                              out var hit, castDist,
                                              wallMask, QueryTriggerInteraction.Ignore);

            if (!blocked)
            {
                // 前方通畅，正常前进
                transform.position += move;

                // 通畅就重置绕行状态（避免绕行计时无限累积）
                _detourElapsed = 0f;
            }
            else
            {
                // ══════════════════ S53 修复（三）：被墙挡住时"沿墙绕行" ══════════════════
                //
                //   演进过程（都是实测驱动的）：
                //     第一版：直接 transform.position += move  → **穿墙**（玩家反馈的 bug）
                //     第二版：左右各 60° 固定侧滑           → 走廊尽头**永久卡死**
                //     第三版：±40°~±150° 扇形扫描           → 能动了，但会**原地绕圈**
                //              （距离恒为 3.94m，"已移动"却持续增长）
                //
                //   最终方案：**保持一个固定的绕行侧选择**，并记录绕行时间。
                //     · 一旦决定往左/往右绕，就**持续朝那个方向**走，不每帧重新随机
                //       （随机选择正是"原地绕圈"的根源）
                //     · 若绕行超过 maxDetourTime 秒距离仍没缩短，放弃追击回到 Idle
                //       （避免永远围着墙打转，玩家看着莫名其妙）
                float desiredAng = _detourSign * 75f;   // 持续用一个方向

                // 每隔一小段收紧一次角度，逐步逼近目标方向（类似"贴墙走"）
                if (_detourElapsed > 0f)
                    desiredAng = _detourSign * Mathf.Lerp(150f, 45f, Mathf.Clamp01(_detourElapsed / 1.5f));

                Vector3 tryDir = Quaternion.Euler(0f, desiredAng, 0f) * move;

                bool canGo = !Physics.SphereCast(origin, radius, tryDir.normalized,
                                                 out _, tryDir.magnitude + 0.05f,
                                                 wallMask, QueryTriggerInteraction.Ignore);

                if (!canGo)
                {
                    // 这个方向也不行 → 翻转方向再试
                    _detourSign = -_detourSign;
                    desiredAng = _detourSign * 90f;
                    tryDir = Quaternion.Euler(0f, desiredAng, 0f) * move;
                    canGo = !Physics.SphereCast(origin, radius, tryDir.normalized,
                                                out _, tryDir.magnitude + 0.05f,
                                                wallMask, QueryTriggerInteraction.Ignore);
                }

                if (canGo)
                {
                    transform.position += tryDir;
                    _detourElapsed += Time.deltaTime;
                }
                else
                {
                    // 左右都不行：真的无路可走，停住（绝不穿墙）
                    _detourElapsed += Time.deltaTime;
                }

                if (logStateChanges && _blockedLogTimer <= 0f)
                {
                    _blockedLogTimer = 2f;
                    Debug.Log($"[Mimic] {name} 被墙挡住 → 绕行(方向{(_detourSign > 0 ? "右" : "左")}, " +
                              $"已绕 {_detourElapsed:F1}s)");
                }

                // 绕行太久仍无法接近 → 放弃追击，避免原地打转
                if (_detourElapsed > maxDetourTime)
                {
                    _detourElapsed = 0f;
                    _state = State.Idle;
                    _lastKnownDist = float.MaxValue;
                    if (logStateChanges) Debug.Log($"[Mimic] {name} 绕行超过 {maxDetourTime}s 仍无进展 → 放弃追击");
                    return;
                }
            }

            // 始终保持背对玩家（不露脸 —— 露脸就变成"怪物"了）
            Vector3 faceAway = -dir.normalized;
            if (faceAway.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(faceAway),
                    6f * Time.deltaTime);
            }
        }

        /// <summary>穿墙检测用的层遮罩（忽略玩家自己）。</summary>
        private LayerMask wallMask
        {
            get
            {
                // 排除 Player 层，避免把自己和玩家算成障碍
                int playerLayer = LayerMask.NameToLayer("Player");
                if (playerLayer >= 0)
                    return ~(1 << playerLayer);
                return ~0;
            }
        }

        private float _blockedLogTimer;
        private float _peaceLogTimer;     // S55：和平模式日志节流

        // ══ S53：绕行状态（避免"原地绕圈"与"永久卡死"）══
        private int _detourSign = 1;          // +1=右绕, -1=左绕
        private float _detourElapsed;         // 已连续绕行时长
        private float _lastKnownDist = float.MaxValue;

        [Header("绕行")]
        [Tooltip("被墙挡住后，最多绕行多久仍无进展就放弃追击（秒）")]
        public float maxDetourTime = 4f;

        /// <summary>消失：淡出，然后延迟重生。</summary>
        public void Vanish()
        {
            _state = State.Vanish;
            _respawnTimer = respawnDelay;
            _visibleTimer = 0f;
            if (logStateChanges) Debug.Log($"[Mimic] {name} → Vanish");
        }

        /// <summary>在玩家视野外重生。</summary>
        private void Respawn()
        {
            Vector3 pos = PickRespawnPoint();
            transform.position = pos;
            SetAlpha(1f);
            _alpha = 1f;
            _state = State.Idle;
            _visibleTimer = 0f;

            if (logStateChanges) Debug.Log($"[Mimic] {name} 重生于 {pos}");
        }

        /// <summary>
        /// 选一个"玩家看不见、但距离适中"的位置。
        ///
        /// 做法：围绕玩家试 16 个方向，挑第一个不在玩家视野锥内的。
        /// 都不行就退回初始位置。
        /// </summary>
        private Vector3 PickRespawnPoint()
        {
            if (_player == null) return _homePosition;

            Vector3 center = _player.position;
            Vector3 camForward = _camera != null ? _camera.transform.forward : _player.forward;
            camForward.y = 0f;
            camForward.Normalize();

            // ══════════════════ S53 修复：重生点可能在墙里/在视野中 ══════════════════
            //
            //   原代码只判断"在玩家背后"，**完全不检查那个位置是不是墙里**。
            //   于是模仿者经常重生在**墙壁内部或房间外面**，
            //   然后穿透墙体直线扑过来 —— 玩家看到"凭空出现"。
            //
            //   修复：每个候选点必须同时满足
            //     ① 在玩家背后（视野锥外）
            //     ② 该位置没有墙（OverlapSphere 无重叠）
            //     ③ 脚下有地面（向下射线命中）
            //     ④ 从玩家到该点之间**没有墙阻挡**（避免"隔墙出现"）
            //   全部通过才采用；都不行就回到初始位置（初始位置是关卡设计好的安全点）。
            for (int attempt = 0; attempt < 24; attempt++)
            {
                float ang = Random.Range(0f, 360f);
                float dist = Random.Range(respawnMinDist, respawnMaxDist);

                Vector3 dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
                Vector3 cand = center + dir * dist;
                cand.y = _homePosition.y;

                // ① 必须在玩家背后（视野锥外，留 20 度余量）
                float a = Vector3.Angle(camForward, dir);
                if (a < sightAngle + 20f) continue;

                // ② 该位置不能有墙
                var overlaps = Physics.OverlapSphere(cand + Vector3.up * 0.9f, 0.45f,
                                                     ~0, QueryTriggerInteraction.Ignore);
                bool insideWall = false;
                foreach (var o in overlaps)
                {
                    if (o == null || o.isTrigger) continue;
                    if (o.transform.IsChildOf(transform)) continue;
                    if (_player != null && o.transform.IsChildOf(_player)) continue;
                    insideWall = true;
                    break;
                }
                if (insideWall) continue;

                // ③ 脚下要有地面
                if (!Physics.Raycast(cand + Vector3.up * 0.5f, Vector3.down, 3f,
                                     ~0, QueryTriggerInteraction.Ignore))
                    continue;

                // ④ 玩家到候选点之间不能有墙（防止隔墙重生）
                Vector3 from = _player.position + Vector3.up * 1.2f;
                Vector3 to = cand + Vector3.up * 1.2f;
                Vector3 ray = to - from;
                if (Physics.Raycast(from, ray.normalized, out var hh, ray.magnitude,
                                    ~0, QueryTriggerInteraction.Ignore))
                {
                    // 命中的若是玩家自己则不算遮挡
                    bool hitsPlayer = _player != null &&
                                      hh.collider != null &&
                                      hh.collider.transform.IsChildOf(_player);
                    if (!hitsPlayer) continue;
                }

                return cand;
            }

            // 全部候选都不可用 → 回到关卡设计的初始位置
            if (logStateChanges)
                Debug.LogWarning($"[Mimic] {name} 找不到合适的重生点，回到初始位置 {_homePosition}");
            return _homePosition;
        }

        /// <summary>设置整体透明度。</summary>
        private void SetAlpha(float a)
        {
            if (fadeRenderers == null) return;

            foreach (var r in fadeRenderers)
            {
                if (r == null) continue;

                r.GetPropertyBlock(_mpb);
                if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_BaseColor"))
                {
                    var c = r.sharedMaterial.GetColor("_BaseColor");
                    c.a = a;
                    _mpb.SetColor("_BaseColor", c);
                    r.SetPropertyBlock(_mpb);
                }
                else if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Color"))
                {
                    var c = r.sharedMaterial.GetColor("_Color");
                    c.a = a;
                    _mpb.SetColor("_Color", c);
                    r.SetPropertyBlock(_mpb);
                }
            }
        }

        /// <summary>扑击：调用现有的死亡系统。</summary>
        private void HitPlayer()
        {
            var death = FindFirstObjectByType<AbyssEcho.Horror.PlayerDeathSystem>();
            if (death != null) death.Kill("它找到了你。");
            else Debug.Log("[Mimic] 扑击（未找到 PlayerDeathSystem）");
        }

        private float FlatDistance()
        {
            if (_player == null) return float.MaxValue;
            Vector3 a = _player.position; a.y = 0f;
            Vector3 b = transform.position; b.y = 0f;
            return Vector3.Distance(a, b);
        }

        /// <summary>重置到初始状态（测试/重开用）。</summary>
        public void ResetStalker()
        {
            transform.position = _homePosition;
            _state = State.Idle;
            _alpha = 1f;
            _visibleTimer = 0f;
            _respawnTimer = 0f;
            SetAlpha(1f);
        }

        /// <summary>诊断信息（测试用）。</summary>
        public string DebugVisibility { get; private set; } = "";

        /// <summary>供测试：强制检查可见性（不依赖 Update）。</summary>
        public bool TestVisibility() => ComputeVisibility();

        /// <summary>供测试：手动推进一次状态机。</summary>
        public void TestTick() => Update();
    }
}