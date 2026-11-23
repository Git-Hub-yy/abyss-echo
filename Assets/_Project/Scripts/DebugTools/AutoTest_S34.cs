using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S34 镜沼倒影测试。
    ///
    ///   T1 场景里有倒影组件
    ///   T2 倒影副本已创建（不含功能组件）
    ///   T3 玩家远离时不激活
    ///   T4 靠近后进入 Normal 阶段
    ///   T5 Normal 阶段倒影在玩家下方（Y 镜像）
    ///   T6 强制切到 Anomaly 后倒影会转身上浮
    ///   T7 副本没有 Camera / CharacterController / AudioListener（防"第二个玩家"）
    /// </summary>
    public class AutoTest_S34 : MonoBehaviour
    {
        public bool runOnStart = true;

        private int _pass, _fail;
        private readonly StringBuilder _log = new StringBuilder();

        private void Start()
        {
            if (runOnStart) StartCoroutine(RunAll());
        }

        private IEnumerator RunAll()
        {
            _log.AppendLine("ABYSS ECHO · S34 镜沼倒影测试");
            _log.AppendLine("=====================================");

            AbyssEcho.Save.SaveSystem.Delete();
            yield return new WaitForSeconds(0.8f);

            var mirrors = FindObjectsByType<AbyssEcho.Horror.MirrorReflection>(FindObjectsSortMode.None);
            Check("T1 场景中有倒影组件", mirrors.Length >= 1, $"共 {mirrors.Length} 个");
            if (mirrors.Length == 0) { Finish(); yield break; }

            var mirror = mirrors[0];
            var player = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            Check("T1b 玩家就绪", player != null, player != null ? player.name : "❌");
            if (player == null) { Finish(); yield break; }

            yield return null;
            yield return null;

            // T2 副本已创建
            Check("T2 倒影副本已创建", mirror.HasClone, mirror.HasClone ? "ok" : "❌ 未创建");

            // T7 副本没有危险组件
            //
            // ⚠️ 不能用 GameObject.Find("MirrorClone")：
            //   副本是 Mirror_F7 的子物体，在某些状态下还是 inactive，
            //   GameObject.Find 找不到。改为直接从 mirror 组件上取。
            Transform cloneRoot = null;
            foreach (var t in mirror.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "MirrorClone") { cloneRoot = t; break; }
            }
            // 兜底：遍历场景里所有 Transform
            if (cloneRoot == null)
            {
                // 倒影在远离水面时是 SetActive(false) 的，
                // FindObjectsByType 默认不返回未激活对象，必须用 FindObjectsOfTypeAll。
                foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                {
                    if (t != null && t.name == "MirrorClone") { cloneRoot = t; break; }
                }
            }

            if (cloneRoot != null)
            {
                bool hasCam = cloneRoot.GetComponentInChildren<Camera>(true) != null;
                bool hasCC = cloneRoot.GetComponentInChildren<CharacterController>(true) != null;
                bool hasAL = cloneRoot.GetComponentInChildren<AudioListener>(true) != null;
                bool hasLight = cloneRoot.GetComponentInChildren<Light>(true) != null;
                Check("T7 副本无相机/控制器/听者/灯光", !hasCam && !hasCC && !hasAL && !hasLight,
                      $"Camera={hasCam} CC={hasCC} AudioListener={hasAL} Light={hasLight}");
            }
            else
            {
                Check("T7 副本无相机/控制器/听者/灯光", false, "❌ 找不到 MirrorClone");
            }

            // T3 远离时不激活
            player.transform.position = new Vector3(0f, 0.05f, 30f);   // 距 F7(z=60) 30 米
            yield return null;
            yield return null;
            Check("T3 远离时倒影不激活", mirror.CurrentStage == AbyssEcho.Horror.MirrorReflection.Stage.Idle,
                  $"阶段={mirror.CurrentStage}");

            // T4 靠近进入 Normal
            player.transform.position = new Vector3(0f, 0.05f, 55f);   // 距水面 5 米
            yield return null;
            yield return null;
            yield return null;
            Check("T4 靠近后进入 Normal", mirror.CurrentStage == AbyssEcho.Horror.MirrorReflection.Stage.Normal,
                  $"阶段={mirror.CurrentStage}");

            // 等待积累历史帧
            yield return new WaitForSeconds(1.0f);

            // T5 Y 镜像
            Vector3 cp = mirror.ClonePosition;
            float waterY = 0.12f;
            float expectedY = 2f * waterY - player.transform.position.y;
            bool mirrored = Mathf.Abs(cp.y - expectedY) < 0.5f;
            Check("T5 倒影在玩家下方（Y 镜像）", mirrored,
                  $"玩家Y={player.transform.position.y:F2} 倒影Y={cp.y:F2} 期望≈{expectedY:F2}");

            Check("T5b 历史帧已积累", mirror.HistoryCount > 10, $"{mirror.HistoryCount} 帧");

            // T6 强制 Anomaly
            mirror.ForceStage(AbyssEcho.Horror.MirrorReflection.Stage.Anomaly);
            yield return null;
            Vector3 beforeFloat = mirror.ClonePosition;
            yield return new WaitForSeconds(3.0f);
            Vector3 afterFloat = mirror.ClonePosition;

            bool rose = afterFloat.y > beforeFloat.y + 0.2f;
            Check("T6 Anomaly 阶段倒影上浮", rose,
                  $"上浮前Y={beforeFloat.y:F2} 上浮后Y={afterFloat.y:F2}");

            // 阶段保持在 Anomaly
            Check("T6b 阶段保持 Anomaly",
                  mirror.CurrentStage == AbyssEcho.Horror.MirrorReflection.Stage.Anomaly,
                  $"阶段={mirror.CurrentStage}");

            Finish();
        }

        private void Check(string name, bool ok, string detail = "")
        {
            if (ok) { _pass++; _log.AppendLine($"[通过] {name} {detail}"); }
            else { _fail++; _log.AppendLine($"[失败] {name} {detail}"); }
        }

        private void Finish()
        {
            _log.AppendLine("=====================================");
            _log.AppendLine($"结果：通过 {_pass}，失败 {_fail}");
            string text = _log.ToString();
            Debug.Log("<b>[S34Test]</b>\n" + text);
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "S34_AutoTestResult.txt"), text, new UTF8Encoding(false));
            }
            catch { }
            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }
    }
}
