using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S29：给第二章补 AudioManager。
    ///
    /// ⚠️ 问题（玩家实测"第二章没有声音"）：
    ///   AudioManager 是单例，但**不会自动创建**——
    ///   只有 HorrorSetupBuilder（第一章）里有一行 AddComponent。
    ///
    ///   第二章走的是 ChapterTwoHorrorBuilder，它从未创建 AudioManager。
    ///   结果 AudioManager.Instance == null，
    ///   所有音效调用（脚步、门、抽水、惊吓）都在 `if (Instance != null)` 判断里被静默跳过。
    ///
    /// 这解释了为什么第二章"完全没声音"而不是"声音不对"。
    /// </summary>
    public static class ChapterTwoAudioSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity";

        [MenuItem("ABYSS ECHO/S29 给第二章装配音频")]
        public static void RunAll()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("[Ch2Audio] 无法打开第二章场景");
                EditorApplication.Exit(1);
                return;
            }

            // 与第一章完全一致的参数，保证两章听感统一
            var audioGo = GameObject.Find("AudioManager");
            if (audioGo == null) audioGo = new GameObject("AudioManager");

            var audio = audioGo.GetComponent<AbyssEcho.Audio.AudioManager>();
            if (audio == null) audio = audioGo.AddComponent<AbyssEcho.Audio.AudioManager>();

            audio.masterVolume = 0.8f;
            audio.sfxVolume = 0.85f;
            audio.ambientVolume = 0.32f;

            EditorUtility.SetDirty(audioGo);
            EditorUtility.SetDirty(audio);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("<b>[Ch2Audio]</b> 第二章 AudioManager 已装配（master=0.8 sfx=0.85 ambient=0.32）");
        }
    }
}