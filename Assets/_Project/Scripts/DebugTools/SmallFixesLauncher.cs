using UnityEngine;
namespace AbyssEcho.DebugTools {
    public static class SmallFixesLauncher {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() {
            if (!Debug.isDebugBuild) return;
            bool want = false;
            foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-smallfix") { want = true; break; }
            if (!want) return;
            if (Object.FindFirstObjectByType<SmallFixesTest>() != null) return;
            var go = new GameObject("--- SMALLFIX ---");
            go.AddComponent<SmallFixesTest>();
            Object.DontDestroyOnLoad(go);
        }
    }
}