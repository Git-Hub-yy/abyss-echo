using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// 清理场景中残留的调试空对象。
    ///
    /// 来源：我在排查问题时用 BuildToolsRepro 等工具临时创建了
    ///   --- DOORTEST --- / --- DEEPDIAG --- / --- SAFETY --- / --- DIAG ---
    /// 这些对象被保存进了场景，虽然组件已移除，但空对象会随发布版打包出去。
    ///
    /// 本工具递归清理所有以 "--- " 开头且**没有任何组件**（只有 Transform）的对象，
    /// 但保留 --- DEBUG ---（那是正式装配用的根节点）。
    /// </summary>
    public static class CleanupDebugObjects
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";

        /// <summary>需要保留的根节点（正式内容）。</summary>
        private static readonly string[] Keep = { "--- DEBUG ---" };

        [MenuItem("ABYSS ECHO/工具：清理调试空对象")]
        public static void Clean()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[Cleanup] 请先打开 Level_Chapter1_Graybox 场景");
                return;
            }

            var roots = scene.GetRootGameObjects();
            int removed = 0;

            foreach (var go in roots)
            {
                if (go == null) continue;

                bool isKeep = false;
                foreach (var k in Keep) if (go.name == k) { isKeep = true; break; }
                if (isKeep) continue;

                // 只清理 "--- XXX ---" 形式且没有实际组件的对象
                if (!go.name.StartsWith("---")) continue;

                var comps = go.GetComponents<Component>();
                bool onlyTransform = comps.Length <= 1;
                bool hasChildren = go.transform.childCount > 0;

                if (onlyTransform && !hasChildren)
                {
                    Debug.Log($"[Cleanup] 删除空调试对象：{go.name}");
                    Object.DestroyImmediate(go);
                    removed++;
                }
                else if (onlyTransform && hasChildren)
                {
                    Debug.Log($"[Cleanup] 保留（有子对象）：{go.name}，子对象 {go.transform.childCount} 个");
                }
                else
                {
                    Debug.Log($"[Cleanup] 保留（有组件）：{go.name}，组件 {comps.Length} 个");
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"<b>[Cleanup]</b> 清理完成，删除 {removed} 个空调试对象");
        }
    }
}