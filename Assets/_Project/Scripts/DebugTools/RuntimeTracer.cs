using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// 运行状态记录器：把玩家位置、结局状态等写入文件，
    /// 用于在**发布版**里诊断问题（发布版日志不易获取）。
    ///
    /// 输出位置：Application.persistentDataPath/runtime_trace.txt
    ///
    /// 记录内容：
    ///   · 启动时的玩家位置
    ///   · 每 2 秒的位置（最多 20 条）
    ///   · 位置突变（超过 2 米/次）
    ///   · 结局触发
    /// </summary>
    public class RuntimeTracer : MonoBehaviour
    {
        [Tooltip("是否启用")]
        public bool enableTracing = true;

        [Tooltip("采样间隔（秒）")]
        public float sampleInterval = 2f;

        [Tooltip("最多记录多少条采样")]
        public int maxSamples = 20;

        private readonly StringBuilder _log = new StringBuilder();
        private float _timer;
        private int _samples;
        private Vector3 _lastPos;
        private bool _started;

        private AbyssEcho.Player.PlayerMotor _motor;
        private AbyssEcho.Narrative.ChapterEndingSystem _ending;
        private bool _endingReported;

        private void Start()
        {
            if (!enableTracing) return;

            _motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            _ending = FindFirstObjectByType<AbyssEcho.Narrative.ChapterEndingSystem>();

            _log.AppendLine("=== ABYSS ECHO 运行时追踪 ===");
            _log.AppendLine($"时间: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _log.AppendLine($"场景: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");

            if (_motor != null)
            {
                _lastPos = _motor.transform.position;
                _log.AppendLine($"启动位置: {_lastPos}");
            }
            else
            {
                _log.AppendLine("⚠ 找不到 PlayerMotor");
            }

            if (_ending != null)
            {
                _log.AppendLine($"结局触发区: 中心={_ending.triggerCenter} 尺寸={_ending.triggerSize}");
                _log.AppendLine($"玩家到终点距离: {Vector3.Distance(_lastPos, _ending.triggerCenter):F1} 米");
            }

            _log.AppendLine("");
            _started = true;
            Write();
        }

        private void Update()
        {
            if (!_started || !enableTracing) return;

            if (_motor != null)
            {
                Vector3 pos = _motor.transform.position;
                float jump = Vector3.Distance(pos, _lastPos);

                // 位置突变立即记录
                if (jump > 2f)
                {
                    _log.AppendLine($"[跳变] {_lastPos} → {pos} （{jump:F1} 米）");
                    Write();
                }

                _lastPos = pos;
            }

            // 结局触发记录
            if (_ending != null && _ending.IsPlaying && !_endingReported)
            {
                _endingReported = true;
                _log.AppendLine($"[!!] 结局触发！玩家位置={(_motor != null ? _motor.transform.position.ToString() : "?")}");
                Write();
            }

            // 定时采样
            _timer += Time.deltaTime;
            if (_timer >= sampleInterval && _samples < maxSamples)
            {
                _timer = 0f;
                _samples++;
                _log.AppendLine($"[{_samples}] {Time.time:F1}s 位置={(_motor != null ? _motor.transform.position.ToString() : "?")} " +
                                $"结局={(_ending != null ? _ending.IsPlaying : false)}");
                Write();
            }
        }

        private void Write()
        {
            try
            {
                string path = Path.Combine(Application.persistentDataPath, "runtime_trace.txt");
                File.WriteAllText(path, _log.ToString(), new UTF8Encoding(false));
            }
            catch { }
        }
    }
}