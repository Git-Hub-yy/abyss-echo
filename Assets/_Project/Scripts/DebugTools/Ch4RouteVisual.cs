using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S60：从玩家视角沿路线截图，看玩家到底能看到什么。
    ///
    /// 重点拍 G3 → G5 这段：玩家从 G3 往北看，能不能看到通往 G5 的甬道，
    /// 以及进了 G5 之后往北看能不能看到门洞。
    /// </summary>
    public class Ch4RouteVisual : MonoBehaviour
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
            yield return new WaitForSeconds(1.0f);

            _log.AppendLine("══════ 第四章路线视角截图 ══════");

            _motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            _cc = _motor != null ? _motor.GetComponent<CharacterController>() : null;
            _cam = Camera.main;
            if (_motor == null || _cam == null) { _log.AppendLine("❌ 缺组件"); Dump(); yield break; }

            var look = FindFirstObjectByType<AbyssEcho.Player.PlayerLook>();
            if (look != null) look.enabled = false;
            var fv = FindFirstObjectByType<AbyssEcho.Horror.FearVisuals>();
            if (fv != null) fv.enabled = false;
            foreach (var m in FindObjectsByType<AbyssEcho.Horror.MimicStalker>(FindObjectsSortMode.None))
                m.gameObject.SetActive(false);

            var fl = FindFirstObjectByType<AbyssEcho.Tools.FlashlightSystem>();
            typeof(AbyssEcho.Tools.FlashlightSystem)
                .GetMethod("SetLightOn", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(fl, new object[] { true });

            yield return new WaitForSeconds(0.4f);

            // ── G2 环形廊 ──
            yield return Shot(new Vector3(0f, 0.05f, -12f), 0f, "01_G2朝北看G1");
            yield return Shot(new Vector3(0f, 0.05f, -12f), -90f, "02_G2朝西看G3甬道");
            yield return Shot(new Vector3(-10f, 0.05f, -12f), -90f, "03_G2西端朝西");

            // ── G3 消化囊 ──
            yield return Shot(new Vector3(-13f, 0.05f, -12f), -90f, "04_G3甬道朝西");
            yield return Shot(new Vector3(-16f, 0.05f, -12f), -90f, "05_G3房间中心朝西");
            yield return Shot(new Vector3(-19f, 0.05f, -10f), 0f, "06_G3朝北看G5甬道");
            yield return Shot(new Vector3(-19f, 0.05f, -7f), 0f, "07_G3北边界朝北");

            // ── G5 组织层 ──
            yield return Shot(new Vector3(-19f, 0.05f, -4f), 0f, "08_刚进G5朝北");
            yield return Shot(new Vector3(-19f, 0.05f, 0f), 0f, "09_G5中心朝北看门洞");
            yield return Shot(new Vector3(-19f, 0.05f, 3f), 0f, "10_G5靠近北墙");

            // ── 过渡区 ──
            yield return Shot(new Vector3(-19f, 0.05f, 5.6f), 0f, "11_出门洞朝北");
            yield return Shot(new Vector3(-19f, 0.05f, 5.6f), -90f, "12_过渡区朝西");
            yield return Shot(new Vector3(-19f, 0.05f, 5.6f), 90f, "13_过渡区朝东");
            yield return Shot(new Vector3(-15f, 0.05f, 6f), 90f, "14_过渡区东行");

            // ── 连廊 ──
            yield return Shot(new Vector3(-11.8f, 0.05f, 7.2f), 0f, "15_连廊朝北");
            yield return Shot(new Vector3(-11f, 0.05f, 8f), 90f, "16_连廊朝东看G7门");
            yield return Shot(new Vector3(-9.5f, 0.05f, 8f), 90f, "17_进G7");

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

                string shots = @"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch4RouteShots";
                Directory.CreateDirectory(shots);
                File.WriteAllBytes(Path.Combine(shots, name + ".png"), tex.EncodeToPNG());

                float sum = 0f;
                Color[] px = tex.GetPixels();
                foreach (var p in px) sum += 0.299f * p.r + 0.587f * p.g + 0.114f * p.b;
                _log.AppendLine($"  [{name}] ({pos.x:F0},{pos.z:F1}) yaw={yaw} 亮度={sum / px.Length:F4}");
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
            Debug.Log("<b>[Ch4RouteVisual]</b>\n" + text);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch4RouteVisual.txt",
                                  text, new UTF8Encoding(false));
            }
            catch { }
            if (!Application.isEditor) Application.Quit(0);
        }
    }
}