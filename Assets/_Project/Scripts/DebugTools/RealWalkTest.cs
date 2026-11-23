using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// 真实物理走查：用 CharacterController 从起点一路向北，
    /// 记录卡住的确切位置和挡路的物体。这是唯一可靠的验证方式。
    /// </summary>
    public class RealWalkTest : MonoBehaviour
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

            _log.AppendLine("=== 真实物理走查 ===");
            _log.AppendLine($"场景：{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");

            var motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            if (motor == null) { _log.AppendLine("❌ 无 PlayerMotor"); Flush(); yield break; }

            var cc = motor.GetComponent<CharacterController>();
            if (cc == null) { _log.AppendLine("❌ 无 CharacterController"); Flush(); yield break; }

            // 强制站到起点
            cc.enabled = false;
            motor.transform.position = AbyssEcho.LevelGen.ChapterLayout.GetPlayerStart();
            cc.enabled = true;
            yield return new WaitForSeconds(0.3f);

            Vector3 start = motor.transform.position;
            _log.AppendLine($"起点：{start}");
            _log.AppendLine($"CC: height={cc.height} radius={cc.radius} center={cc.center}");

            float stuckTime = 0f;
            float lastZ = start.z;
            float elapsed = 0f;
            bool reported = false;

            while (elapsed < 20f)
            {
                elapsed += Time.deltaTime;

                // 朝 +Z 移动（跑步速度约 4.2 m/s，这里用固定速度 3 m/s）
                cc.Move(new Vector3(0f, -0.03f, 3f * Time.deltaTime));

                Vector3 pos = motor.transform.position;

                if (Mathf.Abs(pos.z - lastZ) < 0.0005f)
                {
                    stuckTime += Time.deltaTime;
                    if (stuckTime > 0.8f && !reported)
                    {
                        reported = true;
                        _log.AppendLine();
                        _log.AppendLine($"❌ 卡住！时间={elapsed:F2}s");
                        _log.AppendLine($"   位置：({pos.x:F2}, {pos.y:F2}, {pos.z:F2})");
                        _log.AppendLine($"   已前进：{pos.z - start.z:F2} 米");
                        DumpObstacles(pos);
                        break;
                    }
                }
                else
                {
                    stuckTime = 0f;
                }

                lastZ = pos.z;
                yield return null;
            }

            if (!reported)
            {
                Vector3 pos = motor.transform.position;
                _log.AppendLine();
                _log.AppendLine($"✅ 20 秒内未卡住，最终位置 ({pos.x:F2}, {pos.y:F2}, {pos.z:F2})");
                _log.AppendLine($"   前进 {pos.z - start.z:F1} 米");
            }

            Flush();
            yield return null;
            if (!Application.isEditor) Application.Quit(0);
        }

        private void DumpObstacles(Vector3 pos)
        {
            _log.AppendLine("   周围 2 米内的实体碰撞体（非触发器）：");

            // 检查玩家胶囊所在位置
            Vector3 capsuleCenter = pos + Vector3.up * 0.9f;
            var hits = Physics.OverlapCapsule(capsuleCenter + Vector3.down * 0.8f,
                                              capsuleCenter + Vector3.up * 0.8f,
                                              0.35f, ~0, QueryTriggerInteraction.Ignore);

            foreach (var h in hits)
            {
                if (h == null) continue;
                if (h is CharacterController) continue;   // 跳过玩家自己

                if (h is BoxCollider bc)
                {
                    Vector3 c = bc.transform.TransformPoint(bc.center);
                    Vector3 s = Vector3.Scale(bc.size, bc.transform.lossyScale);
                    _log.AppendLine($"     {h.gameObject.name}: 中心=({c.x:F2},{c.y:F2},{c.z:F2}) 尺寸=({s.x:F2},{s.y:F2},{s.z:F2})");
                }
                else
                {
                    _log.AppendLine($"     {h.gameObject.name}: {h.GetType().Name}");
                }
            }

            // 向前打射线
            _log.AppendLine("   向前射线（1.5 米内）：");
            for (float y = 0.3f; y <= 1.8f; y += 0.5f)
            {
                RaycastHit hit;
                if (Physics.Raycast(new Vector3(pos.x, pos.y + y, pos.z), Vector3.forward,
                                    out hit, 1.5f, ~0, QueryTriggerInteraction.Ignore))
                {
                    _log.AppendLine($"     y+{y:F1}: {hit.collider.name} 距离={hit.distance:F2}");
                }
                else
                {
                    _log.AppendLine($"     y+{y:F1}: 畅通");
                }
            }
        }

        private void Flush()
        {
            string text = _log.ToString();
            Debug.Log("<b>[RealWalk]</b>\n" + text);
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "RealWalkTest.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}