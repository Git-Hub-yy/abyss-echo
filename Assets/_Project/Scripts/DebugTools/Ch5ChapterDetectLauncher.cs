using UnityEngine;
namespace AbyssEcho.DebugTools {
    public static class Ch5ChapterDetectLauncher {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() {
            if (!Debug.isDebugBuild) return;
            bool want = false;
            foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-ch5detect") { want = true; break; }
            if (!want) return;
            var s = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!s.name.Contains("Chapter5")) return;
            if (Object.FindFirstObjectByType<Ch5ChapterDetectTest>() != null) return;
            var go = new GameObject("--- CH5DETECT ---");
            go.AddComponent<Ch5ChapterDetectTest>();
            Object.DontDestroyOnLoad(go);
            Debug.Log("[Ch5Detect] 测试组件已挂载");
        }
    }
}