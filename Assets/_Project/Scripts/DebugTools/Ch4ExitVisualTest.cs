using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>S57：第四章终点门 + 门后通道的真实渲染截图。</summary>
    public class Ch4ExitVisualTest : MonoBehaviour
    {
        public bool runOnStart = true;
        private readonly StringBuilder _log = new StringBuilder();

        private AbyssEcho.Player.PlayerMotor _motor;
        private CharacterController _cc;
        private Camera _cam;

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            AbyssEcho.LevelGen.ChapterLayout.Current =
                AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter4;
            yield return new WaitForSeconds(1.2f);

            _log.AppendLine("══════ S57 第四章终点门视觉验证 ══════");
            _motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            _cc = _motor != null ? _motor.GetComponent<CharacterController>() : null;
            _cam = Camera.main;
            if (_motor == null || _cam == null) { _log.AppendLine("❌ 缺组件"); Dump(); yield break; }

            var look = FindFirstObjectByType<AbyssEcho.Player.PlayerLook>();
            if (look != null) look.enabled = false;
            var fv = FindFirstObjectByType<AbyssEcho.Horror.FearVisuals>();
            if (fv != null) fv.enabled = false;
            foreach (var m in FindObjectsByType<AbyssEcho.Horror.MimicStalker>(FindObjectsSortMode.None))
                m.enabled = false;

            var fl = FindFirstObjectByType<AbyssEcho.Tools.FlashlightSystem>();
            typeof(AbyssEcho.Tools.FlashlightSystem)
                .GetMethod("SetLightOn", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(fl, new object[] { true });

            yield return new WaitForSeconds(0.4f);

            var gate = GameObject.Find("Door_HeartGate");
            var door = gate != null ? gate.GetComponent<AbyssEcho.Interaction.DoorInteractable>() : null;
            var valve = GameObject.Find("HeartValve");
            var pulse = valve != null ? valve.GetComponent<AbyssEcho.Puzzle.PulseSyncPuzzle>() : null;

            // ══ 1. 门关着时 ══
            yield return Shot(new Vector3(0f, 0.05f, 14f), 0f, "01_G7朝终点门_关闭");
            yield return Shot(new Vector3(0f, 0.05f, 20f), 0f, "02_靠近门_关闭");
            yield return Shot(new Vector3(0f, 0.05f, 23f), 0f, "03_门前_关闭");

            // ══ 解谜开门 ══
            if (pulse != null)
            {
                pulse.GetType().GetMethod("Solve", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(pulse, null);
                yield return new WaitForSeconds(0.3f);
            }
            if (door != null)
            {
                door.OnInteract(gameObject);
                // 等门滑到位（用真实时间，不用 deltaTime）
                float waited = 0f;
                while (waited < 4f && Vector3.Distance(gate.transform.position,
                       new Vector3(0f, 1.4f, 24.7f)) < 2.5f)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
                yield return new WaitForSeconds(0.3f);
                _log.AppendLine($"  门已打开: IsOpen={door.IsOpen}  位置={gate.transform.position} 等待={waited:F2}s");
            }

            // ══ 2. 门打开后朝门洞看 ══
            yield return Shot(new Vector3(0f, 0.05f, 22f), 0f, "04_门打开_朝门洞");

            yield return Shot(new Vector3(0f, 0.05f, 24f), 0f, "05_门洞近景");

            // ══ 3. 刚穿过门 ══
            yield return Shot(new Vector3(0f, 0.05f, 26.5f), 0f, "06_刚穿过门");

            // ══ 4. 通道中段 ══
            yield return Shot(new Vector3(0f, 0.05f, 32f), 0f, "07_通道中段_朝北");
            yield return Shot(new Vector3(0f, 0.05f, 32f), 180f, "08_通道中段_朝南回望");

            // ══ 5. 到达触发区 ══
            yield return Shot(new Vector3(0f, 0.05f, 39f), 0f, "09_触发区_朝北");
            yield return Shot(new Vector3(0f, 0.05f, 39f), 180f, "10_触发区_朝南回望");

            // ══ 通道结构确认 ══
            _log.AppendLine();
            _log.AppendLine("──── 通道对象 ────");
            var root = GameObject.Find("--- EXIT_CORRIDOR_CH4 ---");
            if (root != null)
            {
                foreach (Transform c in root.transform)
                {
                    if (c.name == "Lights")
                    {
                        _log.AppendLine($"  Lights = {c.childCount} 盏");
                        continue;
                    }
                    var bc = c.GetComponent<BoxCollider>();
                    if (bc != null)
                        _log.AppendLine($"  {c.name,-22} x[{bc.bounds.min.x:F1},{bc.bounds.max.x:F1}] " +
                                        $"y[{bc.bounds.min.y:F1},{bc.bounds.max.y:F1}] " +
                                        $"z[{bc.bounds.min.z:F1},{bc.bounds.max.z:F1}]");
                }
            }

            Dump();
        }

        private IEnumerator Shot(Vector3 pos, float yaw, string name)
        {
            if (_cc != null) _cc.enabled = false;
            _motor.transform.position = pos;
            _motor.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (_cc != null) _cc.enabled = true;

            yield return null;
            yield return null;
            yield return new WaitForEndOfFrame();

            int w = 640, h = 360;
            RenderTexture rt = null;
            Texture2D tex = null;
            try
            {
                rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
                var pt = _cam.targetTexture;
                var pa = RenderTexture.active;
                _cam.targetTexture = rt;
                _cam.Render();
                _cam.targetTexture = pt;

                RenderTexture.active = rt;
                tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = pa;

                string dir = FindReports();
                if (dir != null)
                {
                    string shots = Path.Combine(dir, "Ch4ExitShots");
                    Directory.CreateDirectory(shots);
                    File.WriteAllBytes(Path.Combine(shots, name + ".png"), tex.EncodeToPNG());
                }

                float sum = 0f, max = 0f;
                Color[] px = tex.GetPixels();
                foreach (var p in px)
                {
                    float l = 0.299f * p.r + 0.587f * p.g + 0.114f * p.b;
                    sum += l; if (l > max) max = l;
                }
                _log.AppendLine($"  [{name}] pos=({pos.x:F1},{pos.z:F1}) yaw={yaw} " +
                                $"亮度={sum / px.Length:F4} 最大={max:F4}");
            }
            catch (System.Exception e) { _log.AppendLine($"  [{name}] 失败 {e.GetType().Name}"); }
            finally
            {
                if (tex != null) Destroy(tex);
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
            }
        }

        private string FindReports()
        {
            var di = new DirectoryInfo(Application.dataPath);
            for (int hop = 0; hop < 8 && di != null; hop++)
            {
                string r = Path.Combine(di.FullName, "Reports");
                if (Directory.Exists(r) && Directory.Exists(Path.Combine(di.FullName, "UnityProject")))
                    return r;
                di = di.Parent;
            }
            return @"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports";
        }

        private void Dump()
        {
            string text = _log.ToString();
            Debug.Log("<b>[Ch4ExitVisualTest]</b>\n" + text);
            string dir = FindReports();
            if (dir != null)
            {
                try { File.WriteAllText(Path.Combine(dir, "Ch4ExitVisualTest.txt"), text, new UTF8Encoding(false)); }
                catch { }
            }
            if (!Application.isEditor) Application.Quit(0);
        }
    }
}