using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S28：给第二章烘焙导航网格。
    ///
    /// ⚠️ 为什么需要：
    ///   日志里一直有 "Failed to create agent because there is no valid NavMesh"。
    ///   NavMeshBaker 只支持第一章（写死了场景路径），
    ///   第二章从未烘焙过导航网格 —— 怪物生成后无法移动，等于不存在。
    ///
    ///   注意：第二章 D2 大厅有"水面"物体，它是碰撞体已删除的 Cube，
    ///   烘焙前要确保它不参与导航（useGeometry = PhysicsColliders 时它没有碰撞体，天然排除）。
    /// </summary>
    public static class ChapterTwoNavMesh
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity";
        private const string SurfaceName = "--- NAVMESH ---";

        [MenuItem("ABYSS ECHO/S28 烘焙第二章导航网格")]
        public static void Bake()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("[Ch2NavMesh] 无法打开第二章场景");
                EditorApplication.Exit(1);
                return;
            }

            // 移除旧 surface（幂等）
            var old = GameObject.Find(SurfaceName);
            if (old != null) Object.DestroyImmediate(old);

            var go = new GameObject(SurfaceName);
            var surface = go.AddComponent<NavMeshSurface>();

            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            surface.agentTypeID = 0;

            surface.BuildNavMesh();

            int vertCount = 0;
            var data = NavMesh.CalculateTriangulation();
            if (data.vertices != null) vertCount = data.vertices.Length;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"<b>[Ch2NavMesh] 第二章导航网格已烘焙</b>\n" +
                      $"  顶点数：{vertCount}\n" +
                      $"  表面对象：{SurfaceName}");

            if (vertCount == 0)
            {
                Debug.LogError("[Ch2NavMesh] ❌ 顶点数为 0，导航网格烘焙失败！");
                EditorApplication.Exit(1);
            }
        }
    }
}