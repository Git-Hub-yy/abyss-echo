using UnityEngine;
namespace AbyssEcho.DebugTools {
    public static class CamDirProbeLauncher {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() {
            if (!Debug.isDebugBuild) return;
            if (System.Environment.GetEnvironmentVariable("AE_TEST") != "probe") return;
            var s = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!s.name.Contains("Chapter4")) return;
            var go = new GameObject("--- PROBE ---");
            go.AddComponent<CamDirProbe>();
            Object.DontDestroyOnLoad(go);
        }
    }
}