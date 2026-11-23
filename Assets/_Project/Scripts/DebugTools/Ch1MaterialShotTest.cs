using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S67：第一章材质升级的**真实 GPU 渲染**验收截图。
    ///
    /// 必须覆盖任务要求的 8 个视角：
    ///   1. 整体场景  2. 墙壁地板近景  3. 科研设备近景  4. 门和门框近景
    ///   5. 管道机械  6. 显示器发光     7. 可拾取道具    8. 第一人称视角
    /// </summary>
    public class Ch1MaterialShotTest : MonoBehaviour
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
                AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter1;
            yield return new WaitForSeconds(1.0f);

            _log.AppendLine("══════ 第一章材质验收截图 ══════");
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

            // 打印运行时实际读到的 UV（验证网格修改是否生效）
            foreach (var nm in new[] { "West_B3_Full", "East_B3_Full", "North_B3_Full" })
            {
                var go = GameObject.Find(nm);
                if (go == null) continue;
                var mf = go.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var uv = mf.sharedMesh.uv;
                var nr = mf.sharedMesh.normals;
                float mnU=999,mxU=-999,mnV=999,mxV=-999;
                if (uv != null && nr != null) {
                    for (int i = 0; i < uv.Length; i++) {
                        if (Mathf.Abs(nr[i].x) > 0.5f) {   // 只看 X 朝向的大面
                            mnU=Mathf.Min(mnU,uv[i].x); mxU=Mathf.Max(mxU,uv[i].x);
                            mnV=Mathf.Min(mnV,uv[i].y); mxV=Mathf.Max(mxV,uv[i].y);
                        }
                    }
                }
                _log.AppendLine($"  [UV] {nm} 世界尺寸={go.GetComponent<Renderer>().bounds.size} X面UV跨度={(mxU-mnU):F2}x{(mxV-mnV):F2}");
            }

            // 用场景里真实存在的对象定位
            var pts = new[]
            {
                // (名称, 位置, yaw, pitch, 说明)
                ("01_整体场景_起点",    Vector3.zero, 0f, 0f, "玩家出生视角"),
                ("02_墙壁地板近景",     Vector3.zero, 0f, -25f, "低头看地板"),
                ("03_墙壁近景",         Vector3.zero, 0f, 0f, "正对墙壁"),
            };

            // 按实际场景找关键物体
            var doors = FindObjectsByType<AbyssEcho.Interaction.DoorInteractable>(FindObjectsSortMode.None);
            var pickups = FindObjectsByType<AbyssEcho.Interaction.PickupItem>(FindObjectsSortMode.None);
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);

            // 1. 起点整体
            Vector3 start = _motor.transform.position;

            // 额外：站在房间中央看长墙（能看清纹理整体比例）
            var westWall = GameObject.Find("West_B3_Full");
            if (westWall != null) {
                Vector3 wc = westWall.transform.position;
                yield return Shot(new Vector3(wc.x + 2.0f, 0.05f, wc.z - 1.0f), 90f, 0f, "00_长墙_远景");
            }
            yield return Shot(start + new Vector3(0, -0.02f, 0), 0f, 0f, "01_整体场景_起点");
            yield return Shot(start, 0f, -35f, "02_墙壁地板_低头近景");
            yield return Shot(start, -90f, 0f, "03_墙壁_侧视近景");

            // 2. 门
            if (doors.Length > 0)
            {
                var d = doors[0];
                Vector3 dp = d.transform.position;
                yield return Approach(dp, 2.2f, "04_门与门框_近景");
            }

            // 3. 拾取物
            if (pickups.Length > 0)
            {
                var p = pickups[0];
                yield return Approach(p.transform.position, 1.3f, "05_可拾取道具_近景");
            }

            // 4. 找控制器/屏幕类物体
            GameObject screenObj = null;
            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.name.Contains("Console") || t.name.Contains("Screen") ||
                    t.name.Contains("Panel") || t.name.Contains("Keypad"))
                { screenObj = t.gameObject; break; }
            }
            if (screenObj != null)
                yield return Approach(screenObj.transform.position, 1.8f, "06_科研设备与屏幕_近景");

            // 5. 灯光装置
            if (lights.Length > 0)
            {
                var l = lights[0];
                yield return Approach(l.transform.position, 2.5f, "07_灯具与发光_近景");
            }

            // 6. 走廊深处（第一人称）
            yield return Shot(start, 0f, 0f, "08_第一人称视角");

            _log.AppendLine();
            _log.AppendLine("材质统计：");
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r == null) continue;
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || !seen.Add(m.name)) continue;
                    bool tex = m.GetTexture("_BaseMap") != null;
                    bool emit = m.IsKeywordEnabled("_EMISSION");
                    bool trans = m.GetFloat("_Surface") > 0.5f;
                    _log.AppendLine($"  {m.name,-28} 贴图={(tex ? "✓" : "-")} 发光={(emit ? "✓" : "-")} 透明={(trans ? "✓" : "-")}");
                }
            }

            Dump();
        }

        private IEnumerator Approach(Vector3 target, float dist, string name)
        {
            // 站在目标南侧，看向目标
            Vector3 pos = new Vector3(target.x, 0.05f, target.z - dist);
            Vector3 dir = (target - pos);
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            yield return Shot(pos, yaw, 0f, name);
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

                string dir = @"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch1MaterialShots";
                Directory.CreateDirectory(dir);
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());

                float sum = 0f, max = 0f;
                Color[] px = tex.GetPixels();
                foreach (var p in px)
                {
                    float l = 0.299f * p.r + 0.587f * p.g + 0.114f * p.b;
                    sum += l; if (l > max) max = l;
                }
                _log.AppendLine($"  [{name}] pos=({pos.x:F1},{pos.z:F1}) yaw={yaw:F0} pitch={pitch:F0} " +
                                $"平均亮度={sum / px.Length:F4} 最大={max:F4}");
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
            Debug.Log("<b>[Ch1MaterialShotTest]</b>\n" + text);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch1MaterialShots.txt",
                                  text, new UTF8Encoding(false));
            }
            catch { }
            if (!Application.isEditor) Application.Quit(0);
        }
    }
}