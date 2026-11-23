using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S56 调查用启动器。
    ///
    /// 用 [RuntimeInitializeOnLoadMethod] 在场景加载后自动挂上审计组件，
    /// 这样**不需要修改任何场景文件**就能跑视觉审计。
    ///
    /// 只在 Development 构建中生效（Debug.isDebugBuild）。
    /// </summary>
    public static class Ch4VisualAuditLauncher
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Debug.isDebugBuild) return;

            // 只在本章节场景生效
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!scene.name.Contains("Chapter4")) return;

            if (Object.FindFirstObjectByType<Ch4VisualAudit>() != null) return;

            var go = new GameObject("--- CH4VISUAL ---");
            go.AddComponent<Ch4VisualAudit>();
            Object.DontDestroyOnLoad(go);

            Debug.Log("[Ch4Visual] 审计组件已挂载（未修改场景文件）");
        }
    }
}