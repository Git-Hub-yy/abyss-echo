using UnityEngine;
namespace AbyssEcho.DebugTools {
    public static class Ch4RouteVisualLauncher {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() {
            if (!Debug.isDebugBuild) return;
            if (System.Environment.GetEnvironmentVariable("AE_TEST") != "route") return;
            var s = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!s.name.Contains("Chapter4")) return;
            if (Object.FindFirstObjectByType<Ch4RouteVisual>() != null) return;
            var go = new GameObject("--- CH4ROUTE ---");
            go.AddComponent<Ch4RouteVisual>();
            Object.DontDestroyOnLoad(go);
        }
    }
}