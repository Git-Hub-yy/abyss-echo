using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AbyssEcho.Player
{
    /// <summary>
    /// 玩家输入的唯一入口（S2）。
    ///
    /// 设计要点：
    ///  - 全工程只有这里读取输入，其它模块只订阅事件或读取属性。
    ///  - 业务代码不感知"按键"，只感知"动作"，日后改键位/加手柄不影响逻辑。
    ///  - 依赖 Input System 的 InputAction C# 事件，不使用 SendMessage。
    ///
    /// ⚠️ 改动约定：本类不引用任何其它模块（保持依赖方向 Core ← Player）。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerInputReader : MonoBehaviour
    {
        [Header("输入资源")]
        [Tooltip("Assets/_Project/Input/AbyssEchoPlayer.inputactions")]
        [SerializeField] private InputActionAsset actionsAsset;

        [Header("输入开关")]
        [Tooltip("关闭后所有输入被忽略（暂停/阅读界面时由 GameManager 调用）")]
        [SerializeField] private bool inputEnabled = true;

        // ── 持续型输入（每帧读取） ──
        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool RunHeld { get; private set; }
        public bool CrouchHeld { get; private set; }

        // ── 瞬时型输入（本帧触发一次） ──
        public bool InteractPressed { get; private set; }
        public bool JournalPressed { get; private set; }

        // ── 事件（订阅方自行处理） ──
        /// <summary>交互键按下（E）。</summary>
        public event Action OnInteract;
        /// <summary>手电筒键按下（F）。</summary>
        public event Action OnFlashlightToggle;
        /// <summary>跳跃键按下（空格）。</summary>
        public event Action OnJump;
        /// <summary>暂停键按下（Esc）。</summary>
        public event Action OnPause;

        private InputActionMap _playerMap;
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _runAction;
        private InputAction _crouchAction;
        private InputAction _jumpAction;
        private InputAction _interactAction;
        private InputAction _flashlightAction;
        private InputAction _pauseAction;
        private InputAction _journalAction;

        private bool _bound;

        /// <summary>输入是否可用。为 false 时 Move/Look 归零，且不触发事件。</summary>
        public bool InputEnabled
        {
            get => inputEnabled;
            set
            {
                inputEnabled = value;
                if (!value)
                {
                    Move = Vector2.zero;
                    Look = Vector2.zero;
                    RunHeld = false;
                    CrouchHeld = false;
                }
                RefreshCursor();
            }
        }

        private void Awake()
        {
            BindActions();
        }

        private void OnEnable()
        {
            if (!_bound) BindActions();
            _playerMap?.Enable();
            RefreshCursor();
        }

        private void OnDisable()
        {
            _playerMap?.Disable();
        }

        private void OnDestroy()
        {
            UnbindActions();
        }

        private void Update()
        {
            if (!inputEnabled || _playerMap == null)
            {
                Move = Vector2.zero;
                Look = Vector2.zero;
                InteractPressed = false;
                JournalPressed = false;
                return;
            }

            Move = _moveAction.ReadValue<Vector2>();
            Look = _lookAction.ReadValue<Vector2>();
            RunHeld = _runAction.IsPressed();
            CrouchHeld = _crouchAction.IsPressed();
            InteractPressed = _interactAction.WasPressedThisFrame();
            JournalPressed = _journalAction.WasPressedThisFrame();
        }

        // ────────────────────────── 绑定 ──────────────────────────

        private void BindActions()
        {
            if (_bound) return;

            if (actionsAsset == null)
            {
                Debug.LogError("[PlayerInputReader] 未指定 InputActionAsset，玩家将无法操作。" +
                               "请在 Inspector 中拖入 Assets/_Project/Input/AbyssEchoPlayer.inputactions。");
                return;
            }

            _playerMap = actionsAsset.FindActionMap("Player", throwIfNotFound: true);
            if (_playerMap == null)
            {
                Debug.LogError("[PlayerInputReader] 输入资源中找不到名为 \"Player\" 的 Action Map。");
                return;
            }

            _moveAction       = _playerMap.FindAction("Move", true);
            _lookAction       = _playerMap.FindAction("Look", true);
            _runAction        = _playerMap.FindAction("Run", true);
            _crouchAction     = _playerMap.FindAction("Crouch", true);
            _jumpAction       = _playerMap.FindAction("Jump", true);
            _interactAction   = _playerMap.FindAction("Interact", true);
            _flashlightAction = _playerMap.FindAction("Flashlight", true);
            _pauseAction      = _playerMap.FindAction("Pause", true);
            _journalAction    = _playerMap.FindAction("Journal", true);

            _jumpAction.performed       += HandleJump;
            _interactAction.performed   += HandleInteract;
            _flashlightAction.performed += HandleFlashlight;
            _pauseAction.performed      += HandlePause;

            _bound = true;
        }

        private void UnbindActions()
        {
            if (!_bound) return;
            _jumpAction.performed       -= HandleJump;
            _interactAction.performed   -= HandleInteract;
            _flashlightAction.performed -= HandleFlashlight;
            _pauseAction.performed      -= HandlePause;
            _bound = false;
        }

        // ────────────────────────── 事件转发 ──────────────────────────

        private void HandleJump(InputAction.CallbackContext ctx)
        {
            if (!inputEnabled) return;
            OnJump?.Invoke();
        }

        private void HandleInteract(InputAction.CallbackContext ctx)
        {
            if (!inputEnabled) return;
            OnInteract?.Invoke();
        }

        private void HandleFlashlight(InputAction.CallbackContext ctx)
        {
            if (!inputEnabled) return;
            OnFlashlightToggle?.Invoke();
        }

        private void HandlePause(InputAction.CallbackContext ctx)
        {
            // 暂停键即使 inputEnabled 为 false 也应可用，否则无法从暂停中恢复。
            OnPause?.Invoke();
        }

        // ────────────────────────── 光标 ──────────────────────────

        /// <summary>游戏中锁定并隐藏光标；输入关闭时释放（用于暂停/UI）。</summary>
        public void RefreshCursor()
        {
            if (Application.isBatchMode) return;   // 无界面模式下不处理光标

            if (inputEnabled)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }
}