using System.Collections.Generic;
using UnityEngine;

namespace AbyssEcho.Horror
{
    /// <summary>
    /// 镜沼倒影（第三章 F7 核心演出）。
    ///
    /// ══════════════════════ 设计思路 ══════════════════════
    ///
    ///  玩家走进镜沼水面，能看到自己的倒影。
    ///  **但倒影会做你没做的事。**
    ///
    ///  三个阶段：
    ///    ① Normal  —— 延迟 0.3 秒正常模仿（"哦，是倒影"）
    ///    ② Lagged  —— 延迟涨到 1.5 秒（"它好像慢了很多"）
    ///    ③ Anomaly —— 停止跟随，自己转头看向玩家（"它不是倒影"）
    ///
    /// ══════════════════════ 技术方案 ══════════════════════
    ///
    ///  ⚠️ 为什么不用"第二相机 + RenderTexture"做真反射：
    ///    真反射只能忠实复制玩家动作，**做不到"倒影做你没做的事"** ——
    ///    而这个"倒影会自己动"恰恰是第三章的高潮。
    ///
    ///  所以改用「水下复制一个玩家模型 + 延迟同步」：
    ///    · 在玩家位置的正下方（Y 轴镜像）放一个玩家模型的副本
    ///    · 每帧记录玩家姿态，倒影按延迟播放历史姿态
    ///    · 进入 Anomaly 阶段后停止同步，让倒影自己转头
    ///
    ///  代价：不是物理正确的反射。
    ///  收益：可以精确控制倒影的行为，且性能远低于双相机。
    ///
    /// ══════════════════════ 阶段推进 ══════════════════════
    ///
    ///  阶段切换不看时间，看**玩家接近水面的程度**：
    ///    距水面边缘 &lt; 8 米  → Normal
    ///    玩家第一次"低头看水"（视角朝下 &gt; 35°） → Lagged
    ///    在 Lagged 状态下持续看水 3 秒 → Anomaly
    ///
    ///  这样玩家"自己一步步把它看活"，而不是被动等计时器。
    /// </summary>
    public class MirrorReflection : MonoBehaviour
    {
        [Header("玩家")]
        [Tooltip("玩家根物体（用于同步位置）")]
        public Transform playerRoot;

        [Tooltip("玩家模型（用于复制外观）。留空则自动创建简易人形。")]
        public GameObject playerModelPrefab;

        [Header("水面")]
        [Tooltip("水面中心的世界坐标")]
        public Vector3 waterCenter = new Vector3(0f, 0.12f, 52f);

        [Tooltip("水面尺寸（XZ 平面）")]
        public Vector2 waterSize = new Vector2(13f, 11f);

        [Header("三阶段")]
        [Tooltip("普通阶段的延迟（秒）")]
        public float normalDelay = 0.3f;

        [Tooltip("延迟阶段的延迟（秒）")]
        public float laggedDelay = 1.5f;

        [Tooltip("进入 Normal 的触发距离（玩家距水面中心）")]
        public float normalRange = 12f;

        [Tooltip("低头看水的角度阈值（度）")]
        public float lookDownAngle = 35f;

        [Tooltip("在 Lagged 状态持续看水多久进入 Anomaly")]
        public float anomalyWatchTime = 3f;

        [Header("调试")]
        public bool logStageChanges = true;

        // ────────── 阶段 ──────────

        public enum Stage { Idle, Normal, Lagged, Anomaly }

        /// <summary>当前阶段。</summary>
        public Stage CurrentStage { get; private set; } = Stage.Idle;

        // ────────── 运行时 ──────────

        private GameObject _clone;
        private Transform _cloneT;

        private struct Pose
        {
            public Vector3 pos;
            public Quaternion rot;
            public float time;
        }

        private readonly List<Pose> _history = new List<Pose>(512);

        private Camera _cam;
        private float _lookDownTimer;
        private float _anomalyTimer;

        private void Awake()
        {
            var motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            if (playerRoot == null && motor != null) playerRoot = motor.transform;

            _cam = Camera.main;
            if (_cam == null && motor != null) _cam = motor.GetComponentInChildren<Camera>(true);

            CreateClone();
        }

        /// <summary>
        /// 创建倒影副本。
        ///
        /// ⚠️ 关键：副本必须**去掉所有功能组件**
        ///   （CharacterController / PlayerMotor / PlayerLook / InteractionDetector / 相机），
        ///   否则会出现"两个玩家"同时响应输入、两个相机同时渲染的灾难。
        /// </summary>
        private void CreateClone()
        {
            if (playerRoot == null) return;

            if (playerModelPrefab != null)
            {
                _clone = Instantiate(playerModelPrefab);
                _clone.name = "MirrorClone";
            }
            else
            {
                // 没有外部模型时，用玩家自身的 Mesh 克隆一个纯视觉副本
                _clone = new GameObject("MirrorClone");
                CopyVisualOnly(playerRoot.gameObject, _clone);
            }

            _cloneT = _clone.transform;

            // 移除一切会导致"第二个玩家"的组件
            foreach (var c in _clone.GetComponentsInChildren<MonoBehaviour>(true))
                if (c != null) Destroy(c);
            foreach (var c in _clone.GetComponentsInChildren<Camera>(true))
                if (c != null) Destroy(c);
            foreach (var c in _clone.GetComponentsInChildren<AudioListener>(true))
                if (c != null) Destroy(c);
            foreach (var c in _clone.GetComponentsInChildren<Light>(true))
                if (c != null) Destroy(c);
            foreach (var c in _clone.GetComponentsInChildren<CharacterController>(true))
                if (c != null) Destroy(c);
            foreach (var c in _clone.GetComponentsInChildren<Collider>(true))
                if (c != null) Destroy(c);

            _clone.SetActive(false);   // 等进入范围再显示
        }

        /// <summary>只复制可见网格（不复制逻辑）。</summary>
        private static void CopyVisualOnly(GameObject src, GameObject dst)
        {
            var srcRenderers = src.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var sr in srcRenderers)
            {
                var mf = sr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;

                var go = new GameObject(sr.gameObject.name + "_mirror");
                go.transform.SetParent(dst.transform, false);
                go.transform.position = sr.transform.position;
                go.transform.rotation = sr.transform.rotation;
                go.transform.localScale = sr.transform.lossyScale;

                var nmf = go.AddComponent<MeshFilter>();
                nmf.sharedMesh = mf.sharedMesh;

                var nmr = go.AddComponent<MeshRenderer>();
                nmr.sharedMaterials = sr.sharedMaterials;
            }
        }

        private void Update()
        {
            if (playerRoot == null) return;

            float distToWater = FlatDistance(playerRoot.position, waterCenter);

            // ── 阶段推进 ──
            UpdateStage(distToWater);

            // ── 同步 ──
            if (CurrentStage == Stage.Normal || CurrentStage == Stage.Lagged)
            {
                RecordPose();
                ApplyDelayedPose(CurrentStage == Stage.Normal ? normalDelay : laggedDelay);
            }
            else if (CurrentStage == Stage.Anomaly)
            {
                AnomalyUpdate();
            }
        }

        private void UpdateStage(float dist)
        {
            // 太远：什么都不做
            if (dist > normalRange)
            {
                if (CurrentStage != Stage.Idle)
                {
                    SetStage(Stage.Idle);
                    if (_clone != null) _clone.SetActive(false);
                }
                return;
            }

            if (CurrentStage == Stage.Idle)
            {
                SetStage(Stage.Normal);
                if (_clone != null) _clone.SetActive(true);
                _history.Clear();
            }

            // 检测"低头看水"
            bool lookingDown = IsLookingDown();

            if (CurrentStage == Stage.Normal)
            {
                if (lookingDown)
                {
                    SetStage(Stage.Lagged);
                    _lookDownTimer = 0f;
                }
            }
            else if (CurrentStage == Stage.Lagged)
            {
                if (lookingDown)
                {
                    _lookDownTimer += Time.deltaTime;
                    if (_lookDownTimer >= anomalyWatchTime)
                    {
                        SetStage(Stage.Anomaly);
                        _anomalyTimer = 0f;
                    }
                }
                else
                {
                    _lookDownTimer = Mathf.Max(0f, _lookDownTimer - Time.deltaTime * 0.5f);
                }
            }
        }

        /// <summary>玩家是否正低头看水面。</summary>
        private bool IsLookingDown()
        {
            if (_cam == null) return false;

            // 相机 forward 的 Y 分量越负 → 越朝下
            float pitch = Mathf.Asin(Mathf.Clamp(_cam.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            return pitch < -lookDownAngle;
        }

        // ────────── 姿态录制与回放 ──────────

        private void RecordPose()
        {
            _history.Add(new Pose
            {
                pos = playerRoot.position,
                rot = playerRoot.rotation,
                time = Time.time
            });

            // 只保留最近 3 秒
            while (_history.Count > 0 && Time.time - _history[0].time > 3f)
                _history.RemoveAt(0);
        }

        private void ApplyDelayedPose(float delay)
        {
            if (_cloneT == null) return;

            float targetTime = Time.time - delay;

            // 找到最接近 targetTime 的历史帧
            Pose p = _history.Count > 0 ? _history[0] : new Pose
            {
                pos = playerRoot.position,
                rot = playerRoot.rotation
            };

            for (int i = _history.Count - 1; i >= 0; i--)
            {
                if (_history[i].time <= targetTime) { p = _history[i]; break; }
            }

            // Y 轴镜像：倒影在水面下方
            float mirroredY = 2f * waterCenter.y - p.pos.y;

            _cloneT.position = new Vector3(p.pos.x, mirroredY, p.pos.z);
            _cloneT.rotation = p.rot * Quaternion.Euler(180f, 0f, 0f);   // 上下翻转
        }

        // ────────── 异常阶段 ──────────

        /// <summary>
        /// Anomaly：倒影不再跟随玩家，而是自己站定、慢慢转过头来看玩家。
        /// </summary>
        private void AnomalyUpdate()
        {
            if (_cloneT == null) return;

            _anomalyTimer += Time.deltaTime;

            // 保持在水面位置，慢慢转向玩家
            Vector3 toPlayer = playerRoot.position - _cloneT.position;
            toPlayer.y = 0f;

            if (toPlayer.sqrMagnitude > 0.01f)
            {
                Quaternion target = Quaternion.LookRotation(toPlayer.normalized);
                _cloneT.rotation = Quaternion.Slerp(_cloneT.rotation, target, 1.2f * Time.deltaTime);
            }

            // 缓慢上浮，最终露出水面（"它不是倒影"）。
            //
            // ⚠️ 数学要小心：倒影平时在玩家下方（Y 为负）。
            //   异常阶段它要**从水下升到水面上方**，
            //   所以是从"当前 Y"逐步 MoveTowards 到 waterCenter.y + 1.4。
            //   早期写法用 waterCenter.y - 1.6f + rise，起点比当前还低，
            //   看起来像在往下沉（实测 Y 从 0.09 掉到 -0.78）。
            if (_anomalyTimer > 1.0f)
            {
                float targetY = waterCenter.y + 1.4f;
                var p = _cloneT.position;
                p.y = Mathf.MoveTowards(p.y, targetY, 0.6f * Time.deltaTime);
                _cloneT.position = p;
            }

            if (_anomalyTimer > 4.5f)
            {
                SetStage(Stage.Anomaly);   // 保持，不重复切换
            }
        }

        private void SetStage(Stage s)
        {
            if (CurrentStage == s) return;
            CurrentStage = s;
            if (logStageChanges)
                Debug.Log($"[Mirror] 阶段切换 → {s}");
        }

        private float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }

        // ────────── 供测试 ──────────

        /// <summary>强制切到指定阶段（测试用）。</summary>
        public void ForceStage(Stage s)
        {
            SetStage(s);
            if (_clone != null) _clone.SetActive(s != Stage.Idle);
        }

        /// <summary>倒影是否已创建。</summary>
        public bool HasClone => _clone != null;

        /// <summary>倒影的世界位置（测试用）。</summary>
        public Vector3 ClonePosition => _cloneT != null ? _cloneT.position : Vector3.zero;

        /// <summary>历史帧数量（测试用）。</summary>
        public int HistoryCount => _history.Count;
    }
}