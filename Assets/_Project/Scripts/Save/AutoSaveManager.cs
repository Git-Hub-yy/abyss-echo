using System.Collections;
using UnityEngine;

namespace AbyssEcho.Save
{
    /// <summary>
    /// 自动存档：在关键节点自动保存，避免玩家丢失进度。
    ///
    /// 触发时机：
    ///   · 恢复电力后
    ///   · 解除门禁后
    ///   · 拾取重要物品后（延迟 1 秒，避免与音效冲突）
    ///
    /// 为什么不做"定时保存"：
    ///   恐怖游戏里定时保存会打断沉浸感（存档提示突然弹出）。
    ///   改为事件驱动，只在玩家完成里程碑时静默保存。
    /// </summary>
    public class AutoSaveManager : MonoBehaviour
    {
        [Header("设置")]
        [Tooltip("是否启用自动存档")]
        public bool enableAutoSave = true;

        [Tooltip("拾取物品后延迟多久保存（秒）")]
        public float pickupSaveDelay = 1.0f;

        [Tooltip("是否在屏幕上提示已保存")]
        public bool showSaveToast = true;

        private float _pendingSaveTime = -1f;
        private string _toast = "";
        private float _toastUntil;

        private void OnEnable()
        {
            AbyssEcho.Puzzle.PowerRestorePanel.OnPowerRestored += HandlePowerRestored;
            AbyssEcho.Puzzle.KeypadPanel.OnAccessGranted += HandleAccessGranted;
            AbyssEcho.Interaction.PickupItem.OnPickedUp += HandlePickedUp;
        }

        private void OnDisable()
        {
            AbyssEcho.Puzzle.PowerRestorePanel.OnPowerRestored -= HandlePowerRestored;
            AbyssEcho.Puzzle.KeypadPanel.OnAccessGranted -= HandleAccessGranted;
            AbyssEcho.Interaction.PickupItem.OnPickedUp -= HandlePickedUp;
        }

        private void Start()
        {
            // 进入场景后尝试恢复存档
            if (SaveSystem.HasSave())
            {
                var data = SaveSystem.Load();
                if (data != null)
                {
                    SaveSystem.Apply(data);
                    Toast("已恢复上次进度");
                }
            }
        }

        private void HandlePowerRestored() => ScheduleSave("电力恢复");
        private void HandleAccessGranted() => ScheduleSave("门禁解除");

        private void HandlePickedUp(string itemId)
        {
            _pendingSaveTime = Time.time + pickupSaveDelay;
        }

        private void ScheduleSave(string reason)
        {
            if (!enableAutoSave) return;

            string msg;
            if (SaveSystem.Save(out msg))
            {
                Debug.Log($"[AutoSave] {reason} → 已保存");
                Toast("进度已保存");
            }
        }

        private void Update()
        {
            if (_pendingSaveTime > 0f && Time.time >= _pendingSaveTime)
            {
                _pendingSaveTime = -1f;
                ScheduleSave("拾取物品");
            }
        }

        private void Toast(string text)
        {
            if (!showSaveToast) return;
            _toast = text;
            _toastUntil = Time.time + 2.0f;
        }

        private GUIStyle _style;

        private void OnGUI()
        {
            if (!showSaveToast) return;
            if (Time.time >= _toastUntil || string.IsNullOrEmpty(_toast)) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    alignment = TextAnchor.MiddleCenter
                };
                _style.normal.textColor = new Color(0.55f, 0.80f, 0.62f);
                AbyssEcho.UI.ChineseFont.Apply(_style);
            }

            float alpha = Mathf.Clamp01((_toastUntil - Time.time) / 0.5f);
            var prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(0f, Screen.height - 70f, Screen.width, 24f), _toast, _style);
            GUI.color = prev;
        }
    }
}