using System.Collections;
using System.IO;
using System.Text;
using AbyssEcho.Horror;
using AbyssEcho.Player;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S5 恐怖系统视觉验证：
    ///   1. 在正常状态下截一张
    ///   2. 把恐惧值拉到最高，再截一张（应能看到暗角/色差明显增强）
    ///   3. 关闭部分灯光，再截一张（应明显变暗）
    ///
    /// 用像素亮度对比来客观证明效果确实生效，而不只是"看起来像"。
    /// </summary>
    public class S5VisualCheck : MonoBehaviour
    {
        public bool runOnStart = true;
        private const int W = 960, H = 540;

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            var cam = Camera.main;
            if (cam == null) { Debug.LogError("[S5Visual] 无相机"); yield break; }

            var motor = FindFirstObjectByType<PlayerMotor>();
            var look = FindFirstObjectByType<PlayerLook>();
            var fear = FearSystem.Instance;
            var cc = motor != null ? motor.GetComponent<CharacterController>() : null;

            string dir = Path.Combine(Application.persistentDataPath, "Shots");
            Directory.CreateDirectory(dir);

            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            rt.Create();
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            var sb = new StringBuilder();
            sb.AppendLine("S5 视觉验证（亮度对比）");
            sb.AppendLine("=====================================");

            // 站在 B2 宿舍走廊（有多个灯，适合观察闪烁与暗角）
            if (cc != null) cc.enabled = false;
            if (motor != null) motor.transform.position = new Vector3(-10f, 0.05f, 10.9f);
            if (cc != null) cc.enabled = true;
            if (look != null) { look.SetYaw(90f); look.SetPitch(0f); }

            yield return new WaitForSeconds(1.0f);

            // ① 平静状态
            if (fear != null) fear.SetFear(0f);
            yield return new WaitForSeconds(0.4f);
            float calm = Shot(cam, rt, tex, Path.Combine(dir, "S5_01_平静.png"));
            sb.AppendLine($"[1] 平静状态（恐惧=0）    亮度 = {calm:F4}");

            // ② 恐惧拉满（应看到暗角加深、色差增强）
            if (fear != null) fear.SetFear(1f);
            yield return new WaitForSeconds(0.6f);
            float panic = Shot(cam, rt, tex, Path.Combine(dir, "S5_02_恐惧拉满.png"));
            sb.AppendLine($"[2] 恐惧拉满（恐惧=1）    亮度 = {panic:F4}");
            sb.AppendLine($"    差异 = {Mathf.Abs(panic - calm):F4}  {(Mathf.Abs(panic - calm) > 0.005f ? "✅ 视觉后处理已变化" : "⚠ 变化不明显")}");

            // ③ 验证后处理确实随恐惧变化
            //    注意：点光在离屏渲染下不计入（S3B 已确认的环境限制），
            //    所以这里不测"熄灯变暗"，改为测后处理数值本身。
            if (fear != null) fear.SetFear(0f);
            yield return new WaitForSeconds(0.3f);
            // 诊断：确认 Volume 情况
            var allVols = FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None);
            sb.AppendLine($"[诊断] 场景 Volume 数量 = {allVols.Length}");
            foreach (var v in allVols)
            {
                int compCount = v.sharedProfile != null ? v.sharedProfile.components.Count : -1;
                sb.AppendLine($"[诊断]   {v.name}: isGlobal={v.isGlobal} weight={v.weight} profile={(v.sharedProfile != null ? v.sharedProfile.name : "null")} 组件数={compCount}");
                if (v.sharedProfile != null)
                {
                    foreach (var comp in v.sharedProfile.components)
                        sb.AppendLine($"[诊断]      组件: {comp.GetType().Name} active={comp.active}");
                }
            }
            var v0 = FindFirstObjectByType<AbyssEcho.Horror.FearVisuals>();
            string calmVig = ReadVignette();
            string calmAb = ReadAberration();

            if (fear != null) fear.SetFear(1f);
            yield return new WaitForSeconds(0.3f);
            string panicVig = ReadVignette();
            string panicAb = ReadAberration();

            sb.AppendLine($"[3] 后处理随恐惧变化：");
            sb.AppendLine($"    Vignette   平静={calmVig}  恐惧={panicVig}");
            sb.AppendLine($"    色差       平静={calmAb}  恐惧={panicAb}");
            bool vigChanged = calmVig != panicVig;
            sb.AppendLine($"    {(vigChanged ? "✅ 后处理确实随恐惧变化" : "⚠ 后处理未变化")}");

            if (fear != null) fear.SetFear(0f);

            rt.Release(); Destroy(rt); Destroy(tex);

            string text = sb.ToString();
            Debug.Log("<b>[S5Visual]</b>\n" + text);
            try { File.WriteAllText(Path.Combine(dir, "S5视觉验证.txt"), text, new UTF8Encoding(false)); } catch { }

            yield return new WaitForSeconds(0.3f);
            if (!Application.isEditor) Application.Quit(0);
        }

        private static string ReadVignette()
        {
            var vols = FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None);
            foreach (var v in vols)
            {
                if (v.sharedProfile == null) continue;
                // 优先用 components 列表直接查找（TryGet 在部分时机不可靠）
                foreach (var comp in v.sharedProfile.components)
                {
                    if (comp is UnityEngine.Rendering.Universal.Vignette vig && comp.active)
                        return vig.intensity.value.ToString("F3");
                }
            }
            return "n/a";
        }

        private static string ReadAberration()
        {
            var vols = FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None);
            foreach (var v in vols)
            {
                if (v.sharedProfile == null) continue;
                foreach (var comp in v.sharedProfile.components)
                {
                    if (comp is UnityEngine.Rendering.Universal.ChromaticAberration ca && comp.active)
                        return ca.intensity.value.ToString("F3");
                }
            }
            return "n/a";
        }
        private float Shot(Camera cam, RenderTexture rt, Texture2D tex, string path)
        {
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.Render();
            cam.targetTexture = prev;

            var pa = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = pa;

            byte[] png = tex.EncodeToPNG();
            File.WriteAllBytes(path, png);

            var px = tex.GetPixels();
            double sum = 0;
            for (int i = 0; i < px.Length; i++)
                sum += 0.2126 * px[i].r + 0.7152 * px[i].g + 0.0722 * px[i].b;
            return (float)(sum / px.Length);
        }
    }
}