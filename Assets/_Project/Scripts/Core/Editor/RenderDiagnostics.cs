using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// 运行时诊断：在编辑器 Play 模式下打印渲染关键状态。
    ///
    /// 用途：当"游戏全黑"且自动截图不可靠时，用文本报告定位问题，
    /// 不依赖截图、不依赖窗口可见性。
    ///
    /// 使用方法：
    ///   1. 打开 Level_Chapter1_Graybox 场景
    ///   2. 菜单 ABYSS ECHO / 诊断：检查渲染状态
    ///   3. 查看 Console 输出的报告，或 Reports/诊断_渲染状态.txt
    /// </summary>
    public static class RenderDiagnostics
    {
        [MenuItem("ABYSS ECHO/诊断：检查渲染状态")]
        public static void Diagnose()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("ABYSS ECHO · 渲染状态诊断");
            sb.AppendLine("=====================================");

            // 1) 渲染管线
            var rp = GraphicsSettings.currentRenderPipeline;
            sb.AppendLine($"渲染管线：{(rp != null ? rp.GetType().Name + " / " + rp.name : "内置管线 (Built-in)")}");

            var urpAsset = rp as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            if (urpAsset != null)
            {
                sb.AppendLine($"  supportsHDR={urpAsset.supportsHDR}");
                sb.AppendLine($"  renderScale={urpAsset.renderScale}");
                sb.AppendLine($"  msaa={urpAsset.msaaSampleCount}");
                sb.AppendLine($"  maxAdditionalLightsCount={urpAsset.maxAdditionalLightsCount}");
                sb.AppendLine($"  supportsMainLightShadows={urpAsset.supportsMainLightShadows}");
                sb.AppendLine($"  shadowDistance={urpAsset.shadowDistance}");
            }
            else
            {
                sb.AppendLine("  ⚠️ 当前不是 URP！材质会显示为粉/黑");
            }

            // 2) 环境光
            sb.AppendLine();
            sb.AppendLine("环境光：见场景文件（RenderSettings 在批处理下不可访问）");
            
            
            

            // 3) 相机
            sb.AppendLine();
            var cam = Camera.main;
            if (cam == null)
            {
                sb.AppendLine("❌ 场景中没有 Camera.main（画面必然全黑）");
                var anyCam = Object.FindFirstObjectByType<Camera>();
                if (anyCam != null) sb.AppendLine($"   但存在相机：{anyCam.name}，Tag={anyCam.tag}");
            }
            else
            {
                sb.AppendLine($"相机：{cam.name}");
                sb.AppendLine($"  enabled={cam.enabled}, activeInHierarchy={cam.gameObject.activeInHierarchy}");
                sb.AppendLine($"  cullingMask=0x{cam.cullingMask:X}");
                sb.AppendLine($"  near={cam.nearClipPlane}, far={cam.farClipPlane}, fov={cam.fieldOfView}");
                sb.AppendLine($"  clearFlags={cam.clearFlags}, background={cam.backgroundColor}");
                sb.AppendLine($"  position={cam.transform.position}");

                var camData = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                sb.AppendLine($"  URP CameraData：{(camData != null ? "存在" : "缺失")}");
                if (camData != null)
                {
                    sb.AppendLine($"    renderPostProcessing={camData.renderPostProcessing}");
                    sb.AppendLine($"    antialiasing={camData.antialiasing}, antialiasingQuality={camData.antialiasingQuality}");
                }
            }

            // 4) Volume
            sb.AppendLine();
            var volumes = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
            sb.AppendLine($"Volume 数量：{volumes.Length}");
            foreach (var v in volumes)
            {
                sb.AppendLine($"  {v.name}: isGlobal={v.isGlobal}, priority={v.priority}, " +
                              $"weight={v.weight}, profile={(v.sharedProfile != null ? v.sharedProfile.name : "无")}, " +
                              $"enabled={v.enabled}");
            }

            // 5) 光源
            sb.AppendLine();
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            int active = 0;
            sb.AppendLine($"光源总数：{lights.Length}");
            foreach (var l in lights)
            {
                bool ok = l.enabled && l.gameObject.activeInHierarchy && l.intensity > 0.01f;
                if (ok) active++;
                if (lights.Length <= 15 || ok)
                    sb.AppendLine($"  {l.name}: type={l.type}, intensity={l.intensity:F2}, " +
                                  $"enabled={l.enabled}, active={l.gameObject.activeInHierarchy}, " +
                                  $"range={l.range}, pos={l.transform.position}");
            }
            sb.AppendLine($"有效光源（启用且强度>0.01）：{active}");

            // 6) 渲染器
            sb.AppendLine();
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            int rEnabled = 0, rVisible = 0, nullMat = 0;
            foreach (var r in renderers)
            {
                if (r.enabled) rEnabled++;
                if (r.isVisible) rVisible++;
                if (r.sharedMaterial == null) nullMat++;
            }
            sb.AppendLine($"渲染器总数：{renderers.Length}，enabled={rEnabled}，isVisible={rVisible}，空材质={nullMat}");
            sb.AppendLine("  （编辑器非播放状态下 isVisible 为 0 属正常）");

            // 7) 材质检查
            sb.AppendLine();
            sb.AppendLine("材质检查（前 5 个渲染器）：");
            int shown = 0;
            foreach (var r in renderers)
            {
                if (shown++ >= 5) break;
                var m = r.sharedMaterial;
                sb.AppendLine($"  {r.name}: {(m != null ? m.name + " shader=" + (m.shader != null ? m.shader.name : "null") : "无材质")}");
            }

            // 8) 深度雾
            sb.AppendLine();
            var fogCtl = Object.FindFirstObjectByType<AbyssEcho.Rendering.DepthFogController>();
            sb.AppendLine($"深度雾控制器：{(fogCtl != null ? "存在，材质=" + (fogCtl.fogMaterial != null ? fogCtl.fogMaterial.name : "空") : "缺失")}");

            sb.AppendLine("=====================================");

            string text = sb.ToString();
            Debug.Log("<b>[渲染诊断]</b>\n" + text);

            // 写文件
            try
            {
                string dir = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "../Reports"));
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "诊断_渲染状态.txt"),
                    text, new System.Text.UTF8Encoding(false));
            }
            catch { }
        }
    }
}