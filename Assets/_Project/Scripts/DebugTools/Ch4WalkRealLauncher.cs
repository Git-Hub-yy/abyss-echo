using UnityEngine;
namespace AbyssEcho.DebugTools {
    public static class Ch4WalkRealLauncher {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() {
            if (!Debug.isDebugBuild) return;
            if (System.Environment.GetEnvironmentVariable("AE_TEST") != "walk") return;
            var s = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!s.name.Contains("Chapter4")) return;
            if (Object.FindFirstObjectByType<Ch4WalkRealTest>() != null) return;
            var go = new GameObject("--- CH4WALKREAL ---");
            go.AddComponent<Ch4WalkRealTest>();
            Object.DontDestroyOnLoad(go);
        }
    }
}