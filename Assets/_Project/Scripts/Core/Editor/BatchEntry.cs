using UnityEditor;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// 批处理入口：用于命令行无界面执行 S1 初始化与自检，避免依赖人工点击菜单。
    /// 用法：
    ///   Unity.exe -batchmode -quit -projectPath &lt;工程&gt; -executeMethod AbyssEcho.Core.EditorTools.BatchEntry.RunS1
    /// </summary>
    public static class BatchEntry
    {
        public static void RunS1()
        {
            Debug.Log("[ABYSS ECHO] BatchEntry.RunS1 开始");
            ProjectBootstrap.Initialize();
            ProjectHealthCheck.Run();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ABYSS ECHO] BatchEntry.RunS1 结束");
        }

        public static void HealthCheckOnly()
        {
            ProjectHealthCheck.Run();
        }

        public static void BuildWindowsSmokeTest()
        {
            BuildTools.BuildWindowsSmokeTest();
        }
    }
}