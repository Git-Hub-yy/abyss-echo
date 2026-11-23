using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S69：通用材质验收截图（章节由 AE_CH 环境变量指定 3/4/5）。
    /// </summary>
    public class MaterialShotTest : MonoBehaviour
    {
        public bool runOnStart = true;
        private readonly StringBuilder _log = new StringBuilder();
        private AbyssEcho.Player.PlayerMotor _motor;
        private CharacterController _cc;
        private Camera _cam;
        private int _chapter = 3;

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            // 从命令行参数读取章节号（环境变量在 Start-Process 下不可靠）
            _chapter = 3;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-ch")
                {
                    int.TryParse(args[i + 1], out _chapter);
                    break;
                }
            }
            if (_chapter < 3 || _chapter > 5) _chapter = 3;
            Debug.Log($"[MatShot] 目标章节 = {_chapter}");

            AbyssEcho.LevelGen.ChapterLayout.Current =
                (AbyssEcho.LevelGen.ChapterLayout.Chapter)_chapter;
            yield return new WaitForSeconds(1.0f);

            _log.AppendLine($"══════ 第{_chapter}章材质验收截图 ══════");

            _motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            _cc = _motor != null ? _motor.GetComponent<CharacterController>() : null;
            _cam = Camera.main;
            if (_motor == null || _cam == null) { _log.AppendLine("❌ 缺组件"); Dump(); yield break; }

            var look = FindFirstObjectByType<AbyssEcho.Player.PlayerLook>();
            if (look != null) look.enabled = false;
            var fv = FindFirstObjectByType<AbyssEcho.Horror.FearVisuals>();
            if (fv != null) fv.enabled = false;

            var fl = FindFirstObjectByType<AbyssEcho.Tools.FlashlightSystem>();
            typeof(AbyssEcho.Tools.FlashlightSystem)
                .GetMethod("SetLightOn", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(fl, new object[] { true });

            yield return new WaitForSeconds(0.4f);

            // 自动探测：找出场景中分布最广的几个位置
            var renderers = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            float minZ = 9999, maxZ = -9999;
            var xs = new List<float>();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                Vector3 p = r.transform.position;
                minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z);
                xs.Add(p.x);
            }

            // 按 Z 轴均匀取 6 个点做全景
            int shots = 6;
            for (int i = 0; i < shots; i++)
            {
                float z = Mathf.Lerp(minZ + 2f, maxZ - 2f, i / (float)(shots - 1));
                yield return Shot(new Vector3(0f, 0.05f, z), 0f, 0f, $"{i + 1:00}_全景_Z{z:F0}");
            }

            // 走廊视角
            yield return Shot(new Vector3(0f, 0.05f, minZ + 6f), 0f, 0f, "07_走廊");

            // 道具/设备近景：找几个有交互组件的
            var pickups = FindObjectsByType<AbyssEcho.Interaction.PickupItem>(FindObjectsSortMode.None);
            if (pickups.Length > 0)
            {
                var p = pickups[0].transform.position;
                yield return Shot(new Vector3(p.x, 0.05f, p.z - 1.5f), 0f, -15f, "08_可拾取道具近景");
            }

            var doors = FindObjectsByType<AbyssEcho.Interaction.DoorInteractable>(FindObjectsSortMode.None);
            if (doors.Length > 0)
            {
                var d = doors[0].transform.position;
                yield return Shot(new Vector3(d.x, 0.05f, d.z - 2.5f), 0f, 0f, "09_门与门框");
            }

            // 手电筒照射特写：面对最近的墙
            yield return Shot(new Vector3(0f, 0.05f, (minZ + maxZ) * 0.5f), 0f, -8f, "10_手电筒照射地面");

            // 有机材质特写：从室内中心朝有机墙的方向看（避免贴墙）
            var orgWalls = new System.Collections.Generic.List<Renderer>();
            foreach (var r in renderers)
            {
                if (r == null || r.sharedMaterial == null) continue;
                if (!r.sharedMaterial.name.StartsWith("M_Org")) continue;
                var sz = r.bounds.size;
                // 只要墙类：一个轴很薄，另两个轴较长
                bool isWall = (sz.x < 0.6f && (sz.y > 2f || sz.z > 2f))
                           || (sz.z < 0.6f && (sz.y > 2f || sz.x > 2f))
                           || (sz.y < 0.6f && (sz.x > 2f || sz.z > 2f));
                if (!isWall) continue;
                orgWalls.Add(r);
            }
            _log.AppendLine($"  找到 {orgWalls.Count} 面有机墙体");

            for (int i = 0; i < Mathf.Min(2, orgWalls.Count); i++)
            {
                var r = orgWalls[i];
                Vector3 wc = r.bounds.center;
                Vector3 ws = r.bounds.size;

                // 判断墙的法线方向：最薄的轴
                Vector3 eye;
                float yaw = 0f;
                if (ws.x < ws.z && ws.x < ws.y)
                {
                    // 法线 X → 从 +X 或 -X 方向看，距离 3 米
                    float sign = wc.x > 0 ? -1f : 1f;
                    eye = new Vector3(wc.x + sign * 3.5f, 0.05f, wc.z);
                    yaw = sign > 0 ? 90f : -90f;
                }
                else if (ws.z < ws.x && ws.z < ws.y)
                {
                    // 法线 Z → 从 +Z 或 -Z 看
                    float sign = wc.z > 0 ? -1f : 1f;
                    eye = new Vector3(wc.x, 0.05f, wc.z + sign * 3.5f);
                    yaw = sign > 0 ? 0f : 180f;
                }
                else
                {
                    // 地板/天花板：站旁边俯视
                    eye = new Vector3(wc.x, 0.05f, wc.z - 3.0f);
                    yaw = 0f;
                }

                yield return Shot(eye, yaw, -8f, $"11_有机特写_{i + 1}_{r.gameObject.name}");
            }
            // 第四章：核心腔路径（G5 北门 → 过渡区 → 连廊）
            if (_chapter == 4)
            {
                yield return Shot(new Vector3(-19f, 0.05f, 2.0f), 0f, -12f, "12_核心腔路径_北门");
                yield return Shot(new Vector3(-19f, 0.05f, 6.0f), 90f, -10f, "13_核心腔路径_过渡区");
                yield return Shot(new Vector3(-16f, 0.05f, 8.0f), 90f, -8f, "14_核心腔路径_补地板");
            }

            // 材质统计
            _log.AppendLine();
            _log.AppendLine("材质统计：");
            var seen = new HashSet<string>();
            int nulls = 0;
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r == null) continue;
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) { nulls++; continue; }
                    if (!seen.Add(m.name)) continue;
                    bool tex = m.GetTexture("_BaseMap") != null;
                    bool emit = m.IsKeywordEnabled("_EMISSION") &&
                                m.GetColor("_EmissionColor").maxColorComponent > 0.01f;
                    bool trans = m.GetFloat("_Surface") > 0.5f;
                    _log.AppendLine($"  {m.name,-28} tex={(tex ? "✓" : "-")} emit={(emit ? "✓" : "-")} trans={(trans ? "✓" : "-")}");
                }
            }
            _log.AppendLine($"  空材质槽 = {nulls}");

            Dump();
        }

        private IEnumerator Shot(Vector3 pos, float yaw, float pitch, string name)
        {
            if (_cc != null) _cc.enabled = false;
            _motor.transform.position = pos;
            _motor.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            if (_cc != null) _cc.enabled = true;

            yield return null;
            yield return null;
            yield return new WaitForEndOfFrame();

            int w = 960, h = 540;
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

                string dir = $@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch{_chapter}MaterialShots";
                Directory.CreateDirectory(dir);
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());

                float sum = 0f, max = 0f;
                var px = tex.GetPixels();
                foreach (var p in px)
                {
                    float l = 0.299f * p.r + 0.587f * p.g + 0.114f * p.b;
                    sum += l; if (l > max) max = l;
                }
                _log.AppendLine($"  [{name}] pos=({pos.x:F1},{pos.z:F1}) 亮度={sum / px.Length:F4} 最大={max:F4}");
            }
            catch (System.Exception e) { _log.AppendLine($"  [{name}] 失败 {e.GetType().Name}"); }
            finally
            {
                if (tex != null) Destroy(tex);
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
            }
        }

        private void Dump()
        {
            string text = _log.ToString();
            Debug.Log("<b>[MaterialShotTest]</b>\n" + text);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.WriteAllText($@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch{_chapter}MaterialShots.txt",
                                  text, new UTF8Encoding(false));
            }
            catch { }
            if (!Application.isEditor) Application.Quit(0);
        }
    }
}