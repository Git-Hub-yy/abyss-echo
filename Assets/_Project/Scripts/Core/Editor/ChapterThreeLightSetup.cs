using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S32：给第三章布置灯光。
    ///
    /// ⚠️ RoomLightBuilder.Build() 内部读的是 ChapterLayout.GetRooms()（当前章节），
    ///   所以必须先切到 Chapter3 再调用，否则会把第一章的灯布置到第三章场景里
    ///   —— 这正是第二章"只有 1 盏灯"事故的成因，这里显式规避。
    /// </summary>
    public static class ChapterThreeLightSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity";

        [MenuItem("ABYSS ECHO/S32 布置第三章灯光")]
        public static void RunAll()
        {
            var prev = AbyssEcho.LevelGen.ChapterLayout.Current;
            AbyssEcho.LevelGen.ChapterLayout.Current =
                AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter3;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("[Ch3Light] 无法打开第三章场景");
                AbyssEcho.LevelGen.ChapterLayout.Current = prev;
                EditorApplication.Exit(1);
                return;
            }

            RoomLightBuilder.Build();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            int lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length;
            Debug.Log($"<b>[Ch3Light]</b> 第三章灯光布置完成，共 {lights} 盏");

            AbyssEcho.LevelGen.ChapterLayout.Current = prev;
        }
    }
}