using UnityEditor;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>批处理入口：S3A 第一章灰盒构建。</summary>
    public static class BatchEntryS3A
    {
        public static void RunBuild()
        {
            Debug.Log("[ABYSS ECHO] S3A 灰盒构建开始");
            ChapterOneSceneBuilder.Build();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ABYSS ECHO] S3A 灰盒构建结束");
        }
    }
}