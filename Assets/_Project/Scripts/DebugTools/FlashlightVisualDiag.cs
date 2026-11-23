using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S52 手电筒「为什么看不见光」运行时追踪。
    ///
    /// ══════════════════ 设计原则 ══════════════════
    ///
    ///   上一轮只测了 Light.enabled 和 intensity，这不够。
    ///   本轮要追踪**真实渲染链路**的每一环：
    ///
    ///     A. F 键是否触发 Toggle
    ///     B. 控制的是不是玩家真实使用的那盏灯
    ///     C. Light 的位置/朝向/父物体是否正确
    ///     D. 渲染管线是否允许这盏灯参与照明  ← 重点
    ///     E. 后处理是否压掉了照明
    ///
    /// ══════════════════ 关键检查项 ══════════════════
    ///
    ///   URP_Pipeline.asset 的 m_AdditionalLightsRenderingMode = 0（Disabled）
    ///   → Spot Light 属于「附加灯光（Additional Light）」
    ///   → 在 Disabled 模式下**完全不参与渲染**
    ///   → 无论 enabled/intensity 怎么设，画面都不会变亮
    ///
    ///   这就是"状态正确但看不见光"的核心原因。
    /// </summary>
    public class FlashlightVisualDiag : MonoBehaviour
    {
        public bool runOnStart = true;
        public int forceChapter = 3;

        private readonly StringBuilder _log = new StringBuilder();
        private static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            if (forceChapter >= 1 && forceChapter <= 5)
                AbyssEcho.LevelGen.ChapterLayout.Current =
                    (AbyssEcho.LevelGen.ChapterLayout.Chapter)forceChapter;

            yield return new WaitForSeconds(1.2f);

            _log.AppendLine("══════ S52 手电筒渲染链路追踪 ══════");
            _log.AppendLine($"场景 = {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            _log.AppendLine($"是否 BatchMode = {Application.isBatchMode}");
            _log.AppendLine($"图形设备 = {SystemInfo.graphicsDeviceType}");
            _log.AppendLine();

            // ══ 第 0 步：渲染管线配置 ══
            _log.AppendLine("──── [0] 渲染管线配置 ────");
            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset == null)
            {
                _log.AppendLine("  ⚠️ 当前不是 URP 管线");
            }
            else
            {
                _log.AppendLine($"  URP Asset = {urpAsset.name}");
                _log.AppendLine($"  supportsHDR = {urpAsset.supportsHDR}");
                _log.AppendLine($"  msaaSampleCount = {urpAsset.msaaSampleCount}");
                _log.AppendLine($"  shadowDistance = {urpAsset.shadowDistance}");

                // 附加灯光渲染模式：0=Disabled, 1=PerVertex, 2=PerPixel
                var addModeProp = typeof(UniversalRenderPipelineAsset)
                    .GetProperty("additionalLightsRenderingMode", BF);
                if (addModeProp != null)
                {
                    var mode = addModeProp.GetValue(urpAsset);
                    _log.AppendLine($"  ★ additionalLightsRenderingMode = {mode}（0=Disabled 1=PerVertex 2=PerPixel）");
                    bool disabled = mode.ToString() == "Disabled" || mode.ToString() == "0";
                    _log.AppendLine(disabled
                        ? "  ❌❌ 附加灯光被禁用！Spot Light 不会参与任何照明渲染！"
                        : "  ✅ 附加灯光已启用");
                }

                var perObjProp = typeof(UniversalRenderPipelineAsset)
                    .GetProperty("maxAdditionalLightsCount", BF);
                if (perObjProp != null)
                    _log.AppendLine($"  maxAdditionalLightsCount = {perObjProp.GetValue(urpAsset)}");
            }

            // Renderer 的 renderingMode
            var cam0 = Camera.main;
            if (cam0 != null)
            {
                var camData = cam0.GetUniversalAdditionalCameraData();
                if (camData != null)
                    _log.AppendLine($"  相机 renderType = {camData.renderType}  0=Base 1=Overlay");
            }
            _log.AppendLine();

            // ══ 第 1 步：找到玩家真实的 FlashlightSystem ══
            _log.AppendLine("──── [1] FlashlightSystem 实例 ────");
            var fls = FindObjectsByType<AbyssEcho.Tools.FlashlightSystem>(FindObjectsSortMode.None);
            _log.AppendLine($"  场景中 FlashlightSystem 数量 = {fls.Length}");
            if (fls.Length == 0) { Dump(); yield break; }

            foreach (var f in fls)
                _log.AppendLine($"    · {GetPath(f.transform)}  active={f.gameObject.activeInHierarchy} enabled={f.enabled}");

            var fl = fls[0];
            _log.AppendLine();

            // ══ 第 2 步：Light 实例完整追踪 ══
            _log.AppendLine("──── [2] Light 实例追踪 ────");
            var flType = typeof(AbyssEcho.Tools.FlashlightSystem);
            var spotField = flType.GetField("spotLight", BF);
            var handledSpot = spotField?.GetValue(fl) as Light;

            _log.AppendLine($"  FlashlightSystem.spotLight 引用 = {(handledSpot != null ? GetPath(handledSpot.transform) : "❌ NULL")}");

            // 列出玩家下所有 Light
            var playerLights = fl.GetComponentsInChildren<Light>(true);
            _log.AppendLine($"  玩家层级下 Light 数量 = {playerLights.Length}");
            foreach (var l in playerLights)
            {
                _log.AppendLine($"    · {GetPath(l.transform)}");
                _log.AppendLine($"        type={l.type} enabled={l.enabled} intensity={l.intensity:F2} " +
                                $"range={l.range:F1} spot={l.spotAngle:F1} color={l.color} " +
                                $"cullingMask={l.cullingMask} activeInHierarchy={l.gameObject.activeInHierarchy}");
            }

            if (handledSpot == null) { Dump(); yield break; }

            // ══ 第 3 步：Transform 与相机对齐 ══
            _log.AppendLine();
            _log.AppendLine("──── [3] Transform 与朝向 ────");
            var cam = Camera.main;
            Vector3 lp = handledSpot.transform.position;
            Vector3 lf = handledSpot.transform.forward;
            _log.AppendLine($"  Light 世界坐标 = {lp}");
            _log.AppendLine($"  Light 世界前向 = {lf}");
            if (cam != null)
            {
                _log.AppendLine($"  相机世界坐标 = {cam.transform.position}");
                _log.AppendLine($"  相机世界前向 = {cam.transform.forward}");
                float dist = Vector3.Distance(lp, cam.transform.position);
                float dot = Vector3.Dot(lf.normalized, cam.transform.forward.normalized);
                _log.AppendLine($"  Light↔相机 距离 = {dist:F3} 米");
                _log.AppendLine($"  Light 朝向·相机朝向 点积 = {dot:F3}（1.0=完全一致）");
            }

            // 父物体链
            _log.AppendLine("  父物体链：");
            var t = handledSpot.transform;
            int depth = 0;
            while (t != null && depth++ < 12)
            {
                _log.AppendLine($"    [{depth}] {t.name}  active={t.gameObject.activeSelf} " +
                                $"localScale={t.localScale} localPos={t.localPosition}");
                t = t.parent;
            }

            // ══ 第 4 步：是否被其他脚本覆盖 ══
            _log.AppendLine();
            _log.AppendLine("──── [4] 是否有其他脚本控制这盏灯 ────");
            var allMbs = handledSpot.GetComponentsInParent<MonoBehaviour>();
            foreach (var mb in allMbs)
            {
                if (mb == null) continue;
                _log.AppendLine($"    父级脚本: {mb.GetType().FullName} @ {mb.gameObject.name}");
            }
            var selfMbs = handledSpot.GetComponents<MonoBehaviour>();
            foreach (var mb in selfMbs)
            {
                if (mb == null) continue;
                _log.AppendLine($"    灯上脚本: {mb.GetType().FullName}");
            }

            // ══ 第 5 步：F 键前后状态对比 ══
            _log.AppendLine();
            _log.AppendLine("──── [5] 开启前后的完整状态对比 ────");

            // 先关
            flType.GetMethod("SetLightOn", BF)?.Invoke(fl, new object[] { false });
            yield return new WaitForSeconds(0.3f);

            _log.AppendLine("  【关闭状态】");
            DumpLight(handledSpot, "    ");

            // 开
            flType.GetMethod("SetLightOn", BF)?.Invoke(fl, new object[] { true });
            yield return new WaitForSeconds(0.3f);

            _log.AppendLine("  【开启状态】");
            DumpLight(handledSpot, "    ");

            // ══ 第 6 步：采样屏幕亮度（如果可渲染）══
            _log.AppendLine();
            _log.AppendLine("──── [6] 屏幕像素采样 ────");
            yield return SampleScreen(cam, "开启中");

            flType.GetMethod("SetLightOn", BF)?.Invoke(fl, new object[] { false });
            yield return new WaitForSeconds(0.4f);
            yield return SampleScreen(cam, "关闭后");

            _log.AppendLine();
            _log.AppendLine("──── 结论 ────");
            if (urpAsset != null)
            {
                var addModeProp = typeof(UniversalRenderPipelineAsset)
                    .GetProperty("additionalLightsRenderingMode", BF);
                var mode = addModeProp?.GetValue(urpAsset);
                bool disabled = mode != null && (mode.ToString() == "Disabled" || mode.ToString() == "0");
                _log.AppendLine(disabled
                    ? "  ❌ 附加灯光渲染模式 = Disabled → 手电筒（Spot Light）永远不会照亮任何物体"
                    : "  ✅ 附加灯光已启用，手电筒应能参与照明");
            }

            Dump();
        }

        private void DumpLight(Light l, string indent)
        {
            _log.AppendLine($"{indent}enabled={l.enabled}");
            _log.AppendLine($"{indent}intensity={l.intensity:F3}");
            _log.AppendLine($"{indent}range={l.range:F2}");
            _log.AppendLine($"{indent}spotAngle={l.spotAngle:F2}");
            _log.AppendLine($"{indent}innerSpotAngle={l.innerSpotAngle:F2}");
            _log.AppendLine($"{indent}color={l.color} (亮度={l.color.grayscale:F3})");
            _log.AppendLine($"{indent}cullingMask={l.cullingMask}");
            _log.AppendLine($"{indent}shadows={l.shadows}");
            _log.AppendLine($"{indent}renderMode={l.renderMode}");
            _log.AppendLine($"{indent}activeInHierarchy={l.gameObject.activeInHierarchy}");
            _log.AppendLine($"{indent}position={l.transform.position}");
            _log.AppendLine($"{indent}forward={l.transform.forward}");
        }

        private IEnumerator SampleScreen(Camera cam, string label)
        {
            if (cam == null) { _log.AppendLine($"    （无相机，跳过 {label}）"); yield break; }

            yield return new WaitForEndOfFrame();

            int w = 160, h = 90;
            RenderTexture rt = null;
            Texture2D tex = null;
            try
            {
                rt = RenderTexture.GetTemporary(w, h, 24);
                var prev = cam.targetTexture;
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = prev;

                RenderTexture.active = rt;
                tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();

                float sum = 0f;
                float max = 0f;
                Color[] px = tex.GetPixels();
                foreach (var p in px)
                {
                    float lum = 0.299f * p.r + 0.587f * p.g + 0.114f * p.b;
                    sum += lum;
                    if (lum > max) max = lum;
                }
                float avg = sum / px.Length;

                _log.AppendLine($"    [{label}] 平均亮度={avg:F4}  最大亮度={max:F4}  " +
                                $"画面尺寸={w}x{h}");

                // 中心区域亮度（手电筒应该照在中间）
                float cSum = 0f; int cN = 0;
                for (int y = h / 3; y < h * 2 / 3; y++)
                    for (int x = w / 3; x < w * 2 / 3; x++)
                    {
                        var p = tex.GetPixel(x, y);
                        cSum += 0.299f * p.r + 0.587f * p.g + 0.114f * p.b;
                        cN++;
                    }
                _log.AppendLine($"       中心 1/3 区域平均亮度 = {cSum / Mathf.Max(1, cN):F4}");
            }
            catch (System.Exception e)
            {
                _log.AppendLine($"    [{label}] 渲染采样失败: {e.GetType().Name}: {e.Message}");
            }
            finally
            {
                if (tex != null) Destroy(tex);
                if (rt != null) { RenderTexture.active = null; RenderTexture.ReleaseTemporary(rt); }
            }
        }

        private string GetPath(Transform t)
        {
            string p = t.name;
            var cur = t.parent; int g = 0;
            while (cur != null && g++ < 20) { p = cur.name + "/" + p; cur = cur.parent; }
            return p;
        }

        private void Dump()
        {
            string text = _log.ToString();
            Debug.Log("<b>[FlashlightVisualDiag]</b>\n" + text);

            var di = new DirectoryInfo(Application.dataPath);
            for (int hop = 0; hop < 6 && di != null; hop++)
            {
                string reports = Path.Combine(di.FullName, "Reports");
                if (Directory.Exists(reports))
                {
                    try
                    {
                        string fn = $"FlashlightVisualDiag_Ch{(int)AbyssEcho.LevelGen.ChapterLayout.Current}.txt";
                        File.WriteAllText(Path.Combine(reports, fn), text, new UTF8Encoding(false));
                        break;
                    }
                    catch { }
                }
                di = di.Parent;
            }

            if (!Application.isEditor) Application.Quit(0);
        }
    }
}