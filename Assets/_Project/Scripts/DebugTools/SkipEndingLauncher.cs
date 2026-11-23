using UnityEngine;
namespace AbyssEcho.DebugTools {
    public static class SkipEndingLauncher {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() {
            if (!Debug.isDebugBuild) return;
            bool want = false;
            foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-skiptest") { want = true; break; }
            if (!want) return;
            if (Object.FindFirstObjectByType<SkipEndingTest>() != null) return;
            var go = new GameObject("--- SKIPTEST ---");
            go.AddComponent<SkipEndingTest>();
            Object.DontDestroyOnLoad(go);
        }
    }
}