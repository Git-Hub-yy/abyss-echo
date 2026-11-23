using UnityEngine;

namespace AbyssEcho.DebugTools
{
    public static class MaterialShotLauncher
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Debug.isDebugBuild) return;
            if (System.Environment.GetEnvironmentVariable("AE_TEST") != "matshot") return;

            var s = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!s.name.Contains("Chapter")) return;
            if (Object.FindFirstObjectByType<MaterialShotTest>() != null) return;

            var go = new GameObject("--- MATSHOT ---");
            go.AddComponent<MaterialShotTest>();
            Object.DontDestroyOnLoad(go);
        }
    }
}