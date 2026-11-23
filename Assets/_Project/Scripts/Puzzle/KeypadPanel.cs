using AbyssEcho.Interaction;
using UnityEngine;

namespace AbyssEcho.Puzzle
{
    /// <summary>
    /// 谜题 2：门禁密码键盘。
    ///
    /// 流程（对应《05 第一章流程》）：
    ///   1. 玩家在 C5 广播室找到纸条，读到密码（4 位数字）
    ///   2. 到 C2 监控室门口的门禁面板交互
    ///   3. 弹出数字键盘 UI，输入 4 位密码
    ///   4. 正确 → 面板变绿、门解锁、"门禁已解除"提示
    ///   5. 错误 → 抖动提示，清空输入
    ///
    /// 设计取舍：
    ///   - 密码硬编码在面板上（可在 Inspector 改），不依赖全局配置
    ///   - 输入错误不惩罚（不锁死），避免玩家卡关
    /// </summary>
    public class KeypadPanel : InteractableBase
    {
        [Header("密码")]
        [Tooltip("正确的 4 位密码")]
        public string correctCode = "1212";

        [Tooltip("输入最大位数")]
        public int maxDigits = 4;

        [Header("联动")]
        [Tooltip("密码正确后解锁的门")]
        public DoorInteractable[] doorsToUnlock;

        [Tooltip("面板指示灯")]
        public Renderer[] indicatorRenderers;
        public Color lockedColor = new Color(0.95f, 0.25f, 0.15f);
        public Color unlockedColor = new Color(0.2f, 1f, 0.45f);

        [Header("提示")]
        public string successMessage = "门禁已解除";
        public string failMessage = "密码错误";

        /// <summary>门禁解除事件。</summary>
        public static event System.Action OnAccessGranted;

        public bool IsUnlocked { get; private set; }

        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        protected override string DefaultPrompt => IsUnlocked ? "门禁已解除" : "输入密码";

        protected override void Awake()
        {
            base.Awake();
            ApplyIndicator(false);
        }

        public override string GetPrompt()
        {
            if (IsUnlocked) return "门禁已解除";
            return "输入门禁密码";
        }

        public override void OnInteract(GameObject interactor)
        {
            if (!CanInteract) return;

            if (IsUnlocked)
            {
                Debug.Log("[Keypad] 门禁已经是解除状态");
                return;
            }

            var ui = FindFirstObjectByType<AbyssEcho.UI.KeypadUI>();
            if (ui == null)
            {
                Debug.LogWarning("[Keypad] 场景中没有 KeypadUI，无法输入密码");
                return;
            }

            ui.Open(this);
        }

        /// <summary>由 KeypadUI 调用：提交输入的密码。</summary>
        /// <summary>解锁所有关联的门（幂等，可重复调用）。</summary>
        private void UnlockDoors()
        {
            if (doorsToUnlock == null) return;
            foreach (var d in doorsToUnlock)
                if (d != null) d.locked = false;
        }

        public bool Submit(string input)
        {
            // ⚠️ S25 修复：已解锁时也要确保门是解锁的。
            //   原来这里直接 return true，什么都不做。
            //   如果存档恢复了 IsUnlocked=true 但门因为别的原因又被锁上，
            //   玩家会陷入"密码正确但门打不开"的死锁。
            if (IsUnlocked)
            {
                UnlockDoors();
                return true;
            }

            if (input == correctCode)
            {
                IsUnlocked = true;
                ApplyIndicator(true);
                UnlockDoors();

                Debug.Log("[Keypad] 密码正确，门禁已解除");
                OnAccessGranted?.Invoke();

                if (AbyssEcho.Audio.AudioManager.Instance != null)
                    AbyssEcho.Audio.AudioManager.Instance.PlayUiBlip();
                if (AbyssEcho.Horror.FearSystem.Instance != null)
                    AbyssEcho.Horror.FearSystem.Instance.AddFear(-0.12f);

                OnFocusExit();
                return true;
            }

            Debug.Log("[Keypad] 密码错误：" + input);

            // 错误音 + 恐惧上升（被拒绝会让人更紧张）
            if (AbyssEcho.Audio.AudioManager.Instance != null)
                AbyssEcho.Audio.AudioManager.Instance.PlayError();
            if (AbyssEcho.Horror.FearSystem.Instance != null)
                AbyssEcho.Horror.FearSystem.Instance.AddFear(0.04f);

            return false;
        }

        private void ApplyIndicator(bool unlocked)
        {
            if (indicatorRenderers == null) return;

            var mpb = new MaterialPropertyBlock();
            Color c = unlocked ? unlockedColor * 2.2f : lockedColor * 1.6f;
            foreach (var r in indicatorRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor(EmissionId, c);
                r.SetPropertyBlock(mpb);
            }
        }
    }
}
