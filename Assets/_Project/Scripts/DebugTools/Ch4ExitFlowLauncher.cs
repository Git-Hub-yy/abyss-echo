using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>S57 启动器：运行时自动挂载终点门验证组件（不改场景）。</summary>
    public static class Ch4ExitFlowLauncher
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Debug.isDebugBuild) return;
            if (System.Environment.GetEnvironmentVariable("AE_TEST") != "flow") return;            
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!scene.name.Contains("Chapter4")) return;
            if (Object.FindFirstObjectByType<Ch4ExitFlowTest>() != null) return;

            var go = new GameObject("--- CH4EXITFLOW ---");
            go.AddComponent<Ch4ExitFlowTest>();
            Object.DontDestroyOnLoad(go);
        }
    }
}