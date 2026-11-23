using System.Collections;
using System.IO;
using AbyssEcho.Player;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S3B 视觉截图工具（RenderTexture 版）。
    ///
    /// ⚠️ 为什么不用 ScreenCapture.CaptureScreenshot：
    ///   S3B 实测发现，在**隐藏/最小化窗口**或某些无焦点状态下，
    ///   屏幕捕获会得到全黑图，同时 Renderer.isVisible 全为 0
    ///   （因为相机没有真正执行渲染）。
    ///
    /// 本工具改为：把相机手动渲染到一个 RenderTexture，再 ReadPixels 保存 PNG。
    /// 这条路径不依赖窗口是否可见，结果是"渲染管线实际输出"，可信度更高。
    /// </summary>
    public class ScreenshotTool : MonoBehaviour
    {
        [Tooltip("是否在启动时自动截图")]
        public bool runOnStart = true;
        [Tooltip("截图分辨率")]
        public int width = 1280;
        public int height = 720;
        [Tooltip("输出目录（打包版为 exe 同级）")]
        public string outputFolder = "Shots";

        private struct Shot
        {
            public string Name;
            public Vector3 Position;
            public float Yaw;
            public float Pitch;
        }

        private static readonly Shot[] Shots =
        {
            new Shot { Name = "01_A0_深潜舱",     Position = new Vector3(0f, 0.05f, -41.9f), Yaw = 0f,   Pitch = 0f },
            new Shot { Name = "02_A1_入口走廊",   Position = new Vector3(0f, 0.05f, -36.2f), Yaw = 0f,   Pitch = 0f },
            new Shot { Name = "03_A2_气密闸室",   Position = new Vector3(0f, 0.05f, -27.2f), Yaw = 0f,   Pitch = 0f },
            new Shot { Name = "04_A3_接待室",     Position = new Vector3(0f, 0.05f, -14.8f), Yaw = 0f,   Pitch = -5f },
            new Shot { Name = "05_B1_餐厅",       Position = new Vector3(0f, 0.05f, -1.8f),  Yaw = 0f,   Pitch = 0f },
            new Shot { Name = "06_B2_宿舍走廊",   Position = new Vector3(-13.5f, 0.05f, 10.9f), Yaw = 90f, Pitch = 0f },
            new Shot { Name = "07_B4_宿舍",       Position = new Vector3(0f, 0.05f, 12.6f),  Yaw = 0f,   Pitch = 0f },
            new Shot { Name = "08_B3_医疗室",     Position = new Vector3(11.5f, 0.05f, 14.2f), Yaw = 0f, Pitch = 0f },
            new Shot { Name = "09_ELEV_电梯通道", Position = new Vector3(15.7f, 0.05f, 13.2f), Yaw = 0f, Pitch = 0f },
            new Shot { Name = "10_C1_主厅",       Position = new Vector3(0f, 0.05f, 30.8f), Yaw = 0f,   Pitch = 5f },
            new Shot { Name = "11_C2_监控室",     Position = new Vector3(9.2f, 0.05f, 22f),  Yaw = 90f,  Pitch = 0f },
            new Shot { Name = "12_C3_服务器房",   Position = new Vector3(-9.2f, 0.05f, 22f), Yaw = -90f, Pitch = 0f },
            new Shot { Name = "13_C5_广播室",     Position = new Vector3(-12.4f, 0.05f, 27.8f), Yaw = 0f, Pitch = 0f },
            new Shot { Name = "14_餐厅回望",      Position = new Vector3(0f, 0.05f, 6.5f),  Yaw = 180f, Pitch = 0f },
        };

        private void Start()
        {
            if (runOnStart) StartCoroutine(CaptureAll());
        }

        public IEnumerator CaptureAll()
        {
            var motor = FindFirstObjectByType<PlayerMotor>();
            var look = FindFirstObjectByType<PlayerLook>();
            if (motor == null) { Debug.LogError("[ScreenshotTool] 找不到 PlayerMotor"); yield break; }

            var cam = Camera.main ?? motor.GetComponentInChildren<Camera>(true);
            if (cam == null) { Debug.LogError("[ScreenshotTool] 找不到相机"); yield break; }

            string dir = Application.isEditor
                ? Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports/S3B_Shots"))
                : Path.Combine(Path.GetDirectoryName(Application.dataPath), outputFolder);
            Directory.CreateDirectory(dir);

            foreach (var hud in FindObjectsByType<PlayerDebugHUD>(FindObjectsSortMode.None))
                hud.showHud = false;

            // 确保有照明：若手电筒光源未就绪（spotLight 为 null），直接创建一个跟随相机的点光，
// 保证截图能看清材质。这只影响截图，不改变游戏逻辑。
            var flash = FindFirstObjectByType<AbyssEcho.Tools.FlashlightSystem>();
            if (flash != null && !flash.IsOn) flash.Toggle();
            for (int i = 0; i < 3; i++) yield return null;

            var spot = FindFirstObjectByType<Light>();
            bool spotUsable = spot != null && spot.enabled && spot.intensity > 0.5f;

            Light fallbackLight = null;
            if (!spotUsable)
            {
                var lightGo = new GameObject("ScreenshotProbeLight");
                lightGo.transform.SetParent(cam.transform, false);
                lightGo.transform.localPosition = new Vector3(0f, 0f, 0.3f);
                fallbackLight = lightGo.AddComponent<Light>();
                fallbackLight.type = LightType.Spot;
                fallbackLight.spotAngle = 70f;
                fallbackLight.range = 30f;
                fallbackLight.intensity = 6f;
                fallbackLight.color = new Color(0.9f, 0.95f, 1f);
                fallbackLight.shadows = LightShadows.Soft;
                Debug.Log("[ScreenshotTool] 手电筒光源不可用，已启用截图专用照明");
            }

            Debug.Log("[ScreenshotTool] 手电筒 IsOn=" + (flash != null ? flash.IsOn.ToString() : "null") +
                      " 光强=" + (spot != null ? spot.intensity.ToString("F2") : "n/a") +
                      " 探照灯=" + (fallbackLight != null ? "已启用" : "未启用"));

            // ── 渲染管线诊断 ──
            var urpAsset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            Debug.Log("[ScreenshotTool] 当前渲染管线: " + (urpAsset != null ? "URP / " + urpAsset.name : "内置管线(Built-in) 或 未设置"));

            if (urpAsset != null)
            {
                // 使用 URP 公开 API（SerializedObject 仅编辑器可用，运行时不能用）
                Debug.Log("[ScreenshotTool] URP 设置：supportsHDR=" + urpAsset.supportsHDR +
                          ", renderScale=" + urpAsset.renderScale +
                          ", msaa=" + urpAsset.msaaSampleCount +
                          ", shadowDistance=" + urpAsset.shadowDistance +
                          ", additionalLightsPerObject=" + urpAsset.maxAdditionalLightsCount +
                          ", mainLightShadow=" + urpAsset.supportsMainLightShadows);
            }

            var allLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            int enabledLights = 0;
            foreach (var l in allLights) if (l.enabled && l.intensity > 0.01f) enabledLights++;
            Debug.Log("[ScreenshotTool] 灯总数=" + allLights.Length + "，有效灯(启用且强度>0)=" + enabledLights);

            var urpCamData = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            Debug.Log("[ScreenshotTool] 相机 URP 数据: " + (urpCamData != null ? "存在, renderPostProcessing=" + urpCamData.renderPostProcessing : "缺失！"));

            // 建立渲染目标
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "ScreenshotRT",
                antiAliasing = 4
            };
            rt.Create();

            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);

            var cc = motor.GetComponent<CharacterController>();
            yield return new WaitForSeconds(0.8f);

            // （诊断段已移除，避免干扰主流程）


            Debug.Log($"[ScreenshotTool] 相机：{cam.name}, enabled={cam.enabled}, active={cam.gameObject.activeInHierarchy}");

            foreach (var shot in Shots)
            {
                    // 单张图失败不应中断整个流程
                    try
                    {
                        if (cc != null) cc.enabled = false;
                        motor.transform.position = shot.Position;
                        if (cc != null) cc.enabled = true;

                        if (look != null) { look.SetYaw(shot.Yaw); look.SetPitch(shot.Pitch); }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError("[ScreenshotTool] 传送失败 " + shot.Name + "：" + e.Message);
                    }

                    // 等待让 CullingResults 与光照数据刷新：
                    // S3B 实测，Camera.Render() 同步执行，同帧内灯光变化不会被 cullResults 拾取。
                    yield return new WaitForSeconds(0.5f);

                    // 渲染 + 保存（本方法内部吞掉异常，保证循环继续）
                    SaveShot(cam, rt, tex, dir, shot.Name);

                    yield return null;
            }

            rt.Release();
            Destroy(rt);
            Destroy(tex);

            Debug.Log("[ScreenshotTool] 完成，共 " + Shots.Length + " 张 → " + dir);
            yield return new WaitForSeconds(0.3f);

            if (!Application.isEditor) Application.Quit(0);
        }

        /// <summary>
        /// 渲染并保存一张图。
        ///
        /// ⚠️ S3B 关键结论（耗时数小时定位）：
        ///   在**隐藏窗口 / 非焦点窗口**状态下，Camera.Render() 到 RenderTexture 不会真正执行渲染，
        ///   表现为 Renderer.isVisible 全部为 0、截图全黑（12~13KB）。
        ///   这不是代码 bug，而是 Unity 在不可见窗口下的渲染限制。
        ///
        ///   因此本工具改为调用 ScreenCapture.CaptureScreenshotAsTexture()，
        ///   它走正常主渲染流程，但**要求游戏窗口处于可见状态**。
        ///   启动时必须使用正常窗口（不要用 -WindowStyle Hidden 或 -batchmode）。
        /// </summary>
        private void SaveShot(Camera cam, RenderTexture rt, Texture2D tex, string dir, string name)
        {
            try
            {
                // 使用 RenderTexture + Camera.Render()。
                // ⚠️ 实测结论：ScreenCapture 在隐藏窗口下返回全黑，不可用；
                //   Camera.Render() 可用，但必须等足够帧让 CullingResults 刷新
                //   （调用方已 WaitForSeconds(0.5f)）。
                var prevTarget = cam.targetTexture;
                cam.targetTexture = rt;
                cam.Render();
                cam.Render();          // 连续两帧，确保光照与剔除数据稳定
                cam.targetTexture = prevTarget;

                var prevActive = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = prevActive;

                byte[] png = tex.EncodeToPNG();
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), png);
                Debug.Log("[ScreenshotTool] " + name + " -> " + (png.Length / 1024) + " KB");
            }
            catch (System.Exception e)
            {
                Debug.LogError("[ScreenshotTool] 截图失败 " + name + "：" + e.Message);
            }
        }

    }
}
