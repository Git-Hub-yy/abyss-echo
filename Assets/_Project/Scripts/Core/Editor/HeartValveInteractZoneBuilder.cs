using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S54：修复心瓣无法被交互检测命中。
    ///
    /// ══════════════════ 根因（实测）══════════════════
    ///
    ///   InteractionDetector 从相机中心发射线，距离 2.6 米。
    ///   玩家站立眼高 1.67 米，心瓣是**地面上的一层薄板**（高 0.12~0.24 米）。
    ///
    ///   实测：玩家站 z=13.8，心瓣 z=16 → 水平距离 2.2 米
    ///         要从眼高 1.67 看到地面 0.12 处，需要**低头 35°**
    ///         而玩家正常看前方，准星根本扫不到心瓣
    ///   → InteractionDetector.HasTarget = False，按 E 无反应
    ///
    /// ══════════════════ 修复方案（最小改动）══════════════════
    ///
    ///   给 HeartValve 增加一个**竖直的交互碰撞体**：
    ///     · 一个独立的子物体 "HeartValve_InteractZone"
    ///     · 尺寸 2.2 × 2.4 × 2.2，从地面向上，覆盖玩家平视高度
    ///     · isTrigger = true（不挡路）
    ///     · 它会被 GetComponentInParent<IInteractable>() 回溯到 HeartValve
    ///
    ///   这样玩家站在心瓣前平视，准星扫到这根"无形的感应柱"即可交互。
    ///   心瓣本体（薄板）和谜题逻辑完全不变。
    /// </summary>
    public static class HeartValveInteractZoneBuilder
    {
        [MenuItem("ABYSS ECHO/修复 v6.0/心瓣交互感应区")]
        public static void Build() => Run();

        public static void Run()
        {
            const string path = "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity";
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Log($"已打开 {path}");

            var valve = GameObject.Find("HeartValve");
            if (valve == null) { Fail("找不到 HeartValve"); return; }

            Log($"  心瓣位置 = {valve.transform.position}");
            Log($"  心瓣缩放 = {valve.transform.lossyScale}");

            // ── 检查是否已存在 ──
            Transform existing = valve.transform.Find("HeartValve_InteractZone");
            if (existing != null)
            {
                Log("  交互感应区已存在，跳过创建");
            }
            else
            {
                var zoneGo = new GameObject("HeartValve_InteractZone");
                zoneGo.transform.SetParent(valve.transform, false);

                // 子物体的本地缩放会继承父级的 (2.2, 0.24, 2.2)
                // 为了让世界尺寸是 2.2×2.4×2.2，需要把本地缩放除以父级
                Vector3 parentScale = valve.transform.lossyScale;
                zoneGo.transform.localScale = new Vector3(
                    2.2f / Mathf.Max(0.001f, parentScale.x),
                    2.4f / Mathf.Max(0.001f, parentScale.y),
                    2.2f / Mathf.Max(0.001f, parentScale.z));

                // 位置：在父物体中心基础上，向上偏移让感应柱覆盖地面到 2.4 米高
                // 父物体中心在世界 y=0.12，感应柱中心应在其上方 1.2 米 - 0.12 米
                zoneGo.transform.localPosition = new Vector3(0f, 1.2f / Mathf.Max(0.001f, parentScale.y), 0f);

                var bc = zoneGo.AddComponent<BoxCollider>();
                bc.isTrigger = true;
                bc.size = Vector3.one;

                Physics.SyncTransforms();

                Log($"  + 已创建 HeartValve_InteractZone");
                Log($"    世界位置 = {zoneGo.transform.position}");
                Log($"    世界尺寸 = {Vector3.Scale(bc.size, zoneGo.transform.lossyScale)}");
                Log($"    isTrigger = {bc.isTrigger}");
            }

            // ── 保存 ──
            EditorSceneManager.MarkSceneDirty(scene);
            bool ok = EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log(ok ? "场景已保存" : "❌ 保存失败");

            // ── 验证：模拟玩家平视射线 ──
            Log("---- 验证 ----");
            Physics.SyncTransforms();

            var zone = valve.transform.Find("HeartValve_InteractZone");
            if (zone != null)
            {
                var zc = zone.GetComponent<Collider>();
                Log($"  感应区包围盒 center={zc.bounds.center} size={zc.bounds.size}");
            }

            // 玩家站在心瓣南侧 2.2 米处平视（略微低头 10 度）
            Vector3 eye = new Vector3(0f, 1.67f, 13.8f);
            Vector3 dirFlat = new Vector3(0f, 0f, 1f);           // 完全平视
            Vector3 dirDown10 = Quaternion.Euler(10f, 0f, 0f) * Vector3.forward;

            foreach (var (d, label) in new[] { (dirFlat, "平视"), (dirDown10, "低头10°") })
            {
                bool hit = Physics.SphereCast(eye, 0.12f, d, out var h, 2.6f,
                                              ~0, QueryTriggerInteraction.Collide);
                if (hit)
                {
                    var inter = h.collider.GetComponentInParent<AbyssEcho.Interaction.IInteractable>();
                    Log($"  {label}: 命中 '{h.collider.gameObject.name}'  " +
                        $"可交互物={(inter != null ? "✅ " + inter.GetType().Name : "❌ 无")}");
                }
                else
                {
                    Log($"  {label}: 未命中任何物体");
                }
            }

            // 更远处
            Vector3 eye2 = new Vector3(0f, 1.67f, 14.5f);
            bool hit2 = Physics.SphereCast(eye2, 0.12f, dirDown10, out var h2, 2.6f,
                                           ~0, QueryTriggerInteraction.Collide);
            if (hit2)
            {
                var inter2 = h2.collider.GetComponentInParent<AbyssEcho.Interaction.IInteractable>();
                Log($"  站 z=14.5 低头10°: 命中 '{h2.collider.gameObject.name}'  " +
                    $"可交互物={(inter2 != null ? "✅ " + inter2.GetType().Name : "❌ 无")}");
            }
            else Log($"  站 z=14.5 低头10°: 未命中");

            Log("完成");
        }

        private static void Log(string msg)
        {
            string line = $"[ValveZone] {msg}";
            Debug.Log(line);
            Write(line);
        }

        private static void Fail(string msg)
        {
            string line = $"[ValveZone] ❌ {msg}";
            Debug.LogError(line);
            Write(line);
        }

        private static void Write(string line)
        {
            try
            {
                string dir = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "HeartValveZone_Result.txt"),
                                   line + "\n", new System.Text.UTF8Encoding(false));
            }
            catch { }
        }
    }
}