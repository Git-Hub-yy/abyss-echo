using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>Batch entry for S5 monster: bake navmesh + setup monster.</summary>
    public static class BatchEntryS5Monster
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";

        public static void RunAll()
        {
            Debug.Log("[ABYSS ECHO] S5-MONSTER START");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("[S5M] Cannot open scene");
                EditorApplication.Exit(1);
                return;
            }

            // ① 先用 NavMeshSurface 烘焙（持久化到场景）
            NavMeshBaker.Bake();
            AssetDatabase.SaveAssets();

            // ② 再布置怪物（此时能检测到导航网格已存在）
            MonsterSetupBuilder.Setup();
            AssetDatabase.SaveAssets();

            scene = EditorSceneManager.GetActiveScene();
            if (scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[ABYSS ECHO] Final scene save: " + scene.path);
            }

            AssetDatabase.Refresh();
            Debug.Log("[ABYSS ECHO] S5-MONSTER DONE");
        }
    }
}