using UnityEngine;
namespace AbyssEcho.DebugTools {
    public static class Ch3EndingTruthLauncher {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() {
            if (!Debug.isDebugBuild) return;
            if (!string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("CH3_CASE"))) return;
            var s = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!s.name.Contains("Chapter3")) return;
            if (Object.FindFirstObjectByType<Ch3EndingTruthTest>() != null) return;
            var go = new GameObject("--- CH3ENDINGTRUTH ---");
            go.AddComponent<Ch3EndingTruthTest>();
            Object.DontDestroyOnLoad(go);
        }
    }
}