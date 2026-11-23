using UnityEngine;
namespace AbyssEcho.DebugTools {
    public static class Ch3InteractFixLauncher {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() {
            if (!Debug.isDebugBuild) return;
            if (System.Environment.GetEnvironmentVariable("CH3_CASE") != "interact") return;
            var s = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!s.name.Contains("Chapter3")) return;
            if (Object.FindFirstObjectByType<Ch3InteractFixTest>() != null) return;
            var go = new GameObject("--- CH3INTERACT ---");
            go.AddComponent<Ch3InteractFixTest>();
            Object.DontDestroyOnLoad(go);
        }
    }
}