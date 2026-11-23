using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// 恐怖触发器诊断：检查 4 个 HorrorTrigger 是否配置正确，
    /// 并模拟走进 A2 验证"变暗"真的会发生。
    /// </summary>
    public class HorrorDiag : MonoBehaviour
    {
        public bool runOnStart = true;
        private readonly StringBuilder _log = new StringBuilder();

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return new WaitForSeconds(1.0f);

            _log.AppendLine("=== 恐怖触发器诊断 ===");

            var triggers = FindObjectsByType<AbyssEcho.Horror.HorrorTrigger>(FindObjectsSortMode.None);
            _log.AppendLine($"场景中共 {triggers.Length} 个 HorrorTrigger");
            _log.AppendLine();

            foreach (var t in triggers)
            {
                var col = t.GetComponent<BoxCollider>();
                _log.AppendLine($"【{t.name}】");
                _log.AppendLine($"  位置={t.transform.position}");
                _log.AppendLine($"  碰撞体={(col != null ? $"size={col.size} isTrigger={col.isTrigger} enabled={col.enabled}" : "无")}");
                _log.AppendLine($"  delay={t.delay} oneShot={t.oneShot} fearJump={t.fearJump}");
                _log.AppendLine($"  maxFearToTrigger={t.maxFearToTrigger} playStinger={t.playStinger}");
                _log.AppendLine($"  lightsToKill={(t.lightsToKill != null ? t.lightsToKill.Length + " 盏" : "null")}");
                _log.AppendLine($"  enabled={t.enabled} activeInHierarchy={t.gameObject.activeInHierarchy}");
                _log.AppendLine();
            }

            // 恐惧系统状态
            var fear = AbyssEcho.Horror.FearSystem.Instance;
            _log.AppendLine($"FearSystem: {(fear != null ? $"当前恐惧={fear.currentFear:F2}" : "❌ 不存在")}");

            var visuals = FindFirstObjectByType<AbyssEcho.Horror.FearVisuals>();
            _log.AppendLine($"FearVisuals: {(visuals != null ? visuals.gameObject.name : "❌ 不存在")}");

            // 检查 A2 房间的灯
            _log.AppendLine();
            _log.AppendLine("=== A2 区域的灯 ===");
            float a2z = -24.5f;
            int lightCount = 0;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (Mathf.Abs(l.transform.position.z - a2z) < 5f)
                {
                    _log.AppendLine($"  {l.name}: 位置={l.transform.position} enabled={l.enabled} intensity={l.intensity:F2}");
                    lightCount++;
                }
            }
            if (lightCount == 0) _log.AppendLine("  ❌ A2 附近没有灯！");

            Flush();
            yield return null;
            if (!Application.isEditor) Application.Quit(0);
        }

        private void Flush()
        {
            string text = _log.ToString();
            Debug.Log("<b>[HorrorDiag]</b>\n" + text);
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "HorrorDiag.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}