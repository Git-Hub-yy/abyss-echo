using UnityEngine;
namespace AbyssEcho.DebugTools {
    public static class Ch3EndingCaseLauncher {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() {
            if (!Debug.isDebugBuild) return;
            var cc = System.Environment.GetEnvironmentVariable("CH3_CASE");
            if (cc != "tape" && cc != "fuse" && cc != "none") return;
            var s = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!s.name.Contains("Chapter3")) return;
            if (Object.FindFirstObjectByType<Ch3EndingCaseTest>() != null) return;
            var go = new GameObject("--- CH3CASE ---");
            go.AddComponent<Ch3EndingCaseTest>();
            Object.DontDestroyOnLoad(go);
        }
    }
}