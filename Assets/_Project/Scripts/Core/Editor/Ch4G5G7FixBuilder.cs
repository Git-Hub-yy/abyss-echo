using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S54：修复第四章 G5 → G7 通路与心瓣可见性。
    ///
    /// ══════════════════ 根因（实测确认）══════════════════
    ///
    ///   诊断数据：
    ///     核心  (肉块)  @ (0, 2.20, 16)  尺寸 4×4.4×4
    ///                   世界范围 x[-2,2] y[0,4.4] z[14,18]
    ///     心瓣 HeartValve @ (0, 0.12, 16)  尺寸 2.2×0.24×2.2
    ///     → 心瓣**完全位于核心包围盒内部**
    ///
    ///   后果：
    ///     · 玩家进入 C7 看到的是「核心」这块 4 米肉块
    ///     · 心瓣藏在肉块正下方，肉眼不可见
    ///     · 走到肉块前，射线打到的是肉块，交互检测命不中心瓣
    ///     · 所以按 E 毫无反应，谜题永远无法开始
    ///
    /// ══════════════════ 修复方案（最小改动）══════════════════
    ///
    ///   把「核心」肉块**向后（北）移开 5 米**，让心瓣在正面露出来。
    ///     · 不改心瓣位置（谜题逻辑依赖其坐标）
    ///     · 不改任何房间几何
    ///     · 不改碰撞体设置
    ///     · 核心肉块仍是房间视觉焦点（移到房间后半部）
    ///
    ///   核心从 z=16 移到 z=21（G7 范围 z[7,25]，仍在室内）。
    ///   心瓣 z=16 前方留出 3 米空地，玩家可以走到跟前按 E。
    /// </summary>
    public static class Ch4G5G7FixBuilder
    {
        [MenuItem("ABYSS ECHO/修复 v6.0/第四章 G5→G7 通路与心瓣")]
        public static void Build() => Run();

        public static void Run()
        {
            const string path = "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity";

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Log($"已打开 {path}");

            // ══ 1. 修复「核心」遮挡 ══
            var core = GameObject.Find("核心");
            if (core == null) { Fail("找不到『核心』物体"); return; }

            Vector3 corePos = core.transform.position;
            var col = core.GetComponent<Collider>();
            Vector3 colSize = col != null ? col.bounds.size : Vector3.zero;

            Log($"  核心 修复前: 位置={corePos}  包围盒尺寸={colSize}");

            var valve = GameObject.Find("HeartValve");
            if (valve == null) { Fail("找不到 HeartValve"); return; }
            Log($"  心瓣 位置={valve.transform.position}（保持不变）");

            bool contained = col != null && col.bounds.Contains(valve.transform.position);
            Log($"  心瓣是否被核心包住 = {(contained ? "❌ 是" : "✅ 否")}");

            if (contained)
            {
                // 把核心沿 +Z（北）移动，使它的南边界越过心瓣北边界
                //
                //   核心 z 范围 [14, 18]，心瓣 z 范围 [14.9, 17.1]
                //   要让心瓣不被包住，核心的南边界必须 > 17.1
                //   核心半长 = 2.0，所以中心 z 需 > 19.1
                //   取 z = 21，南边界 = 19，与心瓣北边界 17.1 相距 1.9 米
                float targetZ = 21f;
                Vector3 newPos = new Vector3(corePos.x, corePos.y, targetZ);

                core.transform.position = newPos;
                EditorUtility.SetDirty(core);

                Log($"  → 核心 移到 {newPos}（北移 {targetZ - corePos.z:F1}m）");
            }

            // ══ 2. 给心瓣加一点自发光，让它在昏暗的核心腔里显眼 ══
            var valveRenderer = valve.GetComponent<Renderer>();
            if (valveRenderer != null && valveRenderer.sharedMaterial != null)
            {
                Log($"  心瓣材质 = {valveRenderer.sharedMaterial.name}（不改材质，仅确认存在）");
            }

            // ══ 3. 保存 ══
            EditorSceneManager.MarkSceneDirty(scene);
            bool ok = EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log(ok ? "场景已保存" : "❌ 保存失败");

            // ══ 4. 验证 ══
            // ⚠️ 移动 Transform 后必须 SyncTransforms，否则 Collider.bounds 仍是旧值
            Physics.SyncTransforms();
            Log("---- 验证（已 SyncTransforms）----");
            var core2 = GameObject.Find("核心");
            var col2 = core2.GetComponent<Collider>();
            var valve2 = GameObject.Find("HeartValve");

            Log($"  核心 位置 = {core2.transform.position}");
            Log($"  核心 世界范围 x[{col2.bounds.min.x:F1},{col2.bounds.max.x:F1}] " +
                $"y[{col2.bounds.min.y:F1},{col2.bounds.max.y:F1}] " +
                $"z[{col2.bounds.min.z:F1},{col2.bounds.max.z:F1}]");
            Log($"  心瓣 位置 = {valve2.transform.position}");
            Log($"  心瓣是否仍被包住 = {(col2.bounds.Contains(valve2.transform.position) ? "❌ 是" : "✅ 否")}");

            // 玩家站在心瓣南侧能否看到它
            Vector3 eye = new Vector3(0f, 1.67f, 13f);
            Vector3 target = valve2.transform.position + Vector3.up * 0.3f;
            Vector3 d = target - eye;
            bool blocked = Physics.Raycast(eye, d.normalized, out var hit, d.magnitude,
                                           ~0, QueryTriggerInteraction.Ignore);
            Log($"  从 (0,1.67,13) 看心瓣: " +
                (blocked ? $"❌ 被 '{hit.collider.gameObject.name}' 挡住" : "✅ 可见"));

            // 走到心瓣跟前（2 米内）
            Vector3 near = new Vector3(0f, 1.67f, 14.5f);
            Vector3 d2 = target - near;
            bool blocked2 = Physics.Raycast(near, d2.normalized, out var hit2, d2.magnitude,
                                            ~0, QueryTriggerInteraction.Ignore);
            Log($"  从 (0,1.67,14.5) 看心瓣: " +
                (blocked2 ? (hit2.collider.gameObject.name == "HeartValve"
                    ? "✅ 可见（命中心瓣自身）"
                    : $"❌ 被 '{hit2.collider.gameObject.name}' 挡住")
                    : "⚠️ 未命中任何物体"));

            Log("完成");
        }

        private static void Log(string msg)
        {
            string line = $"[Ch4Fix] {msg}";
            Debug.Log(line);
            Write(line);
        }

        private static void Fail(string msg)
        {
            string line = $"[Ch4Fix] ❌ {msg}";
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
                File.AppendAllText(Path.Combine(dir, "Ch4G5G7Fix_Result.txt"),
                                   line + "\n", new System.Text.UTF8Encoding(false));
            }
            catch { }
        }
    }
}