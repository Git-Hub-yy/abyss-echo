using UnityEngine;
using UnityEngine.AI;

namespace AbyssEcho.Monster
{
    /// <summary>
    /// 怪物 AI 状态机。
    ///
    /// 状态：
    ///   Dormant    休眠（未激活，完全静止，不消耗性能）
    ///   Patrol     巡逻（在若干路径点之间移动）
    ///   Alert      警觉（听到声音，前往调查）
    ///   Chase      追击（看到玩家，加速接近）
    ///   Search     搜索（跟丢后，在最后位置附近徘徊）
    ///   Stunned    僵直（被手电筒强光直射时短暂停住）
    ///
    /// 感知：
    ///   · 视觉：视野锥 + 射线遮挡检测（墙体能挡住视线）
    ///   · 听觉：玩家跑步/走路产生不同半径的"噪音"，怪物在半径内会警觉
    ///
    /// 设计原则（对应《08 恐怖设计》）：
    ///   · 怪物不是"无脑追杀"，有可预测的规则（玩家能学会利用）
    ///   · 手电筒照射能短暂僵直（给玩家一个应对手段）
    ///   · 跟丢后会搜索一段时间，而不是瞬间脱战
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class MonsterAI : MonoBehaviour
    {
        public enum State { Dormant, Patrol, Alert, Chase, Search, Stunned }

        [Header("感知")]
        [Tooltip("视野距离（米）")]
        public float sightRange = 16f;

        [Tooltip("视野角度（度，总角度）")]
        public float sightAngle = 110f;

        [Tooltip("听觉半径：玩家走路时可被听见的距离")]
        public float hearingRangeWalk = 9f;

        [Tooltip("听觉半径：玩家跑步时可被听见的距离")]
        public float hearingRangeRun = 18f;

        [Tooltip("遮挡检测的层（墙体等）")]
        public LayerMask occlusionLayers = ~0;

        [Header("移动速度（m/s）")]
        public float patrolSpeed = 1.15f;
        public float alertSpeed = 1.8f;
        public float chaseSpeed = 3.4f;
        public float searchSpeed = 1.6f;

        [Header("战斗/接触")]
        [Tooltip("抓住玩家的距离")]
        public float catchDistance = 1.4f;

        [Header("巡逻点")]
        [Tooltip("巡逻路径点（世界坐标）；为空则原地待机")]
        public Transform[] patrolPoints;
        public float patrolWaitTime = 2.5f;

        [Header("搜索")]
        [Tooltip("跟丢后搜索持续时间")]
        public float searchDuration = 12f;
        [Tooltip("搜索半径")]
        public float searchRadius = 8f;

        [Header("手电筒反制")]
        [Tooltip("被强光照射多久后僵直")]
        public float stunByLightTime = 1.2f;
        [Tooltip("僵直持续时间")]
        public float stunDuration = 2.5f;
        [Tooltip("手电筒能造成僵直的最大距离")]
        public float flashlightStunRange = 12f;

        [Header("动画")]
        public Animator animator;
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int StateHash = Animator.StringToHash("State");

        [Header("引用")]
        public AbyssEcho.Player.PlayerMotor player;
        public AbyssEcho.Tools.FlashlightSystem flashlight;

        /// <summary>当前状态（只读）。</summary>
        public State CurrentState { get; private set; } = State.Dormant;

        /// <summary>状态变化事件（旧状态, 新状态）。</summary>
        public event System.Action<State, State> OnStateChanged;

        private NavMeshAgent _agent;
        private Vector3 _lastKnownPlayerPos;
        private float _searchTimer;
        private float _lightExposeTimer;
        private float _stunTimer;
        private float _peaceLogTimer;     // S55：和平模式日志节流
        private int _patrolIndex;
        private float _waitTimer;
        private bool _hasSeenPlayer;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (player == null) player = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            if (flashlight == null) flashlight = FindFirstObjectByType<AbyssEcho.Tools.FlashlightSystem>();
        }

        private void Update()
        {
            if (_peaceLogTimer > 0f) _peaceLogTimer -= Time.deltaTime;

            if (CurrentState == State.Dormant) return;
            if (player == null) return;

            UpdatePerception();

            switch (CurrentState)
            {
                case State.Patrol: UpdatePatrol(); break;
                case State.Alert: UpdateAlert(); break;
                case State.Chase: UpdateChase(); break;
                case State.Search: UpdateSearch(); break;
                case State.Stunned: UpdateStunned(); break;
            }

            UpdateAnimation();
        }

        // ────────────────────────── 感知 ──────────────────────────

        private void UpdatePerception()
        {
            if (CurrentState == State.Stunned) return;

            // ① 手电筒反制：被光直射且玩家看着自己
            if (flashlight != null && flashlight.IsOn)
            {
                Vector3 toMonster = (transform.position - player.transform.position);
                float dist = toMonster.magnitude;

                if (dist < flashlightStunRange)
                {
                    // 玩家朝向是否大致对准怪物
                    Vector3 playerFwd = player.transform.forward;
                    float dot = Vector3.Dot(playerFwd, toMonster.normalized);
                    if (dot > 0.86f)   // 约 30 度锥角
                    {
                        _lightExposeTimer += Time.deltaTime;
                        if (_lightExposeTimer >= stunByLightTime)
                        {
                            EnterStun();
                            _lightExposeTimer = 0f;
                            return;
                        }
                    }
                    else _lightExposeTimer = Mathf.Max(0f, _lightExposeTimer - Time.deltaTime * 2f);
                }
                else _lightExposeTimer = Mathf.Max(0f, _lightExposeTimer - Time.deltaTime * 2f);
            }
            else
            {
                _lightExposeTimer = Mathf.Max(0f, _lightExposeTimer - Time.deltaTime * 2f);
            }

            // ② 视觉
            if (CanSeePlayer())
            {
                _lastKnownPlayerPos = player.transform.position;
                _hasSeenPlayer = true;

                if (CurrentState != State.Chase)
                {
                    SetState(State.Chase);
                    if (AbyssEcho.Audio.AudioManager.Instance != null)
                        AbyssEcho.Audio.AudioManager.Instance.PlayStinger(0.35f);
                    if (AbyssEcho.Horror.FearSystem.Instance != null)
                        AbyssEcho.Horror.FearSystem.Instance.AddFear(0.25f);
                }
                return;
            }

            // ③ 听觉
            float noiseRadius = player.IsSprinting ? hearingRangeRun
                              : player.IsCrouching ? hearingRangeWalk * 0.35f
                              : hearingRangeWalk;

            if (player.CurrentSpeed > 0.3f)
            {
                float dist = Vector3.Distance(transform.position, player.transform.position);
                if (dist < noiseRadius && CurrentState == State.Patrol)
                {
                    _lastKnownPlayerPos = player.transform.position;
                    SetState(State.Alert);
                }
            }
        }

        private bool CanSeePlayer()
        {
            Vector3 eye = transform.position + Vector3.up * 1.5f;
            Vector3 target = player.transform.position + Vector3.up * 1.0f;
            Vector3 dir = target - eye;
            float dist = dir.magnitude;

            if (dist > sightRange) return false;

            // 视野锥
            float angle = Vector3.Angle(transform.forward, dir);
            if (angle > sightAngle * 0.5f) return false;

            // 遮挡检测
            if (Physics.Raycast(eye, dir.normalized, out var hit, dist, occlusionLayers,
                                QueryTriggerInteraction.Ignore))
            {
                // 命中的不是玩家 → 被墙挡住
                if (hit.collider.GetComponentInParent<AbyssEcho.Player.PlayerMotor>() == null)
                    return false;
            }

            return true;
        }

        // ────────────────────────── 状态行为 ──────────────────────────

        private void UpdatePatrol()
        {
            _agent.speed = patrolSpeed;

            if (patrolPoints == null || patrolPoints.Length == 0)
            {
                if (_waitTimer <= 0f) _waitTimer = patrolWaitTime;
                _waitTimer -= Time.deltaTime;
                return;
            }

            if (_waitTimer > 0f)
            {
                _waitTimer -= Time.deltaTime;
                _agent.isStopped = true;
                if (_waitTimer <= 0f) _agent.isStopped = false;
                return;
            }

            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.2f)
            {
                _patrolIndex = (_patrolIndex + 1) % patrolPoints.Length;
                _waitTimer = patrolWaitTime;
                if (patrolPoints[_patrolIndex] != null)
                    _agent.SetDestination(patrolPoints[_patrolIndex].position);
            }
        }

        private void UpdateAlert()
        {
            _agent.speed = alertSpeed;
            _agent.isStopped = false;
            _agent.SetDestination(_lastKnownPlayerPos);

            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.5f)
            {
                // 到达没发现人 → 转为搜索
                EnterSearch();
            }
        }

        private void UpdateChase()
        {
            _agent.speed = chaseSpeed;
            _agent.isStopped = false;

            if (_hasSeenPlayer)
                _agent.SetDestination(player.transform.position);

            // 抓住玩家
            float d = Vector3.Distance(transform.position, player.transform.position);
            if (d < catchDistance)
            {
                CatchPlayer();
                return;
            }

            // 跟丢 → 搜索
            if (!CanSeePlayer())
            {
                _hasSeenPlayer = false;
                EnterSearch();
            }
        }

        private void UpdateSearch()
        {
            _agent.speed = searchSpeed;

            _searchTimer -= Time.deltaTime;
            if (_searchTimer <= 0f)
            {
                SetState(State.Patrol);
                return;
            }

            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.5f)
            {
                // 在最后位置附近随机选点
                Vector3 rnd = _lastKnownPlayerPos +
                              new Vector3(Random.Range(-searchRadius, searchRadius), 0f,
                                          Random.Range(-searchRadius, searchRadius));
                if (NavMesh.SamplePosition(rnd, out var navHit, 4f, NavMesh.AllAreas))
                    _agent.SetDestination(navHit.position);
            }
        }

        private void UpdateStunned()
        {
            _agent.isStopped = true;
            _stunTimer -= Time.deltaTime;
            if (_stunTimer <= 0f)
            {
                _agent.isStopped = false;
                SetState(State.Search);
                _searchTimer = searchDuration * 0.5f;
            }
        }

        // ────────────────────────── 转换 ──────────────────────────

        private void SetState(State s)
        {
            if (s == CurrentState) return;
            var old = CurrentState;
            CurrentState = s;
            OnStateChanged?.Invoke(old, s);
            Debug.Log($"[Monster] {old} → {s}");
        }

        private void EnterSearch()
        {
            _searchTimer = searchDuration;
            SetState(State.Search);
        }

        private void EnterStun()
        {
            _stunTimer = stunDuration;
            _agent.isStopped = true;
            SetState(State.Stunned);
        }

        private void CatchPlayer()
        {
            // ══════════════════ S55：和平模式（仅测试用）══════════════════
            //
            //   开启和平模式时直接返回，不执行任何抓人逻辑：
            //     · 不拉满恐惧
            //     · 不调用 PlayerDeathSystem.Kill()
            //     · 不进入 Stunned 状态（否则怪物会呆立 999 秒）
            //
            //   怪物会继续待在 Chase 状态跟着玩家，但永远抓不到人。
            //   关闭和平模式后，下一次距离判定就会正常触发抓人。
            if (!AbyssEcho.DebugTools.PeaceModeManager.AllowAttack)
            {
                if (_peaceLogTimer <= 0f)
                {
                    _peaceLogTimer = 3f;
                    Debug.Log("[Monster] 已抓到玩家，但和平模式开启 → 不造成死亡");
                }
                return;
            }

            Debug.Log("[Monster] 抓住玩家");

            // 恐惧拉满
            if (AbyssEcho.Horror.FearSystem.Instance != null)
                AbyssEcho.Horror.FearSystem.Instance.SetFear(1f);

            // 触发死亡流程（黑屏 -> 重开界面）
            if (AbyssEcho.Horror.PlayerDeathSystem.Instance != null)
            {
                AbyssEcho.Horror.PlayerDeathSystem.Instance.Kill("被怪物抓住");
            }
            else
            {
                // 兜底：没有死亡系统时只播声音，不中断游戏
                if (AbyssEcho.Audio.AudioManager.Instance != null)
                    AbyssEcho.Audio.AudioManager.Instance.PlayStinger(0.8f);
                Debug.LogWarning("[Monster] 场景中没有 PlayerDeathSystem，无法触发死亡流程");
            }

            // 抓住后停止追击，避免重复触发
            _agent.isStopped = true;
            SetState(State.Stunned);
            _stunTimer = 999f;
        }

        // ────────────────────────── 动画 ──────────────────────────

        private void UpdateAnimation()
        {
            if (animator == null) return;

            float speed = _agent.velocity.magnitude;
            animator.SetFloat(SpeedHash, speed);
            animator.SetInteger(StateHash, (int)CurrentState);

            // 速度驱动 Walk/Run 的混合（若有 BlendTree）
            animator.speed = CurrentState == State.Stunned ? 0.15f : 1f;
        }

        // ────────────────────────── 公开接口 ──────────────────────────

        /// <summary>激活怪物，开始巡逻。</summary>
        public void Activate()
        {
            if (CurrentState != State.Dormant) return;
            SetState(State.Patrol);
            _waitTimer = 0f;
            if (patrolPoints != null && patrolPoints.Length > 0 && patrolPoints[0] != null)
                _agent.SetDestination(patrolPoints[0].position);
        }

        /// <summary>强制休眠。</summary>
        public void Deactivate()
        {
            _agent.isStopped = true;
            SetState(State.Dormant);
        }
    }
}
