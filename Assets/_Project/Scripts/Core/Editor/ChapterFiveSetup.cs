using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S37：第五章的标准装配流程。
    ///
    /// 把四个必做步骤串起来：音频 → 灯光 → 交互 → UI。
    ///
    /// ⚠️ 为什么必须每一步都有：
    ///   · 音频：AudioManager 不自动创建，漏了整章没声音（第二章事故）
    ///   · 灯光：RoomLightBuilder 读的是"当前章节"，必须先切章（第二章只有 1 盏灯的事故）
    ///   · 交互：玩家需要 InteractionDetector 才能按 E
    ///   · UI：准星、暂停菜单、标题卡
    /// </summary>
    public static class ChapterFiveSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter5_Graybox.unity";

        [MenuItem("ABYSS ECHO/S39 第五章全套装配")]
        public static void RunAll()
        {
            var prev = AbyssEcho.LevelGen.ChapterLayout.Current;
            AbyssEcho.LevelGen.ChapterLayout.Current = AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter5;

            try
            {
                // ① 音频
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                if (!scene.IsValid()) { Debug.LogError("[Ch5Setup] 无法打开场景"); return; }

                AddAudio();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);

                // ② 灯光（RoomLightBuilder 读当前章节，已切到 Ch4）
                RoomLightBuilder.Build();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);

                // ③ 交互
                InteractionSetupBuilder.Setup();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);

                // ④ UI
                UISetupBuilder.Setup();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);

                AssetDatabase.SaveAssets();

                int lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length;
                var audio = Object.FindFirstObjectByType<AbyssEcho.Audio.AudioManager>();
                var det = Object.FindFirstObjectByType<AbyssEcho.Interaction.InteractionDetector>();
                var cross = Object.FindFirstObjectByType<AbyssEcho.UI.CrosshairUI>();

                Debug.Log($"<b>[Ch5Setup]</b> 第五章装配完成\n" +
                          $"  灯光 {lights} 盏\n" +
                          $"  音频={(audio != null ? "有" : "无")}\n" +
                          $"  交互={(det != null ? "有" : "无")}\n" +
                          $"  准星={(cross != null ? "有" : "无")}");
            }
            finally
            {
                AbyssEcho.LevelGen.ChapterLayout.Current = prev;
            }
        }

        private static void AddAudio()
        {
            var go = GameObject.Find("AudioManager");
            if (go == null) go = new GameObject("AudioManager");

            var a = go.GetComponent<AbyssEcho.Audio.AudioManager>();
            if (a == null) a = go.AddComponent<AbyssEcho.Audio.AudioManager>();

            a.masterVolume = 0.8f;
            a.sfxVolume = 0.85f;
            a.ambientVolume = 0.32f;

            EditorUtility.SetDirty(go);
            EditorUtility.SetDirty(a);
            Debug.Log("[Ch5Setup] AudioManager 已装配");
        }
    }
}