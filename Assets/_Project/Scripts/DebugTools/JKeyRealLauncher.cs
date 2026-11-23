using UnityEngine;
namespace AbyssEcho.DebugTools {
    public static class JKeyRealLauncher {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() {
            if (!Debug.isDebugBuild) return;
            bool want = false;
            foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-jkey") { want = true; break; }
            if (!want) return;
            if (Object.FindFirstObjectByType<JKeyRealTest>() != null) return;
            var go = new GameObject("--- JKEY ---");
            go.AddComponent<JKeyRealTest>();
            Object.DontDestroyOnLoad(go);
        }
    }
}