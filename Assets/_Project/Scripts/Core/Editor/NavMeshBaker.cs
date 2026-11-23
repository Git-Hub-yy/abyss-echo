using System.Collections.Generic;
using AbyssEcho.Monster;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// 用 NavMeshSurface（AI Navigation 包）烘焙持久化导航网格。
    ///
    /// 为什么换方案：
    ///   之前用 NavMeshBuilder.BuildNavMeshData + NavMesh.AddNavMeshData，
    ///   这只在"当前运行会话"里有效，打包后场景中不存在该数据
    ///   → 怪物无法寻路（T40 测试失败）。
    ///
    ///   NavMeshSurface 会把烘焙结果**序列化进场景**，打包后依然可用。
    ///   包：com.unity.ai.navigation@1.1.5（Unity 官方，允许商用）
    /// </summary>
    public static class NavMeshBaker
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";
        private const string SurfaceName = "--- NAVMESH ---";

        [MenuItem("ABYSS ECHO/S5 烘焙导航网格")]
        public static void Bake()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[NavMeshBaker] 请先打开 Level_Chapter1_Graybox 场景");
                return;
            }

            // 移除旧 surface
            var old = GameObject.Find(SurfaceName);
            if (old != null) Object.DestroyImmediate(old);

            var go = new GameObject(SurfaceName);
            var surface = go.AddComponent<NavMeshSurface>();

            // 收集参数：用物理碰撞体作为可行走表面
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            surface.agentTypeID = 0;

            // 代理尺寸与怪物一致
            surface.overrideVoxelSize = false;
            surface.overrideTileSize = false;

            // 烘焙
            surface.BuildNavMesh();

            int triCount = 0;
            var data = NavMesh.CalculateTriangulation();
            if (data.vertices != null) triCount = data.vertices.Length;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log($"<b>[NavMeshBaker] 导航网格已烘焙并保存到场景</b>\n" +
                      $"  顶点数：{triCount}\n" +
                      $"  表面对象：{SurfaceName}");
        }
    }
}