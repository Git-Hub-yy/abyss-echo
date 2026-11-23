using UnityEngine;
namespace AbyssEcho.DebugTools {
    public static class Ch4ExitVisualLauncher {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() {
            if (!Debug.isDebugBuild) return;
            if (System.Environment.GetEnvironmentVariable("AE_TEST") != "visual") return;            
            var s = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!s.name.Contains("Chapter4")) return;
            if (Object.FindFirstObjectByType<Ch4ExitVisualTest>() != null) return;
            var go = new GameObject("--- CH4EXITVISUAL ---");
            go.AddComponent<Ch4ExitVisualTest>();
            Object.DontDestroyOnLoad(go);
        }
    }
}