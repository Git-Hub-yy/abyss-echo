using UnityEngine;
namespace AbyssEcho.DebugTools {
    public static class Ch3LampEndingLauncher {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() {
            if (!Debug.isDebugBuild) return;
            var c = System.Environment.GetEnvironmentVariable("CH3_CASE");
            if (c != "bulb" && c != "bulb_vs_tape") return;
            var s = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!s.name.Contains("Chapter3")) return;
            if (Object.FindFirstObjectByType<Ch3LampEndingTest>() != null) return;
            var go = new GameObject("--- CH3LAMP ---");
            go.AddComponent<Ch3LampEndingTest>();
            Object.DontDestroyOnLoad(go);
        }
    }
}