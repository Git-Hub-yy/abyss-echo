using System.Collections;
using System.IO;
using AbyssEcho.Narrative;
using AbyssEcho.Player;
using AbyssEcho.UI;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S8 视觉验证：截取开场标题卡与结局画面。
    ///
    /// ⚠️ 重要：所有 UI 都用 OnGUI 绘制，而 Camera.Render() 不包含 OnGUI 层。
    ///    所以本工具额外把【屏幕完整内容】通过 ScreenCapture 拍下来——
    ///    但隐藏窗口下 ScreenCapture 也不可靠（S3B 已确认）。
    ///
    ///    折中方案：截图用于确认 3D 场景（黑幕部分），
    ///    文字内容通过日志输出，由人工在 Unity 中确认。
    /// </summary>
    public class S8VisualCheck : MonoBehaviour
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
            if (cam == null) { Debug.LogError("[S8Visual] 无相机"); yield break; }

            string dir = Path.Combine(Application.persistentDataPath, "Shots");
            Directory.CreateDirectory(dir);

            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            rt.Create();
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);

            foreach (var hud in FindObjectsByType<PlayerDebugHUD>(FindObjectsSortMode.None))
                hud.showHud = false;

            var card = FindFirstObjectByType<ChapterTitleCard>();
            var ending = FindFirstObjectByType<ChapterEndingSystem>();
            var motor = FindFirstObjectByType<PlayerMotor>();

            // ── ① 标题卡：等它进入标题显示段（约 2 秒）──
            if (card != null)
            {
                yield return new WaitForSeconds(2.2f);
                Capture(cam, rt, tex, dir, "S8_01_标题卡");
                Debug.Log("[S8Visual] 标题卡文本：" + card.chapterNumber + " · " + card.chapterName +
                          " / " + card.subtitle);
                WaitForCard(card);
            }

            // ── ② 结局：把玩家传到终点 ──
            if (ending != null && motor != null)
            {
                var cc = motor.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                motor.transform.position = ending.triggerCenter;
                if (cc != null) cc.enabled = true;

                // 等到渐黑完成 + 开始出字
                yield return new WaitForSeconds(4.0f);
                Capture(cam, rt, tex, dir, "S8_02_结局黑幕");

                Debug.Log("[S8Visual] 结局文案：");
                foreach (var line in ending.endingLines)
                    Debug.Log("    · " + line);
                Debug.Log("[S8Visual] 标题：" + ending.chapterTitle + " / " + ending.chapterFooter);
            }

            rt.Release(); Destroy(rt); Destroy(tex);
            Debug.Log("[S8Visual] 完成");
            yield return new WaitForSeconds(0.3f);
            if (!Application.isEditor) Application.Quit(0);
        }

        /// <summary>等待标题卡播放结束（最多 8 秒）。</summary>
        private IEnumerator WaitForCard(ChapterTitleCard card)
        {
            float t = 0f;
            while (t < 8f)
            {
                t += Time.deltaTime;
                if (!card.isActiveAndEnabled) break;
                yield return null;
            }
        }

        private void Capture(Camera cam, RenderTexture rt, Texture2D tex, string dir, string name)
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
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), png);
            Debug.Log("[S8Visual] " + name + " -> " + (png.Length / 1024) + " KB");
        }
    }
}