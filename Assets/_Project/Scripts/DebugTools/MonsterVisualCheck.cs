using System.Collections;
using System.IO;
using AbyssEcho.Monster;
using AbyssEcho.Player;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S5 怪物视觉验证：把玩家送到怪物附近截图，确认剪影效果。
    /// </summary>
    public class MonsterVisualCheck : MonoBehaviour
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
            if (cam == null) { Debug.LogError("[MonsterVisual] 无相机"); yield break; }

            var motor = FindFirstObjectByType<PlayerMotor>();
            var look = FindFirstObjectByType<PlayerLook>();
            var cc = motor != null ? motor.GetComponent<CharacterController>() : null;

            var monster = FindFirstObjectByType<MonsterAI>();
            if (monster == null) { Debug.LogError("[MonsterVisual] 找不到怪物"); yield break; }

            // 让怪物停在原地（休眠），方便截图
            monster.Deactivate();

            Vector3 mPos = monster.transform.position;
            Debug.Log($"[MonsterVisual] 怪物位置: {mPos}");

            string dir = Path.Combine(Application.persistentDataPath, "Shots");
            Directory.CreateDirectory(dir);

            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            rt.Create();
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);

            foreach (var hud in FindObjectsByType<PlayerDebugHUD>(FindObjectsSortMode.None))
                hud.showHud = false;

            // ① 远处观察（6 米）
            yield return ShotAt(cam, rt, tex, dir, "S5M_01_怪物远景",
                motor, look, cc, mPos + new Vector3(6f, 0.05f, 0f), -90f, 0f);

            // ② 中等距离（3 米）
            yield return ShotAt(cam, rt, tex, dir, "S5M_02_怪物中景",
                motor, look, cc, mPos + new Vector3(3f, 0.05f, 0f), -90f, -5f);

            // ③ 近景（2 米）
            yield return ShotAt(cam, rt, tex, dir, "S5M_03_怪物近景",
                motor, look, cc, mPos + new Vector3(2.2f, 0.05f, 0f), -90f, -8f);

            rt.Release(); Destroy(rt); Destroy(tex);
            Debug.Log("[MonsterVisual] 完成");
            yield return new WaitForSeconds(0.3f);
            if (!Application.isEditor) Application.Quit(0);
        }

        private IEnumerator ShotAt(Camera cam, RenderTexture rt, Texture2D tex, string dir, string name,
                                   PlayerMotor motor, PlayerLook look, CharacterController cc,
                                   Vector3 playerPos, float yaw, float pitch)
        {
            if (cc != null) cc.enabled = false;
            if (motor != null) motor.transform.position = playerPos;
            if (cc != null) cc.enabled = true;
            if (look != null) { look.SetYaw(yaw); look.SetPitch(pitch); }

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
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), png);
            Debug.Log("[MonsterVisual] " + name + " -> " + (png.Length / 1024) + " KB");
        }
    }
}