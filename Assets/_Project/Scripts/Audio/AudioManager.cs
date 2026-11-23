using System.Collections.Generic;
using UnityEngine;

namespace AbyssEcho.Audio
{
    /// <summary>
    /// 音频管理器：统一管理所有程序化合成的音效播放。
    ///
    /// 结构：
    ///   · 3D 音效池（脚步、门）：跟随世界坐标，有空间衰减
    ///   · 2D 音效池（心跳、UI、惊吓）：不随位置变化
    ///   · 环境底噪：独立 AudioSource，循环播放
    ///
    /// 为什么用对象池：
    ///   恐怖游戏会频繁播放脚步（每秒 2 次以上），
    ///   每次 new AudioSource 会产生 GC 压力并可能卡顿。
    ///
    /// 音量安全：
    ///   所有音效音量都压低（尤其 Stinger），避免惊吓时音量过载伤耳。
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("音量（0-1）")]
        [Range(0f, 1f)] public float masterVolume = 0.8f;
        [Range(0f, 1f)] public float sfxVolume = 0.85f;
        [Range(0f, 1f)] public float ambientVolume = 0.35f;

        [Header("3D 音效池大小")]
        public int spatialPoolSize = 8;

        [Header("环境底噪")]
        public bool playAmbientDrone = true;

        // ── 音效资源 ──
        private AudioClip[] _footstepsMetal;
        private AudioClip[] _footstepsConcrete;
        private AudioClip _doorSlide;
        private AudioClip _stinger;
        private AudioClip _heartbeat;
        private AudioClip _uiBlip;
        private AudioClip _errorBuzz;
        private AudioClip _ambientDrone;

        // ── 播放源 ──
        private readonly List<AudioSource> _spatialPool = new List<AudioSource>();
        private AudioSource _twoD;
        private AudioSource _heartbeatSource;
        private AudioSource _ambientSource;

        /// <summary>
        /// 录音专用音源（S30 新增）。
        ///
        /// ⚠️ 为什么必须独立：
        ///   原来的 PlayAt() 会复用同一个 AudioSource：
        ///       src.clip = clip;  src.Play();
        ///   如果录音也用 PlayAt，它会**直接中断**正在播放的脚步声/门声，
        ///   玩家听到的就是"点了录音之后其它声音全没了"。
        ///
        ///   录音是"长音"（10 秒左右），必须有自己的通道，
        ///   既不打断别人，也不被别人打断。
        /// </summary>
        private AudioSource _tapeSource;

        private Transform _poolRoot;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            BuildClips();
            BuildSources();

            if (playAmbientDrone) PlayAmbient();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ────────────────────────── 初始化 ──────────────────────────

        private void BuildClips()
        {
            // 脚步做 4 个变体，避免重复听感
            _footstepsMetal = new AudioClip[4];
            _footstepsConcrete = new AudioClip[4];
            for (int i = 0; i < 4; i++)
            {
                _footstepsMetal[i] = ProceduralAudio.CreateFootstep(true, i);
                _footstepsConcrete[i] = ProceduralAudio.CreateFootstep(false, i);
            }

            _doorSlide = ProceduralAudio.CreateDoorSlide();
            _stinger = ProceduralAudio.CreateStinger();
            _heartbeat = ProceduralAudio.CreateHeartbeat(88f, 1f);
            _uiBlip = ProceduralAudio.CreateUiBlip();
            _errorBuzz = ProceduralAudio.CreateErrorBuzz();
            _ambientDrone = ProceduralAudio.CreateAmbientDrone();

            Debug.Log("[AudioManager] 程序化音效已生成");
        }

        private void BuildSources()
        {
            _poolRoot = new GameObject("SpatialPool").transform;
            _poolRoot.SetParent(transform, false);

            for (int i = 0; i < spatialPoolSize; i++)
            {
                var go = new GameObject("Spatial_" + i);
                go.transform.SetParent(_poolRoot, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 1f;          // 完全 3D
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 1.5f;
                src.maxDistance = 22f;
                src.dopplerLevel = 0f;
                _spatialPool.Add(src);
            }

            var twoDGo = new GameObject("TwoD");
            twoDGo.transform.SetParent(transform, false);
            _twoD = twoDGo.AddComponent<AudioSource>();
            _twoD.playOnAwake = false;
            _twoD.spatialBlend = 0f;

            var hbGo = new GameObject("Heartbeat");
            hbGo.transform.SetParent(transform, false);
            _heartbeatSource = hbGo.AddComponent<AudioSource>();
            _heartbeatSource.playOnAwake = false;
            _heartbeatSource.spatialBlend = 0f;
            _heartbeatSource.loop = true;
            _heartbeatSource.volume = 0f;
            _heartbeatSource.clip = _heartbeat;
            _heartbeatSource.Play();

            var ambGo = new GameObject("Ambient");
            ambGo.transform.SetParent(transform, false);
            _ambientSource = ambGo.AddComponent<AudioSource>();
            _ambientSource.playOnAwake = false;
            _ambientSource.spatialBlend = 0f;
            _ambientSource.loop = true;
            _ambientSource.clip = _ambientDrone;

            // 录音专用通道（S30）：独立，不与任何其它音效争用
            var tapeGo = new GameObject("TapeChannel");
            tapeGo.transform.SetParent(transform, false);
            _tapeSource = tapeGo.AddComponent<AudioSource>();
            _tapeSource.playOnAwake = false;
            _tapeSource.spatialBlend = 0f;
            _tapeSource.loop = false;
        }

        /// <summary>
        /// 播放录音音频（S30）。
        ///
        /// 用独立音源，不打断任何正在播放的音效。
        /// clip 由 ProceduralAudio.CreateTapeVoice 生成。
        /// </summary>
        public void PlayTape(AudioClip clip, float volume = 0.75f)
        {
            if (clip == null || _tapeSource == null) return;

            _tapeSource.Stop();
            _tapeSource.clip = clip;
            _tapeSource.volume = Mathf.Clamp01(volume) * sfxVolume * masterVolume;
            _tapeSource.Play();
        }

        /// <summary>停止录音音频。</summary>
        public void StopTape()
        {
            if (_tapeSource != null) _tapeSource.Stop();
        }

        /// <summary>录音是否正在播放。</summary>
        public bool IsTapePlaying => _tapeSource != null && _tapeSource.isPlaying;

        // ────────────────────────── 公开播放接口 ──────────────────────────

        /// <summary>在指定位置播放 3D 音效。</summary>
        public void PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float pitchJitter = 0.06f)
        {
            if (clip == null) return;

            var src = GetFreeSpatialSource();
            if (src == null) return;

            src.transform.position = position;
            src.clip = clip;
            src.volume = Mathf.Clamp01(volume) * sfxVolume * masterVolume;
            src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            src.Play();
        }

        /// <summary>播放 2D 音效（UI、心跳类）。</summary>
        public void Play2D(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;
            _twoD.pitch = pitch;
            _twoD.PlayOneShot(clip, Mathf.Clamp01(volume) * sfxVolume * masterVolume);
        }

        /// <summary>播放脚步声。</summary>
        public void PlayFootstep(Vector3 position, bool metalSurface)
        {
            var arr = metalSurface ? _footstepsMetal : _footstepsConcrete;
            if (arr == null || arr.Length == 0) return;
            PlayAt(arr[Random.Range(0, arr.Length)], position, 0.55f, 0.12f);
        }

        /// <summary>播放门滑动声。</summary>
        public void PlayDoorSlide(Vector3 position, float volume = 0.8f)
        {
            PlayAt(_doorSlide, position, volume, 0.04f);
        }

        /// <summary>播放突发惊吓音（音量已压低）。</summary>
        public void PlayStinger(float volume = 0.55f)
        {
            Play2D(_stinger, volume);
        }

        /// <summary>播放 UI 提示音。</summary>
        public void PlayUiBlip() => Play2D(_uiBlip, 0.5f);

        /// <summary>播放错误音。</summary>
        public void PlayError() => Play2D(_errorBuzz, 0.6f);

        /// <summary>设置心跳强度（0 = 静音，1 = 最大）。</summary>
        public void SetHeartbeatIntensity(float intensity)
        {
            if (_heartbeatSource == null) return;
            float target = Mathf.Clamp01(intensity) * 0.55f * masterVolume;
            _heartbeatSource.volume = Mathf.MoveTowards(_heartbeatSource.volume, target, Time.deltaTime * 0.8f);
        }

        /// <summary>开始环境底噪。</summary>
        public void PlayAmbient()
        {
            if (_ambientSource == null || _ambientSource.isPlaying) return;
            _ambientSource.volume = ambientVolume * masterVolume;
            _ambientSource.Play();
        }

        /// <summary>停止环境底噪。</summary>
        public void StopAmbient()
        {
            if (_ambientSource != null) _ambientSource.Stop();
        }

        // ────────────────────────── 内部 ──────────────────────────

        private AudioSource GetFreeSpatialSource()
        {
            // 优先找空闲的
            foreach (var s in _spatialPool)
                if (!s.isPlaying) return s;

            // 都在播：复用第一个（脚步这类短音效抢占影响不大）
            return _spatialPool.Count > 0 ? _spatialPool[0] : null;
        }
    }
}