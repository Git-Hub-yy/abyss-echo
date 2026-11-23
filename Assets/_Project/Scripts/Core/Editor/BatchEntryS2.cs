using UnityEditor;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// 批处理入口：S2 场景搭建 + 自检。
    /// 用法：
    ///   Unity.exe -batchmode -quit -projectPath &lt;工程&gt; -executeMethod AbyssEcho.Core.EditorTools.BatchEntry.RunS2Build
    /// </summary>
    public static partial class BatchEntryS2
    {
        public static void RunS2Build()
        {
            Debug.Log("[ABYSS ECHO] S2 场景搭建开始");
            PlayerSceneBuilder.BuildAll();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ABYSS ECHO] S2 场景搭建完成");
        }
    }
}