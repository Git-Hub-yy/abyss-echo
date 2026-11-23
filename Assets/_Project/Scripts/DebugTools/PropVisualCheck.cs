using System.Collections;
using System.IO;
using AbyssEcho.Player;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S6 道具视觉验证：把玩家送到各主要房间截图，确认道具布置效果。
    /// </summary>
    public class PropVisualCheck : MonoBehaviour
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
            if (cam == null) { Debug.LogError("[PropVisual] 无相机"); yield break; }

            var motor = FindFirstObjectByType<PlayerMotor>();
            var look = FindFirstObjectByType<PlayerLook>();
            var cc = motor != null ? motor.GetComponent<CharacterController>() : null;

            string dir = Path.Combine(Application.persistentDataPath, "Shots");
            Directory.CreateDirectory(dir);

            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            rt.Create();
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);

            foreach (var hud in FindObjectsByType<PlayerDebugHUD>(FindObjectsSortMode.None))
                hud.showHud = false;

            // 各房间观察点
            var shots = new (string name, Vector3 pos, float yaw, float pitch)[]
            {
                // A3 接待室：从南侧看沙发茶几
                ("S6_01_A3接待室",  new Vector3(0f, 0.05f, -14.5f),   0f,   -5f),
                // B1 餐厅：从南端看餐桌区
                ("S6_02_B1餐厅",    new Vector3(0f, 0.05f, -1.5f),    0f,   0f),
                // B2 走廊：看储物柜
                ("S6_03_B2走廊",    new Vector3(-13f, 0.05f, 10.9f),  90f,  0f),
                // B3 医疗室
                ("S6_04_B3医疗室",  new Vector3(11.5f, 0.05f, 13.0f), 0f,   -3f),
                // C1 主厅
                ("S6_05_C1主厅",    new Vector3(0f, 0.05f, 27f),      0f,   3f),
                // C2 监控室
                ("S6_06_C2监控室",  new Vector3(9f, 0.05f, 35f),      90f,  0f),
                // C3 服务器机房
                ("S6_07_C3服务器",  new Vector3(-6.5f, 0.05f, 35f),   -90f,  0f),
            };

            yield return new WaitForSeconds(0.8f);

            foreach (var s in shots)
            {
                if (cc != null) cc.enabled = false;
                if (motor != null) motor.transform.position = s.pos;
                if (cc != null) cc.enabled = true;
                if (look != null) { look.SetYaw(s.yaw); look.SetPitch(s.pitch); }

                yield return new WaitForSeconds(0.5f);

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
                File.WriteAllBytes(Path.Combine(dir, s.name + ".png"), png);
                Debug.Log("[PropVisual] " + s.name + " -> " + (png.Length / 1024) + " KB");
            }

            rt.Release(); Destroy(rt); Destroy(tex);
            Debug.Log("[PropVisual] 完成");
            yield return new WaitForSeconds(0.3f);
            if (!Application.isEditor) Application.Quit(0);
        }
    }
}