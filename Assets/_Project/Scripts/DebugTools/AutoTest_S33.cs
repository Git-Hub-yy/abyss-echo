using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S33 模仿症机制测试。
    ///
    /// 核心规则验证：
    ///   T1 场景里有模仿者
    ///   T2 玩家看着它 → 它不动
    ///   T3 玩家背对它 → 它靠近
    ///   T4 玩家回头看 → 它立刻定住
    ///   T5 被持续直视 → 消失
    ///   T6 消失后能重生
    /// </summary>
    public class AutoTest_S33 : MonoBehaviour
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
            _log.AppendLine("ABYSS ECHO · S33 模仿症机制测试");
            _log.AppendLine("=====================================");

            AbyssEcho.Save.SaveSystem.Delete();
            yield return new WaitForSeconds(0.8f);

            var mimics = FindObjectsByType<AbyssEcho.Horror.MimicStalker>(FindObjectsSortMode.None);
            Check("T1 场景中有模仿者", mimics.Length >= 1, $"共 {mimics.Length} 个");
            if (mimics.Length == 0) { Finish(); yield break; }

            var player = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            var cam = Camera.main;
            if (cam == null && player != null) cam = player.GetComponentInChildren<Camera>(true);

            Check("T1b 玩家与相机就绪", player != null && cam != null,
                  $"玩家={player != null} 相机={cam != null}");
            if (player == null || cam == null) { Finish(); yield break; }

            // ⚠️ 关键：把所有模仿者先关掉，只启用一个做测试。
            //   否则 3 个实例同时活动，会互相干扰（实测：Mimic_F5 一直在消失重生，
            //   而 mimics[0] 完全没反应，导致测试结论错误）。
            foreach (var m0 in mimics)
            {
                m0.enabled = false;
                m0.enableAttack = false;
            }
            yield return null;

            var mimic = mimics[0];
            mimic.enabled = true;
            mimic.enableAttack = false;
            mimic.gameObject.SetActive(true);

            // ── 布置一个干净的对峙场景 ──
            //   把模仿者放在玩家正前方 8 米
            Vector3 playerPos = player.transform.position;
            Vector3 front = cam.transform.forward; front.y = 0f; front.Normalize();

            mimic.transform.position = playerPos + front * 8f;
            mimic.ResetStalker();

            // 让玩家朝向它
            LookAt(cam, mimic.transform.position + Vector3.up * 1.0f);
            yield return null;
            yield return null;

            // ── T2 看着它：不应该动 ──
            Vector3 posBefore = mimic.transform.position;
            float t = 0f;
            while (t < 1.0f)
            {
                LookAt(cam, mimic.transform.position + Vector3.up * 1.0f);   // 持续盯着
                t += Time.deltaTime;
                yield return null;
            }
            float movedWhileWatched = Vector3.Distance(posBefore, mimic.transform.position);
            Check("T2 被看着时不动", movedWhileWatched < 0.3f,
                  $"移动了 {movedWhileWatched:F2} 米（状态={mimic.StateName}）");

            // ── T3 背对它：应该靠近 ──
            //
            // ⚠️ 这里踩过两个坑：
            //   ① 早期版本用 T2 算出的 front 取反，但那时相机已经转向模仿者了，
            //      取反后又指回去，angle=4° → 一直"看得见" → 永远不靠近。
            //   ② FearVisuals 每帧写 camera.transform.localRotation（恐惧抖动），
            //      PlayerLook 也写 CameraPivot —— 所以必须两个都关掉再设朝向。
            mimic.ResetStalker();

            // 关掉所有会覆写视角的组件
            var lookC = FindFirstObjectByType<AbyssEcho.Player.PlayerLook>();
            if (lookC != null) lookC.enabled = false;
            var fearC = FindFirstObjectByType<AbyssEcho.Horror.FearVisuals>();
            if (fearC != null) fearC.enabled = false;

            Vector3 headPos = playerPos + Vector3.up * 1.6f;
            cam.transform.position = headPos;

            // 模仿者放在玩家正北 8 米
            mimic.transform.position = playerPos + new Vector3(0f, 0f, 8f);

            // 视角朝南（-Z），模仿者在 +Z → 完全背对
            Vector3 lookAway = Vector3.back;
            SetView(cam, lookAway);

            Debug.Log($"[S33Test] T3 准备：模仿者={mimic.transform.position} 相机={cam.transform.position} 相机前向={cam.transform.forward}");

            posBefore = mimic.transform.position;
            t = 0f;
            while (t < 2.0f)
            {
                cam.transform.position = headPos;
                SetView(cam, lookAway);
                t += Time.deltaTime;
                yield return null;
            }
            float movedWhileUnseen = Vector3.Distance(posBefore, mimic.transform.position);
            Debug.Log($"[S33Test] T3 结果：移动 {movedWhileUnseen:F2} 状态={mimic.StateName} 可见={mimic.IsVisibleToPlayer} {mimic.DebugVisibility}");
            Check("T3 看不见时会靠近", movedWhileUnseen > 0.5f,
                  $"移动了 {movedWhileUnseen:F2} 米（状态={mimic.StateName}）");

            // ── T4 回头看：立刻定住 ──
            LookAt(cam, mimic.transform.position + Vector3.up * 1.0f);
            yield return null;
            yield return null;

            posBefore = mimic.transform.position;
            t = 0f;
            while (t < 1.0f)
            {
                LookAt(cam, mimic.transform.position + Vector3.up * 1.0f);
                t += Time.deltaTime;
                yield return null;
            }
            float movedAfterTurning = Vector3.Distance(posBefore, mimic.transform.position);
            Check("T4 回头后立刻定住", movedAfterTurning < 0.3f,
                  $"移动了 {movedAfterTurning:F2} 米（状态={mimic.StateName}）");

            // ── T5 持续直视 → 消失 ──
            mimic.ResetStalker();
            mimic.maxVisibleTime = 0.8f;
            mimic.transform.position = playerPos + front * 8f;

            t = 0f;
            bool vanished = false;
            while (t < 3.0f)
            {
                LookAt(cam, mimic.transform.position + Vector3.up * 1.0f);
                t += Time.deltaTime;
                if (mimic.StateName == "Vanish") { vanished = true; break; }
                yield return null;
            }
            Check("T5 持续直视会消失", vanished,
                  vanished ? $"0.8 秒后消失（实际 {t:F1}s）" : $"3 秒内未消失（状态={mimic.StateName}）");

            // ── T6 消失后重生 ──
            mimic.respawnDelay = 0.5f;
            mimic.Vanish();
            t = 0f;
            bool respawned = false;
            while (t < 3.0f)
            {
                t += Time.deltaTime;
                if (mimic.StateName == "Idle") { respawned = true; break; }
                yield return null;
            }
            Check("T6 消失后会重生", respawned,
                  respawned ? $"重生后状态=Idle" : $"3 秒内未重生（状态={mimic.StateName}）");

            // ── T7 重生位置在玩家背后 ──
            Vector3 toMimic = mimic.transform.position - playerPos;
            toMimic.y = 0f;
            Vector3 camFwd = cam.transform.forward; camFwd.y = 0f;
            float ang = Vector3.Angle(camFwd.normalized, toMimic.normalized);
            Check("T7 重生位置在视野外", ang > 45f,
                  $"与视线夹角 {ang:F0}°（需要 >45°）");

            // ── T8 远距离不判定可见 ──
            mimic.ResetStalker();
            mimic.transform.position = playerPos + front * 40f;
            yield return null;
            var vis = mimic.TestVisibility();
            Check("T8 超距离不算看得见", !vis, $"40 米外 可见={vis}（sightRange=25）");

            Finish();
        }

        /// <summary>
        /// 把玩家视角转向指定世界方向。
        ///
        /// ⚠️ 必须转 CameraPivot（相机父物体）而不是 Camera：
        ///    FearVisuals / PlayerLook 每帧会写相机自己的 localRotation，
        ///    直接改 Camera.rotation 会被立刻覆盖。
        /// </summary>
        private static void SetView(Camera cam, Vector3 worldForward)
        {
            if (cam == null) return;
            Transform pivot = cam.transform.parent != null ? cam.transform.parent : cam.transform;
            pivot.rotation = Quaternion.LookRotation(worldForward, Vector3.up);
        }

        private void LookAt(Camera cam, Vector3 target, Vector3? from = null)
        {
            if (cam == null) return;
            Vector3 origin = from ?? cam.transform.position;
            Vector3 dir = target - origin;
            if (dir.sqrMagnitude < 0.001f) return;
            cam.transform.rotation = Quaternion.LookRotation(dir.normalized);
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
            Debug.Log("<b>[S33Test]</b>\n" + text);
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "S33_AutoTestResult.txt"), text, new UTF8Encoding(false));
            }
            catch { }
            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }
    }
}
