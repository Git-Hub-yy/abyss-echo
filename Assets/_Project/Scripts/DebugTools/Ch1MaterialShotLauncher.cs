using UnityEngine;
namespace AbyssEcho.DebugTools {
    public static class Ch1MaterialShotLauncher {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() {
            if (!Debug.isDebugBuild) return;
            if (System.Environment.GetEnvironmentVariable("AE_TEST") != "matshot") return;
            var s = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!s.name.Contains("Chapter1")) return;
            if (Object.FindFirstObjectByType<Ch1MaterialShotTest>() != null) return;
            var go = new GameObject("--- CH1MATSHOT ---");
            go.AddComponent<Ch1MaterialShotTest>();
            Object.DontDestroyOnLoad(go);
        }
    }
}