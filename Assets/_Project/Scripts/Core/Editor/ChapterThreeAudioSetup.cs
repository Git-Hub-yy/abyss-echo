using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S32：给第三章装配 AudioManager。
    ///
    /// ⚠️ 这一步**不能省**：
    ///   AudioManager 是单例但不会自动创建。
    ///   第二章就是因为漏了这一步，导致整章完全没声音
    ///   （所有 PlayXxx 调用都在 `if (Instance != null)` 里被静默跳过）。
    ///
    /// 参数与第一章/第二章完全一致，保证三章听感统一。
    /// </summary>
    public static class ChapterThreeAudioSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity";

        [MenuItem("ABYSS ECHO/S32 第三章音频")]
        public static void RunAll()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("[Ch3Audio] 无法打开第三章场景");
                EditorApplication.Exit(1);
                return;
            }

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

            Debug.Log("<b>[Ch3Audio]</b> 第三章 AudioManager 已装配（master=0.8 sfx=0.85 ambient=0.32）");
        }
    }
}