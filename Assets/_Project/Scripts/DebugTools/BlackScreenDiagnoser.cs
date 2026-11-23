using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// 全黑问题排查工具：逐项关闭可疑因素，用像素统计判断哪一项导致黑屏。
    ///
    /// 输出：Reports/全黑排查报告.txt
    ///
    /// 排查项（每项单独测量平均亮度）：
    ///   1. 当前状态
    ///   2. 关闭后处理
    ///   3. 关闭深度雾
    ///   4. 关闭 Volume
    ///   5. 提高环境光
    ///   6. 关闭相机抗锯齿
    ///   7. 基线：直接清屏色
    /// </summary>
    public class BlackScreenDiagnoser : MonoBehaviour
    {
        public bool runOnStart = true;
        private int W = 480, H = 270;
        private float LastDarkPct, LastMidPct, LastBrightPct, LastVariance;

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        public IEnumerator Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("ABYSS ECHO · 全黑问题排查");
            sb.AppendLine("=====================================");
            sb.AppendLine("=====================================");

            var cam = Camera.main;
            if (cam == null) { Debug.LogError("[排查] 无相机"); yield break; }

            var camData = cam.GetComponent<UniversalAdditionalCameraData>();
            var volumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
            var fogCtl = FindFirstObjectByType<AbyssEcho.Rendering.DepthFogController>();
            var motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            var look = FindFirstObjectByType<AbyssEcho.Player.PlayerLook>();

            // 传送到 A1 走廊（有灯、有材质）
            var cc = motor != null ? motor.GetComponent<CharacterController>() : null;
            if (cc != null) cc.enabled = false;
            if (motor != null) motor.transform.position = new Vector3(0f, 0.05f, -35.5f);
            if (cc != null) cc.enabled = true;
            if (look != null) { look.SetYaw(0f); look.SetPitch(0f); }

            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);

            yield return new WaitForSeconds(0.4f);

            // ⓪ 雾参数（雾色被误判为 1 时整屏会变成雾色）
            if (fogCtl != null && fogCtl.fogMaterial != null)
            {
                var fm = fogCtl.fogMaterial;
                sb.AppendLine($"雾参数: 颜色={fm.GetColor("_FogColor")} 密度={fm.GetFloat("_FogDensity"):F4} " +
                              $"起始={fm.GetFloat("_FogStart"):F2} 衰减={fm.GetFloat("_FogFalloff"):F2} " +
                              $"高度强度={fm.GetFloat("_HeightFogStrength"):F2}");
                sb.AppendLine($"  控制器: color={fogCtl.fogColor} density={fogCtl.fogDensity:F4} start={fogCtl.fogStart:F2}");
            }
            else sb.AppendLine("雾控制器: 缺失");

            // ① 当前
            float b0 = Measure(cam, rt, tex);
            sb.AppendLine($"[1] 当前状态  亮度={b0:F4} 暗={LastDarkPct:F0}% 中={LastMidPct:F0}% 亮={LastBrightPct:F0}% 方差={LastVariance:F4} " + (LastVariance < 0.01f ? "★纯色(无几何)" : "有几何内容"));

            // ② 关闭后处理
            bool pp = camData != null ? camData.renderPostProcessing : false;
            if (camData != null) camData.renderPostProcessing = false;
            yield return new WaitForSeconds(0.15f);
            float b1 = Measure(cam, rt, tex);
            sb.AppendLine($"[2] 关闭后处理         亮度 = {b1:F4}   {(b1 > b0 + 0.01f ? "★ 后处理是黑屏原因" : "非后处理")}");
            if (camData != null) camData.renderPostProcessing = pp;

            // ③ 关闭深度雾
            bool fogActive = true;
            if (fogCtl != null && fogCtl.fogMaterial != null)
            {
                fogActive = fogCtl.fogMaterial.GetFloat("_FogDensity") > 0f;
                fogCtl.fogMaterial.SetFloat("_FogDensity", 0f);
            }
            yield return new WaitForSeconds(0.15f);
            float b2 = Measure(cam, rt, tex);
            sb.AppendLine($"[3] 关闭深度雾         亮度 = {b2:F4}   {(b2 > b0 + 0.01f ? "★ 深度雾是黑屏原因" : "非深度雾")}");
            if (fogCtl != null && fogCtl.fogMaterial != null && fogActive)
                fogCtl.Apply();

            // ④ 关闭 Volume
            foreach (var v in volumes) v.enabled = false;
            yield return new WaitForSeconds(0.15f);
            float b3 = Measure(cam, rt, tex);
            sb.AppendLine($"[4] 关闭 Volume        亮度 = {b3:F4}   {(b3 > b0 + 0.01f ? "★ Volume 是黑屏原因" : "非 Volume")}");
            foreach (var v in volumes) v.enabled = true;

            // ⑤ 提高环境光
            var savedAmbient = RenderSettings.ambientLight;
            var savedMode = RenderSettings.ambientMode;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.white;
            yield return new WaitForSeconds(0.15f);
            float b4 = Measure(cam, rt, tex);
            sb.AppendLine($"[5] 环境光=纯白         亮度 = {b4:F4}   {(b4 > 0.3f ? "几何与材质正常" : "★ 几何/材质有问题")}");
            RenderSettings.ambientLight = savedAmbient;
            RenderSettings.ambientMode = savedMode;

            // ⑤b 关闭 Tonemapping（ACES 可能压暗画面）
            var toneVolumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
            foreach (var v in toneVolumes)
            {
                if (v.sharedProfile == null) continue;
                if (v.sharedProfile.TryGet<Tonemapping>(out var tm)) tm.active = false;
            }
            yield return new WaitForSeconds(0.15f);
            float b5b = Measure(cam, rt, tex);
            sb.AppendLine($"[5b] 关闭 Tonemapping  亮度 = {b5b:F4} 方差 = {LastVariance:F4}   {(b5b > b0 + 0.01f ? "★ ACES 压暗了画面" : "非 ACES")}");
            foreach (var v in toneVolumes)
            {
                if (v.sharedProfile == null) continue;
                if (v.sharedProfile.TryGet<Tonemapping>(out var tm)) tm.active = true;
            }

            // ⑥ 关闭抗锯齿
            var savedAA = camData != null ? camData.antialiasing : AntialiasingMode.None;
            if (camData != null) camData.antialiasing = AntialiasingMode.None;
            yield return new WaitForSeconds(0.15f);
            float b5 = Measure(cam, rt, tex);
            sb.AppendLine($"[6] 关闭抗锯齿         亮度 = {b5:F4}   {(b5 > b0 + 0.01f ? "★ 抗锯齿是黑屏原因" : "非抗锯齿")}");
            if (camData != null) camData.antialiasing = savedAA;

            // ⑦ 相机基础信息
            sb.AppendLine();
            sb.AppendLine("相机信息：");
            sb.AppendLine($"  position = {cam.transform.position}");
            sb.AppendLine($"  rotation = {cam.transform.eulerAngles}");
            sb.AppendLine($"  far = {cam.farClipPlane}, near = {cam.nearClipPlane}");
            sb.AppendLine($"  clearFlags = {cam.clearFlags}, bg = {cam.backgroundColor}");
            sb.AppendLine($"  renderPostProcessing = {pp}");
            sb.AppendLine($"  antialiasing = {savedAA}");
            sb.AppendLine($"  URP cameraData = {(camData != null ? "存在" : "无")}");

            // ⑧ 光源统计
            sb.AppendLine();
            int on = 0;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.enabled && l.intensity > 0.01f && l.gameObject.activeInHierarchy) on++;
            sb.AppendLine($"有效光源数 = {on}");

            // ⑧b 关键测试：用与关卡几何完全相同的方式创建一个方块
            //    （CreatePrimitive + 相同材质 + 相同 isStatic），看是否可见。
            //    这能区分"渲染管线问题"与"关卡几何数据问题"。
            sb.AppendLine();
            sb.AppendLine("测试：复制一个真实关卡渲染器");
            Renderer sampleRenderer = null;
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r.name.StartsWith("Floor_") || r.name.StartsWith("North_"))
                { sampleRenderer = r; break; }
            }
            if (sampleRenderer != null)
            {
                sb.AppendLine($"  样例: {sampleRenderer.name}, material={(sampleRenderer.sharedMaterial != null ? sampleRenderer.sharedMaterial.name : "null")}");
                var dx = sampleRenderer.bounds.center - cam.transform.position;
                sb.AppendLine($"  样例包围盒中心: {sampleRenderer.bounds.center}");
                sb.AppendLine($"  样例尺寸: {sampleRenderer.bounds.size}");
                sb.AppendLine($"  距相机: {dx.magnitude:F2} 米");
                sb.AppendLine($"  renderer.enabled={sampleRenderer.enabled}, isVisible={sampleRenderer.isVisible}");

                // 在相机前方 1.5m 复制一个同样材质的方块
                var clone = GameObject.CreatePrimitive(PrimitiveType.Cube);
                clone.name = "DIAG_CLONE";
                clone.transform.position = cam.transform.position + cam.transform.forward * 1.5f;
                clone.transform.localScale = new Vector3(1.2f, 1.2f, 0.15f);
                clone.GetComponent<Renderer>().sharedMaterial = sampleRenderer.sharedMaterial;
                yield return new WaitForSeconds(0.2f);
                float bClone = Measure(cam, rt, tex);
                sb.AppendLine($"  用关卡材质复刻方块后亮度 = {bClone:F4} 方差 = {LastVariance:F4}");
                sb.AppendLine(LastVariance > 0.05f
                    ? "  ✅ 关卡材质能正常渲染 —— 问题在几何体位置/剔除"
                    : "  ❌ 连关卡材质都渲染不出 —— 材质本身有问题");
                Destroy(clone);
            }
            else sb.AppendLine("  找不到关卡渲染器");

            // ⑨ 终极测试：在相机正前方放一个巨大的亮色 Cube，看能否渲染出来
            //    这能区分"渲染管线坏了"和"关卡几何有问题"
            sb.AppendLine();
            sb.AppendLine("终极测试：相机制造一个 1m 外的巨型亮色方块");
            var testCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            testCube.name = "DIAG_CUBE";
            testCube.transform.position = cam.transform.position + cam.transform.forward * 2f;
            testCube.transform.localScale = new Vector3(1.5f, 1.5f, 0.2f);
            var testMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (testMat.HasProperty("_BaseColor")) testMat.SetColor("_BaseColor", new Color(1f, 0.2f, 0.2f));
            testCube.GetComponent<Renderer>().sharedMaterial = testMat;
            yield return new WaitForSeconds(0.2f);

            float bTest = Measure(cam, rt, tex);
            sb.AppendLine($"  加方块后亮度 = {bTest:F4} 方差 = {LastVariance:F4}");
            if (LastVariance > 0.05f)
                sb.AppendLine("  ✅ 渲染管线正常，方块可见 —— 问题在关卡几何本身");
            else
                sb.AppendLine("  ❌ 连基础方块都渲染不出 —— 是渲染管线/相机设置问题");

            Destroy(testCube);
            Destroy(testMat);

            sb.AppendLine("=====================================");
            string text = sb.ToString();
            Debug.Log("<b>[全黑排查]</b>\n" + text);
            WriteFile(text);

            rt.Release(); Destroy(rt); Destroy(tex);
            if (!Application.isEditor) Application.Quit(0);
        }

        private float Measure(Camera cam, RenderTexture rt, Texture2D tex)
        {
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prev;

            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;

            var px = tex.GetPixels();
            double sum = 0;
            int dark = 0, mid = 0, bright = 0;
            for (int i = 0; i < px.Length; i++)
            {
                double l = 0.2126 * px[i].r + 0.7152 * px[i].g + 0.0722 * px[i].b;
                sum += l;
                if (l < 0.05) dark++;
                else if (l < 0.4) mid++;
                else bright++;
            }
            LastDarkPct = 100f * dark / px.Length;
            LastMidPct = 100f * mid / px.Length;
            LastBrightPct = 100f * bright / px.Length;

            // 方差：接近 0 说明整屏是纯色（没有几何体渲染）
            double mean = sum / px.Length;
            double varSum = 0;
            for (int i = 0; i < px.Length; i++)
            {
                double l = 0.2126 * px[i].r + 0.7152 * px[i].g + 0.0722 * px[i].b;
                double d = l - mean;
                varSum += d * d;
            }
            LastVariance = (float)System.Math.Sqrt(varSum / px.Length);

            try
            {
                string dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.persistentDataPath), "Shots");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "frame_" + Time.frameCount + ".png");
                File.WriteAllBytes(file, tex.EncodeToPNG());
                Debug.Log("[排查] 截图已保存: " + file);
            }
            catch (System.Exception e) { Debug.LogWarning("[排查] 截图失败: " + e.Message); }

            return (float)(sum / px.Length);
        }

        private static void WriteFile(string text)
        {
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "全黑排查报告.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}