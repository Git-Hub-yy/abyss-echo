using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// 在第二章场景里装配全部内容（灯光/交互/道具/怪物/恐怖/UI/存档）。
    ///
    /// 与第一章的区别：这里明确**切到第二章**再跑各个 Builder，
    /// 保证所有坐标、文案、触发点都指向第二章。
    /// </summary>
    public static class ChapterTwoFullSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity";

        [MenuItem("ABYSS ECHO/S14C 装配第二章全部内容")]
        public static void RunAll()
        {
            Debug.Log("[ABYSS ECHO] S14C START");

            var prevChapter = AbyssEcho.LevelGen.ChapterLayout.Current;
            AbyssEcho.LevelGen.ChapterLayout.Current = AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter2;
            Debug.Log("[ABYSS ECHO] 已切到第二章");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("[S14C] 无法打开第二章场景");
                AbyssEcho.LevelGen.ChapterLayout.Current = prevChapter;
                EditorApplication.Exit(1);
                return;
            }

            try
            {
                // ① 灯光（读 ChapterLayout → 第二章房间）
                AbyssEcho.LevelGen.ChapterLayout.Current = AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter2;
                RoomLightBuilder.Build();
                Save();

                // ② 交互物（抽水泵/录音机/频率终端/笔记/门）
                ChapterTwoContentBuilder.Build();
                Save();

                // ③ 恐怖系统（深水/怪物/触发器）
                ChapterTwoHorrorBuilder.Build();
                Save();

                // ④ UI + 结局 + 存档
                UISetupBuilder.Setup();
                Save();

                Debug.Log("<b>[S14C] 第二章内容装配完成</b>");
            }
            catch (System.Exception e)
            {
                Debug.LogError("[S14C] 装配异常：" + e);
                EditorApplication.Exit(1);
            }
            finally
            {
                AbyssEcho.LevelGen.ChapterLayout.Current = prevChapter;
            }

            AssetDatabase.Refresh();
            Debug.Log("[ABYSS ECHO] S14C DONE");
        }

        private static void Save()
        {
            var sc = EditorSceneManager.GetActiveScene();
            if (!sc.IsValid()) return;
            EditorSceneManager.MarkSceneDirty(sc);
            EditorSceneManager.SaveScene(sc);
        }
    }
}