using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S68：第二章材质验收截图（真实 GPU 渲染）。
    /// </summary>
    public class Ch2MaterialShotTest : MonoBehaviour
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
                AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter2;
            yield return new WaitForSeconds(1.0f);

            _log.AppendLine("══════ 第二章材质验收截图 ══════");
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

            // 各房间中心（来自 ChapterTwoLayout）
            var rooms = new (string name, Vector3 center, string label)[]
            {
                ("D1", new Vector3(0f, 0f, 0f),    "01_闸门过渡舱"),
                ("D2", new Vector3(0f, 0f, 11f),   "02_被水淹的大厅"),
                ("D5", new Vector3(0f, 0f, 24f),   "03_生活区"),
                ("E1", new Vector3(0f, 0f, 35f),   "04_声学实验室"),
                ("E4", new Vector3(0f, 0f, 45f),   "05_深渊观景台"),
                ("D3", new Vector3(-14f, 0f, 11f), "06_泵房"),
                ("D4", new Vector3(14.5f, 0f, 11f),"07_档案室"),
                ("E3", new Vector3(12f, 0f, 35f),  "08_控制室"),
            };

            foreach (var (id, center, label) in rooms)
            {
                // 站在房间入口，朝房间内看
                Vector3 pos = new Vector3(center.x, 0.05f, center.z - 3.0f);
                yield return Shot(pos, 0f, 0f, label);
            }

            // 生活区近景（任务重点）
            // 生活区家具：站在房间中央看左侧（长桌）和右侧（储物柜）
            yield return Shot(new Vector3(1.5f, 0.05f, 24.0f), -90f, -5f, "09_生活区家具近景");
            yield return Shot(new Vector3(2.5f, 0.05f, 22.0f), 90f, -5f, "10_储物柜_旧白近景");

            // 门与设备
            var doors = FindObjectsByType<AbyssEcho.Interaction.DoorInteractable>(FindObjectsSortMode.None);
            if (doors.Length > 0)
            {
                var d = doors[0];
                Vector3 dp = d.transform.position;
                yield return Shot(new Vector3(dp.x, 0.05f, dp.z - 2.2f), 0f, 0f, "11_门近景");
            }

            // 第一人称
            yield return Shot(new Vector3(0f, 0.05f, 5.0f), 0f, 0f, "12_第一人称_甬道");

            // 材质统计
            _log.AppendLine();
            _log.AppendLine("材质统计：");
            var seen = new HashSet<string>();
            int nullCount = 0;
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r == null) continue;
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) { nullCount++; continue; }
                    if (!seen.Add(m.name)) continue;
                    bool tex = m.GetTexture("_BaseMap") != null;
                    bool emit = m.IsKeywordEnabled("_EMISSION");
                    bool trans = m.GetFloat("_Surface") > 0.5f;
                    _log.AppendLine($"  {m.name,-28} 贴图={(tex ? "✓" : "-")} 发光={(emit ? "✓" : "-")} 透明={(trans ? "✓" : "-")}");
                }
            }
            _log.AppendLine($"  空材质槽 = {nullCount}");

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

                string dir = @"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch2MaterialShots";
                Directory.CreateDirectory(dir);
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());

                float sum = 0f, max = 0f;
                var px = tex.GetPixels();
                foreach (var p in px)
                {
                    float l = 0.299f * p.r + 0.587f * p.g + 0.114f * p.b;
                    sum += l; if (l > max) max = l;
                }
                _log.AppendLine($"  [{name}] pos=({pos.x:F1},{pos.z:F1}) 平均亮度={sum / px.Length:F4} 最大={max:F4}");
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
            Debug.Log("<b>[Ch2MaterialShotTest]</b>\n" + text);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch2MaterialShots.txt",
                                  text, new UTF8Encoding(false));
            }
            catch { }
            if (!Application.isEditor) Application.Quit(0);
        }
    }
}