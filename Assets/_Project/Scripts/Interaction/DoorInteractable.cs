using UnityEngine;

namespace AbyssEcho.Interaction
{
    /// <summary>
    /// 可交互门：支持上锁、需要道具、以及"开启后保持开启"。
    ///
    /// 运动方式：
    ///   - 滑动门（默认）：沿本地 X 轴平移
    ///   - 摆动门：绕本地 Y 轴旋转（当 swingAngle != 0 时启用）
    ///
    /// 解锁条件（按顺序检查）：
    ///   1. locked == true 且需要道具 → 检查背包
    ///   2. 不满足则提示"需要 XX"
    ///
    /// 注意：本类的碰撞体在动画期间保持开启，避免玩家被卡住；
    ///       门板移开后碰撞体同时移开，通路自然打开。
    /// </summary>
    public class DoorInteractable : InteractableBase
    {
        [Header("状态")]
        [Tooltip("是否上锁")]
        public bool locked = false;

        [Tooltip("开门所需物品 ID（留空表示无需物品）")]
        public string requiredItemId = "";

        [Tooltip("上锁时的提示文本")]
        public string lockedPrompt = "门已锁";

        [Tooltip("是否允许玩家按交互键直接解锁（谜题门应设为 false，只能由谜题脚本解锁）")]
        public bool unlockableByInteraction = true;

        [Header("运动")]
        [Tooltip("滑动门：开启时沿本地 X 平移的距离（米）")]
        public float slideDistance = 1.1f;

        [Tooltip("摆动门：开启角度（度）；非 0 时改用旋转方式")]
        public float swingAngle = 0f;

        [Tooltip("开合速度（米/秒 或 度/秒）")]
        public float speed = 2.2f;

        [Header("滑动方向")]
        [Tooltip("自动按门板宽度方向滑动（推荐开启：门宽沿 X 就沿 X 滑，门宽沿 Z 就沿 Z 滑）")]
        public bool autoSlideAxis = true;

        [Tooltip("关闭自动时生效：true = 沿 Z 轴滑动，false = 沿 X 轴滑动")]
        public bool slideAlongZ = false;

        [Header("声音/事件（可选）")]
        public bool logOpenClose = true;

        /// <summary>门状态变化事件（isOpen）。</summary>
        public static event System.Action<DoorInteractable, bool> OnDoorStateChanged;

        public bool IsOpen { get; private set; }

        private Vector3 _closedPos;
        private Quaternion _closedRot;
        private Vector3 _openPos;
        private Quaternion _openRot;
        private bool _moving;

        protected override string DefaultPrompt
        {
            get
            {
                if (IsOpen) return "关闭门";
                if (locked) return lockedPrompt;
                return "打开门";
            }
        }

        protected override void Awake()
        {
            base.Awake();
            _closedPos = transform.localPosition;
            _closedRot = transform.localRotation;

            // ⚠️ S23 修复（玩家实测"打开门禁后门卡住，进不去房间"）：
            //
            //   原来固定沿本地 X 轴滑动：
            //       _openPos = _closedPos + localRot * Vector3(slideDistance, 0, 0)
            //
            //   这对"门宽沿 X"的门（如 Door_A3_B：X=2.0 宽 / Z=0.12 厚）是对的。
            //   但 Door_C2 是"门宽沿 Z"（X=0.12 厚 / Z=2.0 宽），
            //   沿 X 滑动等于让门板往墙里钻 —— 门洞照样被封死，玩家过不去。
            //
            //   修复：自动判断该沿哪个轴滑动。
            //     slideAlongZ == false（默认）：沿 X 滑动（保持旧行为，不破坏已有门）
            //     slideAlongZ == true         ：沿 Z 滑动
            //   若未显式指定，则按门板"宽度方向"自动推断。
            float widthX = Mathf.Abs(transform.localScale.x);
            float widthZ = Mathf.Abs(transform.localScale.z);

            Vector3 dir;
            if (autoSlideAxis)
                dir = (widthX >= widthZ) ? Vector3.right : Vector3.forward;
            else
                dir = slideAlongZ ? Vector3.forward : Vector3.right;

            _openPos = _closedPos + transform.localRotation * (dir * slideDistance);
            _openRot = _closedRot * Quaternion.Euler(0f, swingAngle, 0f);

            Debug.Log($"[Door] {name} 滑动轴={(dir == Vector3.right ? "X" : "Z")} " +
                      $"距离={slideDistance:F2} 尺寸=({widthX:F2},{widthZ:F2})");
        }

        public override void OnInteract(GameObject interactor)
        {
            if (!CanInteract || _moving) return;

if (!IsOpen)
            {
                // 尝试开门：检查锁
                if (locked)
                {
                    // ⚠️ S18 修复：谜题门不允许玩家直接解锁。
                    //    曾经的 bug：locked 且 requiredItemId 为空时，
                    //    代码直接执行 locked = false，玩家按 E 就开门，
                    //    导致"配电箱 / 密钥盘"两个谜题形同虚设。
                    if (!unlockableByInteraction)
                    {
                        Debug.Log($"[Door] {name} 需要解谜才能解锁");
                        if (AbyssEcho.Audio.AudioManager.Instance != null)
                            AbyssEcho.Audio.AudioManager.Instance.PlayError();
                        return;
                    }

                    if (!string.IsNullOrEmpty(requiredItemId))
                    {
                        var inv = AbyssEcho.Inventory.PlayerInventory.Instance;
                        bool has = inv != null && inv.Has(requiredItemId);
                        if (!has)
                        {
                            Debug.Log($"[Door] 需要 {requiredItemId} 才能打开");
                            return;   // 提示由 GetPrompt 显示
                        }

                        // 消耗钥匙（若希望钥匙保留，把下一行删掉）
                        // inv.Remove(requiredItemId);
                    }
                    locked = false;
                }
            }

            IsOpen = !IsOpen;
            _moving = true;

            if (logOpenClose)
                Debug.Log("[Door] " + (IsOpen ? "开启" : "关闭") + "：" + name);

            // 播放金属门滑动音（3D，跟随门的实际位置）
            if (AbyssEcho.Audio.AudioManager.Instance != null)
                AbyssEcho.Audio.AudioManager.Instance.PlayDoorSlide(transform.position, 0.75f);

            // 开门瞬间略微增加紧张感（门后可能是未知）
            if (IsOpen && AbyssEcho.Horror.FearSystem.Instance != null)
                AbyssEcho.Horror.FearSystem.Instance.AddFear(0.05f);

            OnDoorStateChanged?.Invoke(this, IsOpen);
        }

        public override string GetPrompt()
        {
            if (!interactable) return "无法使用";
            if (IsOpen) return "关闭门";

            if (locked)
            {
                if (!string.IsNullOrEmpty(requiredItemId))
                {
                    var inv = AbyssEcho.Inventory.PlayerInventory.Instance;
                    bool has = inv != null && inv.Has(requiredItemId);
                    return has ? "使用 " + requiredItemId + " 开门" : lockedPrompt + "（需要 " + requiredItemId + "）";
                }
                return lockedPrompt;
            }
            return "打开门";
        }

        private void Update()
        {
            if (!_moving) return;

            if (Mathf.Abs(swingAngle) > 0.01f)
            {
                Quaternion target = IsOpen ? _openRot : _closedRot;
                transform.localRotation = Quaternion.RotateTowards(transform.localRotation, target, speed * 60f * Time.deltaTime);
                if (Quaternion.Angle(transform.localRotation, target) < 0.5f)
                {
                    transform.localRotation = target;
                    _moving = false;
                }
            }
            else
            {
                Vector3 target = IsOpen ? _openPos : _closedPos;
                transform.localPosition = Vector3.MoveTowards(transform.localPosition, target, speed * Time.deltaTime);
                if (Vector3.Distance(transform.localPosition, target) < 0.005f)
                {
                    transform.localPosition = target;
                    _moving = false;
                }
            }
        }
    }
}
