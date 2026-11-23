using UnityEngine;

namespace AbyssEcho.DebugTools
{
    public static class Ch4ObjectiveLauncher
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Debug.isDebugBuild) return;

            // 用命令行参数判断（环境变量在 Start-Process 下不可靠）
            bool want = false;
            var args = System.Environment.GetCommandLineArgs();
            foreach (var a in args)
                if (a == "-ch4obj") { want = true; break; }
            if (!want) return;

            var s = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!s.name.Contains("Chapter4")) return;
            if (Object.FindFirstObjectByType<Ch4ObjectiveTest>() != null) return;

            var go = new GameObject("--- CH4OBJ ---");
            go.AddComponent<Ch4ObjectiveTest>();
            Object.DontDestroyOnLoad(go);
            Debug.Log("[Ch4Obj] 测试组件已挂载");
        }
    }
}