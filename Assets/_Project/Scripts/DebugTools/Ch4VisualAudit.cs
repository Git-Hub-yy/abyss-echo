using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S56：第四章真实画面截图调查（只读，不修改场景）。
    ///
    /// 用真实 GPU 渲染，从玩家视角拍摄关键位置，
    /// 确认 Door_HeartGate 到底能不能看见。
    ///
    /// ⚠️ 只读取和渲染，不 SaveScene。
    /// </summary>
    public class Ch4VisualAudit : MonoBehaviour
    {
        public bool runOnStart = true;
        private readonly StringBuilder _log = new StringBuilder();

        private AbyssEcho.Player.PlayerMotor _motor;
        private CharacterController _cc;
        private Camera _cam;
        private AbyssEcho.Tools.FlashlightSystem _fl;
        private AbyssEcho.Horror.PlayerDeathSystem _death;

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            var L = typeof(AbyssEcho.LevelGen.ChapterLayout);
            L.GetProperty("Current").SetValue(null,
                AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter4);
            var ending = FindFirstObjectByType<AbyssEcho.Narrative.ChapterEndingSystem>();
            ending?.GetType().GetMethod("ApplyChapterConfig",
                BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(ending, null);

            yield return new WaitForSeconds(1.2f);

            _log.AppendLine("══════ 第四章真实画面审计 ══════");
            _motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            _cc = _motor != null ? _motor.GetComponent<CharacterController>() : null;
            _cam = Camera.main;
            _fl = FindFirstObjectByType<AbyssEcho.Tools.FlashlightSystem>();
            _death = FindFirstObjectByType<AbyssEcho.Horror.PlayerDeathSystem>();

            if (_motor == null || _cam == null) { _log.AppendLine("❌ 缺组件"); Dump(); yield break; }

            // 关闭 PlayerLook（防覆盖朝向），关掉恐惧世界效果
            var look = FindFirstObjectByType<AbyssEcho.Player.PlayerLook>();
            if (look != null) look.enabled = false;
            var fv = FindFirstObjectByType<AbyssEcho.Horror.FearVisuals>();
            if (fv != null) fv.enabled = false;

            // 关掉所有怪物，避免它们在截图时乱走或被吓到
            foreach (var m in FindObjectsByType<AbyssEcho.Horror.MimicStalker>(FindObjectsSortMode.None))
                m.enabled = false;

            // 开手电
            var flType = typeof(AbyssEcho.Tools.FlashlightSystem);
            flType.GetMethod("SetLightOn", BindingFlags.Instance | BindingFlags.NonPublic)
                  ?.Invoke(_fl, new object[] { true });

            yield return new WaitForSeconds(0.4f);

            var gate = GameObject.Find("Door_HeartGate");

            // ══ 1. G5 内朝东看门洞 ══
            yield return Shot(new Vector3(-20f, 0.05f, 8f), 90f,
                "01_G5内朝东看门洞");

            // ══ 2. G5 门洞前 ══
            yield return Shot(new Vector3(-15f, 0.05f, 8f), 90f,
                "02_G5门洞前朝东");

            // ══ 3. 核心西门内 ══
            yield return Shot(new Vector3(-11.5f, 0.05f, 8f), 90f,
                "03_核心西门朝东");

            // ══ 4. 刚进 G7，朝北看 ══
            yield return Shot(new Vector3(-8f, 0.05f, 9f), 20f,
                "04_刚进G7朝北看");

            // ══ 5. G7 中央，朝北看门 ══
            yield return Shot(new Vector3(0f, 0.05f, 12f), 0f,
                "05_G7中央朝北看门");

            // ══ 6. 心瓣附近朝北看门 ══
            yield return Shot(new Vector3(0f, 0.05f, 13.5f), 0f,
                "06_心瓣前朝北看门");

            // ══ 7. 绕核心东侧，从侧面看门 ══
            yield return Shot(new Vector3(4.5f, 0.05f, 20f), 0f,
                "07_绕核心东侧朝北");

            // ══ 8. 门前 2 米 ══
            yield return Shot(new Vector3(0f, 0.05f, 22.5f), 0f,
                "08_门前2米");

            // ══ 9. 门正面近景（1 米）══
            yield return Shot(new Vector3(0f, 0.05f, 23.5f), 0f,
                "09_门前1米近景");

            // ══ 10. 解谜后门打开 ══
            if (gate != null)
            {
                var valve = GameObject.Find("HeartValve");
                var pulse = valve != null ? valve.GetComponent<AbyssEcho.Puzzle.PulseSyncPuzzle>() : null;
                if (pulse != null)
                {
                    // 直接解开谜题
                    var solveM = pulse.GetType().GetMethod("Solve",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    solveM?.Invoke(pulse, null);
                    yield return new WaitForSeconds(0.2f);

                    var door = gate.GetComponent<AbyssEcho.Interaction.DoorInteractable>();
                    if (door != null)
                    {
                        _log.AppendLine($"  解谜后 door.locked = {door.locked}");
                        door.OnInteract(gameObject);
                        yield return new WaitForSeconds(2.0f);
                        _log.AppendLine($"  开门后 IsOpen = {door.IsOpen}  门的当前位置 = {gate.transform.position}");
                    }
                }
            }

            yield return Shot(new Vector3(0f, 0.05f, 22.5f), 0f, "10_门打开后");

            // 尝试走过门
            yield return Shot(new Vector3(0f, 0.05f, 25.5f), 0f, "11_穿过门后");

            // ══ 门的渲染可见性检查 ══
            _log.AppendLine();
            _log.AppendLine("──── 门的渲染状态 ────");
            if (gate != null)
            {
                var r = gate.GetComponent<Renderer>();
                if (r != null)
                {
                    _log.AppendLine($"  Renderer.enabled = {r.enabled}");
                    _log.AppendLine($"  isVisible = {r.isVisible}");
                    _log.AppendLine($"  材质 = {(r.sharedMaterial != null ? r.sharedMaterial.name : "NULL")}");
                    _log.AppendLine($"  包围盒 = {r.bounds.center} size={r.bounds.size}");
                }
                _log.AppendLine($"  门位置 = {gate.transform.position}");
                _log.AppendLine($"  activeInHierarchy = {gate.gameObject.activeInHierarchy}");

                // 从相机到门中心的射线
                Vector3 camP = _cam.transform.position;
                Vector3 gateP = gate.transform.position;
                Vector3 dir = gateP - camP;
                if (Physics.Raycast(camP, dir.normalized, out var h, dir.magnitude + 1f,
                                    ~0, QueryTriggerInteraction.Ignore))
                    _log.AppendLine($"  相机→门射线命中: {h.collider.gameObject.name}");
                else
                    _log.AppendLine($"  相机→门射线: 未命中");
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
                var prevT = _cam.targetTexture;
                var prevA = RenderTexture.active;

                _cam.targetTexture = rt;
                _cam.Render();
                _cam.targetTexture = prevT;

                RenderTexture.active = rt;
                tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = prevA;

                byte[] png = tex.EncodeToPNG();
                string dir = FindReports();
                if (dir != null)
                {
                    string shots = Path.Combine(dir, "Ch4VisualAudit");
                    Directory.CreateDirectory(shots);
                    File.WriteAllBytes(Path.Combine(shots, name + ".png"), png);
                }

                float sum = 0f, max = 0f;
                Color[] px = tex.GetPixels();
                foreach (var p in px)
                {
                    float l = 0.299f * p.r + 0.587f * p.g + 0.114f * p.b;
                    sum += l; if (l > max) max = l;
                }
                _log.AppendLine($"  [{name}] pos={pos} yaw={yaw} 平均亮度={sum / px.Length:F4} 最大={max:F4}");
            }
            catch (System.Exception e) { _log.AppendLine($"  [{name}] 失败: {e.GetType().Name}"); }
            finally
            {
                if (tex != null) Destroy(tex);
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
            }
        }

        private string FindReports()
        {
            // ⚠️ 构建后 Application.dataPath 指向 xxx_Data，
            //   往上两级才是仓库根，需要在其中找含 "UnityProject" 的那一级。
            var di = new DirectoryInfo(Application.dataPath);
            for (int hop = 0; hop < 8 && di != null; hop++)
            {
                string candidate = Path.Combine(di.FullName, "Reports");
                if (Directory.Exists(candidate) && Directory.Exists(Path.Combine(di.FullName, "UnityProject")))
                    return candidate;
                di = di.Parent;
            }
            // 兜底：写死已知路径
            return @"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports";
        }

        private void Dump()
        {
            string text = _log.ToString();
            Debug.Log("<b>[Ch4VisualAudit]</b>\n" + text);
            string dir = FindReports();
            if (dir != null)
            {
                try { File.WriteAllText(Path.Combine(dir, "Ch4VisualAudit.txt"), text, new UTF8Encoding(false)); }
                catch { }
            }
            if (!Application.isEditor) Application.Quit(0);
        }
    }
}