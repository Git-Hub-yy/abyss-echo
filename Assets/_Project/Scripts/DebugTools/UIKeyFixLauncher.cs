using UnityEngine;
namespace AbyssEcho.DebugTools {
    public static class UIKeyFixLauncher {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() {
            if (!Debug.isDebugBuild) return;
            bool want = false;
            foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-uikey") { want = true; break; }
            if (!want) return;
            if (Object.FindFirstObjectByType<UIKeyFixTest>() != null) return;
            var go = new GameObject("--- UIKEY ---");
            go.AddComponent<UIKeyFixTest>();
            Object.DontDestroyOnLoad(go);
        }
    }
}