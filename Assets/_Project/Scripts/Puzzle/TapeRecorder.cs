using System;
using System.Collections.Generic;
using AbyssEcho.Interaction;
using UnityEngine;

namespace AbyssEcho.Puzzle
{
    /// <summary>
    /// 录音机（第二章 D6 卧房）。
    ///
    /// 播放一段录音，同时把内容记入日志。
    ///
    /// 设计意图：
    ///   · 录音比纯文本笔记更有"人曾经在这里"的实感
    ///   · 三段录音拼起来，揭示"回声"的真相和声波谜题的答案
    ///   · 每台录音机只播一次（再交互会重播，但不再重复记日志）
    ///
    /// 音频：
    ///   使用程序化生成的"磁带噪音 + 起伏"作为底噪，
    ///   文字以 OnGUI 逐字显示——不需要真人配音，也不依赖外部音频。
    /// </summary>
    public class TapeRecorder : InteractableBase
    {
        [Header("内容")]
        [Tooltip("录音编号（用于日志去重）")]
        public string tapeId = "tape.1";

        [Tooltip("录音标题")]
        public string tapeTitle = "录音 01";

        [Tooltip("录音全文（会逐字显示）")]
        [TextArea(3, 10)]
        public string transcript = "";

        [Tooltip("是否同时记入日志")]
        public bool addToJournal = true;

        [Header("播放设置")]
        [Tooltip("文字显示速度（字/秒，0 = 立即全部显示）")]
        public float charPerSecond = 28f;

        [Tooltip("显示总时长（秒），播完自动关闭")]
        public float displayDuration = 12f;

        [Header("视觉")]
        [Tooltip("播放时亮起的指示灯")]
        public Renderer[] indicatorRenderers;
        public Color playingColor = new Color(1f, 0.45f, 0.15f);

        /// <summary>是否已经听过。</summary>
        public bool HasPlayed { get; private set; }

        /// <summary>当前是否正在播放。</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>某段录音播放事件（tapeId）。</summary>
        public static event Action<string> OnTapePlayed;

        private float _playTimer;
        private float _charProgress;

        private GUIStyle _titleStyle, _bodyStyle, _hintStyle;
        private Texture2D _white;

        private AbyssEcho.Player.PlayerMotor _motor;
        private AbyssEcho.Player.PlayerLook _look;
        private AbyssEcho.Player.PlayerInputReader _input;
        private AbyssEcho.Interaction.InteractionDetector _detector;

        // S30：录音音频（程序化生成，按 tapeId 区分）
        private AudioClip _voiceClip;
        private AudioClip _clickClip;

        protected override string DefaultPrompt => HasPlayed ? "重听录音" : "播放录音";

        protected override void Awake()
        {
            base.Awake();
            _white = Texture2D.whiteTexture;
            _motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            _look = FindFirstObjectByType<AbyssEcho.Player.PlayerLook>();
            _input = FindFirstObjectByType<AbyssEcho.Player.PlayerInputReader>();
            _detector = FindFirstObjectByType<AbyssEcho.Interaction.InteractionDetector>();
        }

        private void Start()
        {
            // S30：生成这段录音专属的音频
            //   用 tapeId 作种子 —— 同一段录音每次听都一样，
            //   不同录音听起来音高起伏不同。
            _voiceClip = AbyssEcho.Audio.ProceduralAudio.CreateTapeVoice(
                string.IsNullOrEmpty(tapeId) ? gameObject.name : tapeId,
                Mathf.Max(4f, displayDuration));
            _clickClip = AbyssEcho.Audio.ProceduralAudio.CreateTapeClick();
        }

        public override void OnInteract(GameObject interactor)
        {
            if (IsPlaying) return;

            IsPlaying = true;
            _playTimer = 0f;
            _charProgress = 0f;

            // 冻结玩家
            if (_motor != null) _motor.enabled = false;
            if (_look != null) _look.enabled = false;
            if (_detector != null) _detector.enabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // 记入日志
            if (addToJournal && !HasPlayed)
            {
                var journal = AbyssEcho.Narrative.JournalSystem.Instance;
                if (journal != null) journal.AddEntry(tapeTitle, transcript);
            }

            if (!HasPlayed)
            {
                HasPlayed = true;
                OnTapePlayed?.Invoke(tapeId);
            }

            // ⚠️ S30 修复（玩家实测"录音没声音"）：
            //   原来这里只播了一声 UI 提示音，之后就全是文字，
            //   完全没有"这是录音"的听觉线索。
            //   现在先用独立通道播机械咔哒声，再播人声质感音频。
            var am = AbyssEcho.Audio.AudioManager.Instance;
            if (am != null)
            {
                if (_clickClip != null) am.Play2D(_clickClip, 0.55f);
                if (_voiceClip != null) am.PlayTape(_voiceClip, 0.75f);
            }

            UpdateIndicator(true);
            Debug.Log($"[TapeRecorder] 播放 {tapeTitle}（音频={(_voiceClip != null ? _voiceClip.length.ToString("F1") + "秒" : "无")}）");
        }

        private void Update()
        {
            if (!IsPlaying) return;

            _playTimer += Time.deltaTime;
            _charProgress += Time.deltaTime * Mathf.Max(1f, charPerSecond);

            // Esc 提前结束
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E))
            {
                Stop();
                return;
            }

            if (_playTimer >= displayDuration) Stop();
        }

        /// <summary>停止播放，恢复玩家控制。</summary>
        public void Stop()
        {
            if (!IsPlaying) return;
            IsPlaying = false;

            // S30：停止录音音频，否则玩家走开后声音还在响
            if (AbyssEcho.Audio.AudioManager.Instance != null)
                AbyssEcho.Audio.AudioManager.Instance.StopTape();

            if (_motor != null) _motor.enabled = true;
            if (_look != null) _look.enabled = true;
            if (_detector != null) _detector.enabled = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (_input != null) _input.RefreshCursor();

            UpdateIndicator(false);
        }

        private void OnDisable()
        {
            if (IsPlaying) Stop();
        }

        private void UpdateIndicator(bool on)
        {
            if (indicatorRenderers == null) return;

            foreach (var r in indicatorRenderers)
            {
                if (r == null) continue;
                var mat = r.material;
                var c = on ? playingColor : new Color(0.12f, 0.10f, 0.09f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                mat.color = c;
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", c * (on ? 2.2f : 0.2f));
                }
            }
        }

        // ────────────────────────── 绘制 ──────────────────────────

        private void OnGUI()
        {
            if (!IsPlaying) return;
            EnsureStyles();

            // 底部字幕区域
            float w = Mathf.Min(900f, Screen.width - 120f);
            float h = 190f;
            var panel = new Rect((Screen.width - w) * 0.5f, Screen.height - h - 70f, w, h);

            var prev = GUI.color;
            GUI.color = new Color(0.03f, 0.04f, 0.06f, 0.92f);
            GUI.DrawTexture(panel, _white);

            // 顶部细线（磁带风格）
            GUI.color = new Color(1f, 0.45f, 0.15f, 0.8f);
            GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 2f), _white);
            GUI.color = prev;

            var inner = new Rect(panel.x + 26, panel.y + 18, panel.width - 52, panel.height - 32);

            _titleStyle.normal.textColor = new Color(1f, 0.55f, 0.28f);
            GUI.Label(new Rect(inner.x, inner.y, inner.width, 28), tapeTitle, _titleStyle);

            // 逐字显示
            int n = Mathf.Clamp(Mathf.FloorToInt(_charProgress), 0, transcript.Length);
            string shown = transcript.Substring(0, n);

            _bodyStyle.normal.textColor = new Color(0.88f, 0.90f, 0.93f);
            GUI.Label(new Rect(inner.x, inner.y + 36, inner.width, inner.height - 70), shown, _bodyStyle);

            _hintStyle.normal.textColor = new Color(0.45f, 0.50f, 0.56f);
            GUI.Label(new Rect(inner.x, inner.yMax - 22, inner.width, 20), "按 E 或 Esc 结束", _hintStyle);
        }

        private void EnsureStyles()
        {
            if (_titleStyle == null)
            {
                _titleStyle = AbyssEcho.UI.ChineseFont.CreateStyle(19, FontStyle.Bold);
                AbyssEcho.UI.ChineseFont.Apply(_titleStyle);
            }
            if (_bodyStyle == null)
            {
                _bodyStyle = AbyssEcho.UI.ChineseFont.CreateStyle(17, FontStyle.Normal,
                    TextAnchor.UpperLeft, true);
                AbyssEcho.UI.ChineseFont.Apply(_bodyStyle);
            }
            if (_hintStyle == null)
            {
                _hintStyle = AbyssEcho.UI.ChineseFont.CreateStyle(13);
                _hintStyle.alignment = TextAnchor.MiddleRight;
                AbyssEcho.UI.ChineseFont.Apply(_hintStyle);
            }
        }
    }
}