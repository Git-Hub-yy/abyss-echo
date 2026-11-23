using UnityEngine;

namespace AbyssEcho.Horror
{
    /// <summary>
    /// 恐怖事件触发器：玩家进入指定区域时触发一次"惊吓事件"。
    ///
    /// 事件类型（可在 Inspector 中选择组合）：
    ///   · LightKill     区域内灯光熄灭
    ///   · Stinger       播放突发惊吓音
    ///   · Footsteps     在远处播放脚步声（制造"有人"的错觉）
    ///   · FearJump      恐惧值瞬间跳升
    ///   · DoorSlam      附近的门自动开合
    ///
    /// 设计原则：
    ///   · **每个触发器只触发一次**（避免重复吓人导致麻木）
    ///   · 触发后有冷却，防止玩家在边界反复进出刷事件
    ///   · 事件表现克制——恐怖来自"意料之外"，不是"音量之大"
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class HorrorTrigger : MonoBehaviour
    {
        [Header("触发条件")]
        [Tooltip("进入该区域后延迟多久触发（秒）；0 = 立即")]
        public float delay = 0.6f;

        [Tooltip("只在玩家恐惧值低于该值时触发（1 = 总是触发）")]
        [Range(0f, 1f)] public float maxFearToTrigger = 1f;

        [Tooltip("是否只触发一次")]
        public bool oneShot = true;

        [Header("事件：灯光")]
        [Tooltip("熄灭这些灯")]
        public Light[] lightsToKill;

        [Tooltip("让这些灯改为闪烁")]
        public FlickerLight[] lightsToFlicker;

        [Header("事件：声音")]
        public bool playStinger = false;

        [Tooltip("在什么位置播放脚步声（留空则不播放）")]
        public Transform footstepSource;

        [Range(0, 6)] public int footstepCount = 0;

        [Header("事件：恐惧")]
        [Tooltip("恐惧值瞬间增加量（0-1）")]
        [Range(0f, 1f)] public float fearJump = 0f;

        [Header("事件：门")]
        [Tooltip("强制开合的门")]
        public AbyssEcho.Interaction.DoorInteractable[] doorsToToggle;

        [Header("事件：环境光压暗（S20 新增）")]
        [Tooltip("触发时是否压低环境光。必须为 true，否则灭灯玩家感觉不到——" +
                 "场景环境光有基础亮度，灯全灭了房间还是看得见。")]
        public bool dimAmbient = true;

        [Tooltip("环境光压到原来的多少倍（0.15 = 只剩 15%% 亮度）")]
        [Range(0.02f, 1f)] public float dimAmbientTo = 0.15f;

        [Tooltip("压暗过渡时长（秒）")]
        public float dimDuration = 1.2f;

        private Color? _savedAmbient;
        private bool _triggered;
        private bool _playerInside;
        private float _enterTime;

        private void Awake()
        {
            var col = GetComponent<BoxCollider>();
            col.isTrigger = true;
        }

        private void Update()
        {
            if (!_playerInside) return;
            if (_triggered && oneShot) return;

            if (delay > 0f && Time.time - _enterTime < delay) return;

            Fire();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayer(other)) return;

            _playerInside = true;
            _enterTime = Time.time;

            // S21 调试：确认触发器真的被玩家碰到
            Debug.Log($"[Horror] 玩家进入 {name}（delay={delay}）");

            if (delay <= 0f) Fire();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsPlayer(other)) return;
            _playerInside = false;
        }

        private static bool IsPlayer(Collider c)
        {
            return c.GetComponentInParent<AbyssEcho.Player.PlayerMotor>() != null;
        }

        /// <summary>执行事件。</summary>
        public void Fire()
        {
            if (_triggered && oneShot) return;

            var fear = FearSystem.Instance;
            if (fear != null && fear.currentFear > maxFearToTrigger) return;

            _triggered = true;

            // ⓪ 记录原始环境光（压暗前保存，供 RestoreAmbient 恢复）
            _savedAmbient = RenderSettings.ambientLight;

            // ① 熄灯
            if (lightsToKill != null)
                foreach (var l in lightsToKill)
                    if (l != null) l.enabled = false;

            // ② 转为闪烁
            if (lightsToFlicker != null)
                foreach (var f in lightsToFlicker)
                    if (f != null) f.mode = FlickerLight.Mode.Flicker;

            // ③ 惊吓音
            if (playStinger && AbyssEcho.Audio.AudioManager.Instance != null)
                AbyssEcho.Audio.AudioManager.Instance.PlayStinger();

            // ④ 远处脚步
            if (footstepCount > 0 && footstepSource != null)
                StartCoroutine(PlayFootsteps());

            // ⑤ 恐惧跳升
            if (fearJump > 0f && fear != null)
                fear.AddFear(fearJump);

// ⑥ 环境光压暗（S20 新增）
            //
            // ⚠️ 为什么需要：场景的环境光被设成 (0.26, 0.29, 0.33)，
            //    这是"灰盒阶段能看清空间"的亮度。
            //    但恐怖触发器只关灯，环境光照样把房间照亮——
            //    玩家灭灯后什么感觉都没有，抱怨"根本没有变暗"。
            //    所以这里必须同时压低环境光，让"灭灯"真的能被看见。
            if (dimAmbient)
            {
                StartCoroutine(DimAmbientRoutine());
            }

            // ⑦ 门开合
            if (doorsToToggle != null)
                foreach (var d in doorsToToggle)
                    if (d != null) d.OnInteract(null);

            Debug.Log($"[Horror] 触发事件：{name} 灭灯={(lightsToKill != null ? lightsToKill.Length : 0)}盏 " +
                      $"环境光压暗={dimAmbient} 目标={dimAmbientTo:F2}");
        }

        /// <summary>把环境光在 dimDuration 内压到 ambientAfterDim 的亮度。</summary>
        private System.Collections.IEnumerator DimAmbientRoutine()
        {
            Color from = RenderSettings.ambientLight;
            Color to = from * dimAmbientTo;   // 按比例压暗，保留色相

            float t = 0f;
            while (t < dimDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dimDuration);
                RenderSettings.ambientLight = Color.Lerp(from, to, k);
                yield return null;
            }

            RenderSettings.ambientLight = to;
        }

        /// <summary>恢复环境光（重玩时调用）。</summary>
        public void RestoreAmbient()
        {
            if (_savedAmbient.HasValue)
            {
                RenderSettings.ambientLight = _savedAmbient.Value;
                _savedAmbient = null;
            }
        }

        private System.Collections.IEnumerator PlayFootsteps()
        {
            var am = AbyssEcho.Audio.AudioManager.Instance;
            if (am == null) yield break;

            for (int i = 0; i < footstepCount; i++)
            {
                am.PlayFootstep(footstepSource.position, true);
                yield return new WaitForSeconds(Random.Range(0.42f, 0.58f));
            }
        }

        /// <summary>重置触发器（用于重玩）。</summary>
        public void ResetTrigger()
        {
            _triggered = false;
            RestoreAmbient();
        }
    }
}
