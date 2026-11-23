using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// 修复场景序列化格式。
    ///
    /// 背景：某次批处理构建后 Level_Chapter1_Graybox.unity 变成了二进制格式，
    /// 文本工具无法处理（也无法被版本控制 diff）。
    ///
    /// 本工具：
    ///   1. 强制 EditorSettings 为 Force Text
    ///   2. 打开场景并重新保存（Unity 会按当前设置写成文本）
    /// </summary>
    public static class SceneSerializationFixer
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";

        [MenuItem("ABYSS ECHO/工具：修复场景为文本格式")]
        public static void Fix()
        {
            // ① 通过 SerializedObject 设置（比直接改文件可靠）
            var settingsAssets = AssetDatabase.FindAssets("t:EditorSettings");
            // EditorSettings 不是普通 asset，用内部方式：
            var so = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/EditorSettings.asset")[0]);
            var prop = so.FindProperty("m_SerializationMode");
            if (prop != null)
            {
                prop.intValue = 1;   // 1 = ForceText
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                Debug.Log("[SerializationFix] 已设置 m_SerializationMode = 1 (ForceText)");
            }
            else
            {
                Debug.LogWarning("[SerializationFix] 找不到 m_SerializationMode 属性");
            }

            // ② 打开场景并重新保存
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("[SerializationFix] 无法打开场景");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            bool ok = EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"[SerializationFix] 场景重新保存：{(ok ? "成功" : "失败")}");

            // ③ 验证
            string full = System.IO.Path.GetFullPath(ScenePath);
            if (System.IO.File.Exists(full))
            {
                using (var reader = new System.IO.StreamReader(full))
                {
                    string head = reader.ReadLine();
                    bool isText = head != null && head.StartsWith("%YAML");
                    Debug.Log($"[SerializationFix] 格式验证：{(isText ? "✅ 文本格式" : "⚠ 仍为二进制")}");
                }
            }
        }
    }
}