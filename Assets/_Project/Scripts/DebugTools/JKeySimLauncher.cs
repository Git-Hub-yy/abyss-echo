using UnityEngine;
namespace AbyssEcho.DebugTools {
    public static class JKeySimLauncher {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() {
            if (!Debug.isDebugBuild) return;
            bool want = false;
            foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-jsim") { want = true; break; }
            if (!want) return;
            if (Object.FindFirstObjectByType<JKeySimTest>() != null) return;
            var go = new GameObject("--- JSIM ---");
            go.AddComponent<JKeySimTest>();
            Object.DontDestroyOnLoad(go);
        }
    }
}