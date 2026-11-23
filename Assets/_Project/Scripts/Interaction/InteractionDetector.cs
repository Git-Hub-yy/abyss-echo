using System;
using AbyssEcho.Player;
using UnityEngine;

namespace AbyssEcho.Interaction
{
    /// <summary>
    /// 交互检测器：从摄像机中心发射线，找到玩家正在注视的可交互物体。
    ///
    /// 实现要点：
    ///   - 每帧只在有物体进入/离开时触发事件，避免每帧派发造成 UI 抖动
    ///   - 使用 GetComponentInParent 而非只在命中物体上找，
    ///     这样即使准星打在子碰撞体（如按钮的发光面）上也能识别整个可交互物
    ///   - 使用非分配物理查询（Physics.Raycast 返回 RaycastHit 结构体）
    ///
    /// 挂载位置：Main Camera 上
    /// </summary>
    [DisallowMultipleComponent]
    public class InteractionDetector : MonoBehaviour
    {
        [Header("检测参数")]
        [Tooltip("交互距离（米）")]
        public float interactDistance = 2.6f;

        [Tooltip("检测半径：>0 时使用 SphereCast，准星稍偏也能命中，手感更好")]
        public float castRadius = 0.12f;

        [Tooltip("参与检测的层（默认全部；可改为 Interactable 专用层以提升性能）")]
        public LayerMask hitLayers = ~0;

        [Header("调试")]
        public bool debugDrawRay = false;

        /// <summary>当前注视的可交互物（可能为 null）。</summary>
        public IInteractable Current { get; private set; }

        /// <summary>当前是否有可交互目标。</summary>
        public bool HasTarget => Current != null;

        /// <summary>当前目标的提示文本（无目标时为空串）。</summary>
        public string CurrentPrompt => Current != null ? Current.GetPrompt() : string.Empty;

        /// <summary>目标变化时触发（新目标可能为 null）。</summary>
        public event Action<IInteractable> OnTargetChanged;

        private PlayerInputReader _input;
        private Camera _cam;
        private GameObject _previousGo;

        /// <summary>交互请求事件（外部可订阅做音效/动画）。</summary>
        public event Action<IInteractable> OnInteracted;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            if (_cam == null) _cam = Camera.main;
            _input = GetComponentInParent<PlayerInputReader>();
        }

        private void OnEnable()
        {
            if (_input != null) _input.OnInteract += HandleInteractPressed;
        }

        private void OnDisable()
        {
            if (_input != null) _input.OnInteract -= HandleInteractPressed;
        }

        private void Update()
        {
            if (_cam == null) return;

            var found = Probe();

            // S66：正常射线没打到东西时，用"视线下方锥形"兜底，
            //      解决矮物体（灯座 0.8m / 供奉台 1.0m）平视交互不到的问题。
            if (found == null)
                found = ProbeNearbyFallback();

            var foundGo = found as UnityEngine.Object as GameObject;

            // 只在目标变化时派发事件
            if (!ReferenceEquals(found, Current))
            {
                Current?.OnFocusExit();
                Current = found;
                Current?.OnFocusEnter();
                OnTargetChanged?.Invoke(Current);
            }

            if (debugDrawRay)
                Debug.DrawRay(_cam.transform.position, _cam.transform.forward * interactDistance,
                              found != null ? Color.green : Color.gray);
        }

        /// <summary>从准星方向探测可交互物。</summary>
        private IInteractable Probe()
        {
            if (_cam == null) return null;

            Vector3 origin = _cam.transform.position;
            Vector3 dir = _cam.transform.forward;

            // 优先用 SphereCast：准星稍有偏移也能命中，交互手感更好
            bool hit = castRadius > 0f
                ? Physics.SphereCast(origin, castRadius, dir, out var hitInfo, interactDistance,
                                     hitLayers, QueryTriggerInteraction.Collide)
                : Physics.Raycast(origin, dir, out hitInfo, interactDistance,
                                  hitLayers, QueryTriggerInteraction.Collide);

            if (!hit) return null;

            // 从命中物体向上找 IInteractable（支持子碰撞体）
            var interactable = hitInfo.collider.GetComponentInParent<IInteractable>();
            if (interactable == null) return null;
            if (!interactable.CanInteract) return interactable;   // 仍返回，用于显示"不可用"提示

            return interactable;
        }

        /// <summary>
        /// 近距离兜底探测（S66 新增）。
        ///
        /// ══════════════════ 为什么需要 ══════════════════
        ///
        ///   玩家反馈："捡到灯泡之后放不进去"
        ///
        ///   实测原因：
        ///     长明灯座只有 0.8 米高，而相机高度是 1.67 米。
        ///     玩家站在灯座前**平视前方**时，射线从灯座上方 0.87 米处掠过，
        ///     什么都打不到 —— 必须低头 30° 才能命中。
        ///     但没有任何提示告诉玩家"要低头看"。
        ///
        ///   同样的问题也存在：
        ///     供奉台 (高 1.0)  —— 实测平视也打不到
        ///     任何放在地上/矮台面上的物件
        ///
        ///   修复思路：
        ///     在正常射线之外，再做一次"向下锥形"探测：
        ///     从相机位置，沿**视线下方 40°** 方向发一个较粗的 SphereCast。
        ///     这样玩家只要站在矮物体旁边看着它的大致方向就能交互，
        ///     不用精确低头对准。
        ///
        ///   优先级：
        ///     正常射线命中的目标**总是优先**（玩家明确瞄准时以它为准），
        ///     只有正常射线什么都没打到时，才用兜底结果。
        /// </summary>
        private IInteractable ProbeNearbyFallback()
        {
            if (_cam == null) return null;

            Vector3 origin = _cam.transform.position;

            // 从视线正前方向下倾斜 40°，粗一点（0.35）好命中矮物体
            Vector3 dir = Quaternion.AngleAxis(40f, _cam.transform.right) * _cam.transform.forward;
            float reach = Mathf.Min(interactDistance, 2.2f);

            if (!Physics.SphereCast(origin, 0.35f, dir, out var hi, reach,
                                    hitLayers, QueryTriggerInteraction.Collide))
                return null;

            var it = hi.collider.GetComponentInParent<IInteractable>();
            if (it != null)
            {
                float d0 = Vector3.Distance(_cam.transform.position, hi.collider.transform.position);
                return d0 <= 2.4f ? it : null;
            }

            // ══════════════════ S66b：命中"装饰部件"时的兜底 ══════════════════
            //
            //   实测踩坑：
            //     "供奉台" 和 "供奉台面" 是**兄弟节点**（都挂在 Props_F9 下），
            //     不是父子关系。"供奉台面"自己没有 OfferingAltar 组件。
            //
            //     玩家看向供奉台时，射线很容易先命中"供奉台面"，
            //     于是 GetComponentInParent<IInteractable>() 返回 null，
            //     交互直接失败 —— 这正是一直"放不进去"的原因之一。
            //
            //   修复：在这种情况下去附近找最近的可交互物（范围 1.2 米内）。
            //     这样命中台面、灯座的灯罩、容器盖子等装饰部件时，
            //     也能正确找到它们所属的交互体。
            var near = Physics.OverlapSphere(hi.point, 1.2f, hitLayers,
                                             QueryTriggerInteraction.Collide);
            IInteractable best = null;
            float bestDist = float.MaxValue;

            foreach (var c in near)
            {
                if (c == null) continue;
                var cand = c.GetComponentInParent<IInteractable>();
                if (cand == null) continue;

                float cd = Vector3.Distance(_cam.transform.position, c.transform.position);
                if (cd > 2.6f) continue;               // 太远不算
                if (cd < bestDist) { bestDist = cd; best = cand; }
            }

            return best;
        }

        private void HandleInteractPressed()
        {
            if (Current == null) return;
            if (!Current.CanInteract) return;

            Current.OnInteract(_cam != null ? _cam.gameObject : gameObject);
            OnInteracted?.Invoke(Current);

            // 交互后立即刷新检测（避免状态变化后提示滞后）
            var after = Probe();
            if (!ReferenceEquals(after, Current))
            {
                Current?.OnFocusExit();
                Current = after;
                Current?.OnFocusEnter();
                OnTargetChanged?.Invoke(Current);
            }
        }
    }
}