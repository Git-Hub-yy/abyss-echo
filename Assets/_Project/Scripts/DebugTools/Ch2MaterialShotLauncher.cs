using UnityEngine;
namespace AbyssEcho.DebugTools {
    public static class Ch2MaterialShotLauncher {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() {
            if (!Debug.isDebugBuild) return;
            if (System.Environment.GetEnvironmentVariable("AE_TEST") != "ch2mat") return;
            var s = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!s.name.Contains("Chapter2")) return;
            if (Object.FindFirstObjectByType<Ch2MaterialShotTest>() != null) return;
            var go = new GameObject("--- CH2MATSHOT ---");
            go.AddComponent<Ch2MaterialShotTest>();
            Object.DontDestroyOnLoad(go);
        }
    }
}