using System.Collections;
using System.IO;
using AbyssEcho.Player;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S4 交互物视觉验证：把玩家送到每个交互物前截图，
    /// 确认物体在场景中可见、准星 UI 正常显示。
    ///
    /// 输出：%USERPROFILE%/AppData/LocalLow/AbyssEchoStudio/ABYSS ECHO/Shots/
    /// </summary>
    public class S4VisualCheck : MonoBehaviour
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
            if (cam == null) { Debug.LogError("[S4Visual] 无相机"); yield break; }

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

            yield return new WaitForSeconds(1.0f);

            // 交互物实际坐标（来自 InteractablePlacementBuilder）：
            //   保险丝   Pickup_Fuse    (2.2, 0.95, -13.0)
            //   配电箱   PowerPanel_A3  (-2.6, 1.35, -9.25)
            //   门       Door_A3_B      (0, 1.15, -2.15)
            // 相机高度 = 玩家 y(0.05) + 1.65 = 1.70
            // 朝向：yaw=0 表示朝 +Z（北）
            var targets = new (string name, Vector3 pos, float yaw, float pitch)[]
            {
                // 站在保险丝南侧 1.4m，朝北看
                ("S4_01_保险丝", new Vector3(2.2f, 0.05f, -14.4f), 0f, -12f),
                // 站在配电箱前 1.6m，朝北看（配电箱贴着 A3 北墙 z=-9.25）
                ("S4_02_配电箱", new Vector3(-2.6f, 0.05f, -10.8f), 0f, 0f),
                // 站在门前 2.2m，朝北看
                ("S4_03_门", new Vector3(0f, 0.05f, -4.4f), 0f, 0f),
                // A3 房间全景（从南端往北看）
                ("S4_04_A3全景", new Vector3(0f, 0.05f, -14.0f), 0f, 0f),
                // C5 广播室：线索纸条（C5 中心约 (-12.4, 0, 42)）
                ("S4_05_线索纸条", new Vector3(-11.5f, 0.05f, 40.2f), 0f, -8f),
                // C2 监控室：门禁键盘（C2 中心约 (12.4, 0, 35)）
                ("S4_06_门禁键盘", new Vector3(10.0f, 0.05f, 35.0f), -90f, 0f),
            };

            foreach (var t in targets)
            {
                if (cc != null) cc.enabled = false;
                if (motor != null) motor.transform.position = t.pos;
                if (cc != null) cc.enabled = true;
                if (look != null) { look.SetYaw(t.yaw); look.SetPitch(t.pitch); }

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
                File.WriteAllBytes(Path.Combine(dir, t.name + ".png"), png);
                Debug.Log("[S4Visual] " + t.name + " -> " + (png.Length / 1024) + " KB");
            }

            rt.Release(); Destroy(rt); Destroy(tex);
            Debug.Log("[S4Visual] 完成");
            yield return new WaitForSeconds(0.3f);
            if (!Application.isEditor) Application.Quit(0);
        }
    }
}