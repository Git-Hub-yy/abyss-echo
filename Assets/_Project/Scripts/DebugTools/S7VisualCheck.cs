using System.Collections;
using System.IO;
using AbyssEcho.Inventory;
using AbyssEcho.UI;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>S7 UI 视觉验证：截图物品栏与暂停菜单。</summary>
    public class S7VisualCheck : MonoBehaviour
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
            if (cam == null) { Debug.LogError("[S7Visual] 无相机"); yield break; }

            string dir = Path.Combine(Application.persistentDataPath, "Shots");
            Directory.CreateDirectory(dir);

            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            rt.Create();
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);

            foreach (var hud in FindObjectsByType<PlayerDebugHUD>(FindObjectsSortMode.None))
                hud.showHud = false;

            yield return new WaitForSeconds(0.6f);

            // 先给背包塞点东西，才能看到物品栏内容
            var inv = PlayerInventory.Instance;
            if (inv != null)
            {
                inv.Clear();
                inv.Add(ItemIds.Fuse, 1);
                inv.Add(ItemIds.Note, 2);
                Debug.Log("[S7Visual] 背包已注入测试物品");
            }

            // ① 物品栏
            var invUI = FindFirstObjectByType<InventoryUI>();
            if (invUI != null)
            {
                invUI.Open();
                yield return new WaitForSeconds(0.4f);
                Capture(cam, rt, tex, dir, "S7_01_物品栏");
                invUI.Close();
                yield return new WaitForSeconds(0.2f);
            }

            // ② 暂停菜单
            var pause = FindFirstObjectByType<PauseMenuUI>();
            if (pause != null)
            {
                pause.Open();
                yield return new WaitForSeconds(0.4f);
                Capture(cam, rt, tex, dir, "S7_02_暂停菜单");
                pause.Close();
                yield return new WaitForSeconds(0.2f);
            }

            // 清理测试物品
            if (inv != null) inv.Clear();

            rt.Release(); Destroy(rt); Destroy(tex);
            Debug.Log("[S7Visual] 完成");
            yield return new WaitForSeconds(0.3f);
            if (!Application.isEditor) Application.Quit(0);
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
            Debug.Log("[S7Visual] " + name + " -> " + (png.Length / 1024) + " KB");
        }
    }
}