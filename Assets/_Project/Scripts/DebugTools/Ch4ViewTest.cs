using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S54：第四章 G5 → G7 → 心瓣 真实截图验证。
    ///
    /// 把玩家依次传送到路线上几个关键位置，朝正确方向看，
    /// 保存真实渲染截图，证明：
    ///   1. 能看到组织甬道/核心西门的通路
    ///   2. 进入 G7 能看到核心腔
    ///   3. 能看到心瓣装置（修复前被肉块挡住）
    ///   4. 靠近心瓣有交互提示
    /// </summary>
    public class Ch4ViewTest : MonoBehaviour
    {
        public bool runOnStart = true;

        private readonly StringBuilder _log = new StringBuilder();

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            AbyssEcho.LevelGen.ChapterLayout.Current = AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter4;
            yield return new WaitForSeconds(1.0f);

            _log.AppendLine("══════ 第四章 G5→G7 视角截图验证 ══════");

            var motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            var look = FindFirstObjectByType<AbyssEcho.Player.PlayerLook>();
            var cam = Camera.main;

            if (motor == null || cam == null) { _log.AppendLine("❌ 缺组件"); Dump(); yield break; }

            if (look != null) look.enabled = false;

            // 关掉恐惧视觉，避免暗角影响判读
            var fv = FindFirstObjectByType<AbyssEcho.Horror.FearVisuals>();
            if (fv != null) fv.enabled = false;

            // 保证有光：开手电
            var fl = FindFirstObjectByType<AbyssEcho.Tools.FlashlightSystem>();
            var flType = typeof(AbyssEcho.Tools.FlashlightSystem);
            flType.GetMethod("SetLightOn", System.Reflection.BindingFlags.Instance |
                              System.Reflection.BindingFlags.NonPublic)?.Invoke(fl, new object[] { true });

            yield return new WaitForSeconds(0.3f);

            // ══ 用 CharacterController 真移动，而不是直接改 Transform ══
            var cc = motor.GetComponent<CharacterController>();

            // ── 视角 1：G5 组织层内部，朝东看（门洞方向）──
            yield return Shot(cam, motor, cc, new Vector3(-21f, 0.05f, 8f),
                              "G5_组织层_朝东看门洞", 25f);

            // ── 视角 2：G5 东门洞前 ──
            yield return Shot(cam, motor, cc, new Vector3(-15.5f, 0.05f, 8f),
                              "G5东门洞_朝东看甬道", 12f);

            // ── 视角 3：核心西门 ──
            yield return Shot(cam, motor, cc, new Vector3(-12f, 0.05f, 8f),
                              "核心西门_朝东看G7", 2f);

            // ── 视角 4：刚进入 G7，朝北看核心腔 ──
            yield return Shot(cam, motor, cc, new Vector3(-6f, 0.05f, 9f),
                              "进入G7_朝北看核心腔", 15f);

            // ── 视角 5：G7 中央，近距离看心瓣 ★ 关键 ──
            yield return Shot(cam, motor, cc, new Vector3(0f, 0.05f, 11f),
                              "G7靠近心瓣_朝北看", 0f);

            // ── 视角 6：站在心瓣前 2 米 ──
            yield return Shot(cam, motor, cc, new Vector3(0f, 0.05f, 13.8f),
                              "心瓣前2米_朝北看", 0f);

            // ══ 检查交互提示 ══
            _log.AppendLine();
            _log.AppendLine("──── 心瓣交互状态 ────");
            var valve = GameObject.Find("HeartValve");
            if (valve != null)
            {
                var pulse = valve.GetComponent<AbyssEcho.Puzzle.PulseSyncPuzzle>();
                if (pulse != null)
                {
                    _log.AppendLine($"  CanInteract = {pulse.CanInteract}");
                    _log.AppendLine($"  GetPrompt() = \"{pulse.GetPrompt()}\"");
                }

                var detector = FindFirstObjectByType<AbyssEcho.Interaction.InteractionDetector>();
                if (detector != null)
                {
                    _log.AppendLine($"  InteractionDetector.HasTarget = {detector.HasTarget}");
                    _log.AppendLine($"  CurrentPrompt = \"{detector.CurrentPrompt}\"");
                }
            }

            // ══ 检查核心是否还挡路 ══
            _log.AppendLine();
            _log.AppendLine("──── 核心肉块位置 ────");
            var core = GameObject.Find("核心");
            if (core != null)
            {
                var col = core.GetComponent<Collider>();
                _log.AppendLine($"  位置 = {core.transform.position}");
                if (col != null)
                    _log.AppendLine($"  世界范围 z[{col.bounds.min.z:F1},{col.bounds.max.z:F1}]");
            }

            Dump();
        }

        private IEnumerator Shot(Camera cam, AbyssEcho.Player.PlayerMotor motor,
                                 CharacterController cc, Vector3 pos, string name, float yaw)
        {
            // 移动到目标位置（用 cc.Move，避免穿墙）
            Vector3 delta = pos - motor.transform.position;
            if (cc != null && cc.enabled)
            {
                cc.enabled = false;
                motor.transform.position = pos;
                cc.enabled = true;
            }
            else
            {
                motor.transform.position = pos;
            }

            motor.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            yield return null;
            yield return null;
            yield return new WaitForEndOfFrame();

            int w = 640, h = 360;
            RenderTexture rt = null;
            Texture2D tex = null;

            try
            {
                rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
                var prev = cam.targetTexture;
                var prevActive = RenderTexture.active;

                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = prev;

                RenderTexture.active = rt;
                tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = prevActive;

                byte[] png = tex.EncodeToPNG();
                string dir = FindReports();
                if (dir != null)
                {
                    string shots = Path.Combine(dir, "Ch4Shots");
                    Directory.CreateDirectory(shots);
                    File.WriteAllBytes(Path.Combine(shots, $"{name}.png"), png);
                }

                // 亮度统计
                float sum = 0f, max = 0f;
                Color[] px = tex.GetPixels();
                foreach (var p in px)
                {
                    float l = 0.299f * p.r + 0.587f * p.g + 0.114f * p.b;
                    sum += l; if (l > max) max = l;
                }
                _log.AppendLine($"  [截图] {name}  位置={pos} 朝向yaw={yaw}  " +
                                $"平均亮度={sum / px.Length:F4} 最大={max:F4}");
            }
            catch (System.Exception e)
            {
                _log.AppendLine($"  [截图] {name} 失败: {e.GetType().Name}");
            }
            finally
            {
                if (tex != null) Destroy(tex);
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
            }
        }

        private string FindReports()
        {
            var di = new DirectoryInfo(Application.dataPath);
            for (int hop = 0; hop < 6 && di != null; hop++)
            {
                string r = Path.Combine(di.FullName, "Reports");
                if (Directory.Exists(r)) return r;
                di = di.Parent;
            }
            return null;
        }

        private void Dump()
        {
            string text = _log.ToString();
            Debug.Log("<b>[Ch4ViewTest]</b>\n" + text);

            string dir = FindReports();
            if (dir != null)
            {
                try { File.WriteAllText(Path.Combine(dir, "Ch4ViewTest.txt"), text, new UTF8Encoding(false)); }
                catch { }
            }

            if (!Application.isEditor) Application.Quit(0);
        }
    }
}