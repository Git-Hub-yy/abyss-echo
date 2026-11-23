using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// 复核：五章系统组件矩阵（场景静态检查，只读）。
    ///
    /// ⚠️ 上一轮审计的教训：必须同时给出
    ///   · 组件是否存在
    ///   · 挂在哪个 GameObject 上（完整路径）
    ///   · 该 GameObject 是否 activeInHierarchy
    /// 否则无法判断"存在但未启用"和"根本不存在"。
    /// </summary>
    public static class SystemsMatrixCheck
    {
        private static readonly string[] Scenes =
        {
            "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity",
            "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity",
            "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity",
            "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity",
            "Assets/_Project/Scenes/Level_Chapter5_Graybox.unity",
        };

        [MenuItem("ABYSS ECHO/复核/系统组件矩阵")]
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("========== 五章系统组件矩阵（场景静态检查）==========");
            sb.AppendLine("格式：[场景] 组件名 → 数量 | 挂载路径（active 状态）");
            sb.AppendLine();

            foreach (var path in Scenes)
            {
                if (!File.Exists(path)) { sb.AppendLine($"### {Path.GetFileName(path)} ❌ 不存在"); continue; }

                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                string sn = Path.GetFileNameWithoutExtension(path);

                sb.AppendLine($"### {sn}");

                Dump<AbyssEcho.Horror.FearSystem>(sb, "FearSystem");
                Dump<AbyssEcho.Horror.FearVisuals>(sb, "FearVisuals");
                Dump<UnityEngine.Rendering.Volume>(sb, "Volume");
                Dump<AbyssEcho.Horror.PlayerDeathSystem>(sb, "PlayerDeathSystem");
                Dump<AbyssEcho.Horror.PlayerFootsteps>(sb, "PlayerFootsteps");
                Dump<AbyssEcho.Horror.FlickerLight>(sb, "FlickerLight");

                sb.AppendLine();
            }

            // 额外：PostProcess 节点与 RenderSettings
            sb.AppendLine("========== 后处理与渲染设置 ==========");
            foreach (var path in Scenes)
            {
                if (!File.Exists(path)) continue;
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                sb.AppendLine();
                sb.AppendLine($"### {Path.GetFileNameWithoutExtension(path)}");
                sb.AppendLine($"  RenderSettings.fog = {RenderSettings.fog}  density={RenderSettings.fogDensity}  color={RenderSettings.fogColor}");
                sb.AppendLine($"  RenderSettings.ambientMode = {RenderSettings.ambientMode}  ambientLight={RenderSettings.ambientLight}");

                // 找名为 POST_PROCESS 的节点
                bool foundPP = false;
                foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                {
                    if (t == null || t.name != "--- POST_PROCESS ---") continue;
                    if (!t.gameObject.scene.IsValid()) continue;
                    foundPP = true;
                    sb.AppendLine($"  找到 --- POST_PROCESS --- 节点 @ {GetPath(t)}  子物体数={t.childCount}");
                    foreach (var v in t.GetComponentsInChildren<UnityEngine.Rendering.Volume>(true))
                        sb.AppendLine($"    Volume: {v.gameObject.name} isGlobal={v.isGlobal} weight={v.weight} profile={(v.sharedProfile != null ? v.sharedProfile.name : "NULL")}");
                }
                if (!foundPP) sb.AppendLine("  ❌ 没有 --- POST_PROCESS --- 节点");
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports/SystemsMatrix.txt")), text, new UTF8Encoding(false));
        }

        private static void Dump<T>(StringBuilder sb, string label) where T : Behaviour
        {
            var arr = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            if (arr.Length == 0) { sb.AppendLine($"  {label,-20} : ❌ MISSING (0)"); return; }

            sb.AppendLine($"  {label,-20} : PRESENT ({arr.Length})");
            foreach (var c in arr)
            {
                string st = c.gameObject.activeInHierarchy ? "active" : "⚠️INACTIVE";
                string en = c.enabled ? "enabled" : "⚠️disabled";
                sb.AppendLine($"      {GetPath(c.transform)}  [{st}/{en}]");
            }
        }

        private static string GetPath(Transform t)
        {
            string p = t.name;
            var cur = t.parent;
            int guard = 0;
            while (cur != null && guard++ < 20) { p = cur.name + "/" + p; cur = cur.parent; }
            return p;
        }
    }
}