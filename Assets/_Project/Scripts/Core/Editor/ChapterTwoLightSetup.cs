using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S26：给第二章补全灯光。
    ///
    /// ⚠️ 为什么需要独立入口：
    ///   RoomLightBuilder 内部用 ChapterLayout.GetRooms()（不传章节），
    ///   取的是「当前章节」。而第二章场景重建后没有跑过带切章的流程，
    ///   结果整个第二章只有 1 盏灯 —— 23 个房间几乎全黑，没法玩。
    ///
    ///   这里显式切到第二章再跑，跑完恢复原章节。
    /// </summary>
    public static class ChapterTwoLightSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity";

        [MenuItem("ABYSS ECHO/S26 给第二章布置灯光")]
        public static void RunAll()
        {
            var prev = AbyssEcho.LevelGen.ChapterLayout.Current;
            AbyssEcho.LevelGen.ChapterLayout.Current = AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter2;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("[Ch2Light] 无法打开第二章场景");
                AbyssEcho.LevelGen.ChapterLayout.Current = prev;
                EditorApplication.Exit(1);
                return;
            }

            RoomLightBuilder.Build();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            AbyssEcho.LevelGen.ChapterLayout.Current = prev;
            Debug.Log("<b>[Ch2Light]</b> 第二章灯光布置完成");
        }
    }
}