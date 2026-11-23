using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// 实测走查：从 A0 起点开始，用 CharacterController 一路向北走，
    /// 记录卡在哪一步。这比几何计算更可靠。
    /// </summary>
    public class WalkThroughTest : MonoBehaviour
    {
        public bool runOnStart = true;
        private readonly StringBuilder _log = new StringBuilder();

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return new WaitForSeconds(0.5f);

            _log.AppendLine("=== 实测走查（A0 起点向北）===");

            var motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            if (motor == null) { _log.AppendLine("无 PlayerMotor"); Flush(); yield break; }

            var cc = motor.GetComponent<CharacterController>();

            // 从起点开始
            Vector3 start = AbyssEcho.LevelGen.ChapterLayout.GetPlayerStart();
            if (cc != null) cc.enabled = false;
            motor.transform.position = start;
            if (cc != null) cc.enabled = true;

            _log.AppendLine($"起点：{start}");
            yield return new WaitForSeconds(0.3f);

            float lastZ = motor.transform.position.z;
            int stuckFrames = 0;
            float totalTime = 0f;

            // 向北走 25 秒
            while (totalTime < 25f)
            {
                totalTime += Time.deltaTime;

                if (cc != null && cc.enabled)
                {
                    cc.Move(new Vector3(0f, -0.02f, 0.055f));   // 约 2.75 m/s（跑步速度）
                }

                Vector3 pos = motor.transform.position;

                // 检测卡住：位置几乎没变
                if (Mathf.Abs(pos.z - lastZ) < 0.001f)
                {
                    stuckFrames++;
                    if (stuckFrames == 30)   // 约 0.5 秒没动
                    {
                        _log.AppendLine($"❌ 卡住！时间={totalTime:F1}s 位置=({pos.x:F2}, {pos.y:F2}, {pos.z:F2})");
                        LogObstacles(pos);
                        break;
                    }
                }
                else
                {
                    stuckFrames = 0;
                }

                // 每 2 秒记录一次
                if (Mathf.Repeat(totalTime, 2f) < Time.deltaTime)
                {
                    _log.AppendLine($"  t={totalTime:F1}s 位置=({pos.x:F1}, {pos.y:F1}, {pos.z:F1})");
                }

                lastZ = pos.z;
                yield return null;
            }

            Vector3 final = motor.transform.position;
            _log.AppendLine($"结束位置：({final.x:F2}, {final.y:F2}, {final.z:F2})");
            _log.AppendLine($"前进距离：{final.z - start.z:F1} 米");

            Flush();
            yield return null;
            if (!Application.isEditor) Application.Quit(0);
        }

        private void LogObstacles(Vector3 pos)
        {
            _log.AppendLine("  附近阻挡物：");
            var cols = Physics.OverlapSphere(pos + Vector3.up * 1f, 1.2f);
            foreach (var c in cols)
            {
                if (c is BoxCollider bc && !bc.isTrigger)
                {
                    Vector3 center = bc.transform.TransformPoint(bc.center);
                    Vector3 size = Vector3.Scale(bc.size, bc.transform.lossyScale);
                    _log.AppendLine($"    {c.gameObject.name}: 中心=({center.x:F2},{center.y:F2},{center.z:F2}) 尺寸=({size.x:F2},{size.y:F2},{size.z:F2})");
                }
            }
        }

        private void Flush()
        {
            string text = _log.ToString();
            Debug.Log("<b>[WalkTest]</b>\n" + text);
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "WalkThroughTest.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}