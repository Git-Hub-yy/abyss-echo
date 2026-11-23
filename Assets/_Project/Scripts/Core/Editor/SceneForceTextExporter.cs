using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// 强制将场景导出为文本（YAML）格式。
    ///
    /// 背景：
    ///   批处理模式下 EditorSceneManager.SaveScene 会按"当前内存中的序列化设置"写文件，
    ///   而该设置从 Library 缓存读取，直接改 ProjectSettings/EditorSettings.asset 不生效，
    ///   导致场景被写成二进制，文本工具无法处理。
    ///
    /// 本工具的做法：
    ///   1. 设置 EditorSettings 的序列化模式（通过 SerializedObject，会正确落盘）
    ///   2. 用 AssetDatabase 强制重新导入场景，逼 Unity 按新设置重写文件
    /// </summary>
    public static class SceneForceTextExporter
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";

        [MenuItem("ABYSS ECHO/工具：强制场景导出为文本")]
        public static void Export()
        {
            // ── 1. 设置序列化模式 ──
            var objs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/EditorSettings.asset");
            if (objs != null && objs.Length > 0)
            {
                var so = new SerializedObject(objs[0]);
                var prop = so.FindProperty("m_SerializationMode");
                if (prop != null)
                {
                    prop.intValue = 1;   // ForceText
                    so.ApplyModifiedProperties();
                    AssetDatabase.SaveAssets();
                    Debug.Log("[ForceText] m_SerializationMode = 1");
                }
            }

            // ── 2. 打开并重新保存 ──
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("[ForceText] 无法打开场景");
                return;
            }

            // 关键：先用 SaveScene 写一遍，再 ForceReimport 逼它按设置重写
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();

            // ── 3. 验证并报告 ──
            string full = Path.GetFullPath(ScenePath);
            bool isText = false;
            long size = 0;
            if (File.Exists(full))
            {
                size = new FileInfo(full).Length;
                var bytes = File.ReadAllBytes(full);
                isText = bytes.Length > 5 && bytes[0] == 0x25 && bytes[1] == 0x59;   // "%Y"
            }

            Debug.Log($"[ForceText] 验证：{(isText ? "✅ 文本格式" : "⚠ 仍非文本")}，大小 {size / 1024} KB");

            if (!isText)
            {
                Debug.LogWarning("[ForceText] 仍未转为文本。可能原因：");
                Debug.LogWarning("  · EditorSettings 需要在启动时生效，批处理内修改无效");
                Debug.LogWarning("  · 建议：在 Unity 编辑器里手动 File > Save Project，或用编辑器打开一次场景后再次保存");
            }
        }
    }
}