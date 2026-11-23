using UnityEngine;
namespace AbyssEcho.DebugTools {
    public static class JKeyTraceLauncher {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() {
            if (!Debug.isDebugBuild) return;
            bool want = false;
            foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-jtrace") { want = true; break; }
            if (!want) return;
            if (Object.FindFirstObjectByType<JKeyFrameTrace>() != null) return;
            var go = new GameObject("--- JTRACE ---");
            go.AddComponent<JKeyFrameTrace>();
            Object.DontDestroyOnLoad(go);
        }
    }
}