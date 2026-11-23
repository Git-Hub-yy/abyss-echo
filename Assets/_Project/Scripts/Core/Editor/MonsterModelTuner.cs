using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// 怪物模型二次配置：修正缩放与动画循环。
    ///
    /// 第一次导入发现的问题：
    ///   1. 模型原始高度 5.94 米 —— 需要缩放到约 1.9 米（比玩家 1.78 略高，更有压迫感）
    ///   2. 所有动画循环=False —— 巡逻/追击需要循环播放
    /// </summary>
    public static class MonsterModelTuner
    {
        private const string ModelPath = "Assets/_Project/Art/Models/Monster/Monster_Base.fbx";

        /// <summary>目标身高（米）。玩家 1.78，怪物略高一点更有压迫感。</summary>
        private const float TargetHeight = 1.95f;

        [MenuItem("ABYSS ECHO/S5 修正怪物模型缩放与动画")]
        public static void Tune()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) { Debug.LogError("[MonsterTuner] 找不到模型"); return; }

            var sb = new StringBuilder();
            sb.AppendLine("ABYSS ECHO · 怪物模型修正");
            sb.AppendLine("=====================================");

            // ① 缩放
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            float originalHeight = 0f;
            if (fbx != null)
            {
                var rends = fbx.GetComponentsInChildren<Renderer>();
                if (rends.Length > 0)
                {
                    Bounds b = rends[0].bounds;
                    originalHeight = b.size.y;
                }
            }

            if (originalHeight > 0.01f)
            {
                float scale = TargetHeight / originalHeight;
                importer.globalScale = scale;
                sb.AppendLine($"原始高度：{originalHeight:F2} m");
                sb.AppendLine($"目标高度：{TargetHeight:F2} m");
                sb.AppendLine($"缩放系数：{scale:F4}");
            }
            else
            {
                sb.AppendLine("⚠ 无法读取原始高度，保持默认缩放");
            }

            // ② 动画循环
            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Generic;

            var clips = importer.defaultClipAnimations;
            if (clips != null && clips.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine("动画循环设置：");
                for (int i = 0; i < clips.Length; i++)
                {
                    string n = clips[i].name;
                    // Idle / Walk / Run 需要循环；Death / Punch / Jump 不循环
                    bool loop = n.Contains("Idle") || n.Contains("Walk") || n.Contains("Run") ||
                                n.Contains("Working");
                    clips[i].loopTime = loop;
                    clips[i].loopPose = loop;
                    sb.AppendLine($"  · {n}  loop={loop}");
                }
                importer.clipAnimations = clips;
            }
            else
            {
                sb.AppendLine("⚠ defaultClipAnimations 为空，尝试用 clipAnimations");
            }

            importer.SaveAndReimport();
            AssetDatabase.Refresh();

            // ③ 读回验证
            var fbx2 = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            sb.AppendLine();
            if (fbx2 != null)
            {
                var rends = fbx2.GetComponentsInChildren<Renderer>();
                if (rends.Length > 0)
                {
                    Bounds b = rends[0].bounds;
                    sb.AppendLine($"修正后高度：{b.size.y:F2} m");
                    sb.AppendLine($"修正后包围盒：{b.size}");
                }
            }

            sb.AppendLine("=====================================");
            string text = sb.ToString();
            Debug.Log("<b>[MonsterTuner]</b>\n" + text);

            try
            {
                string dir = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "S5_MonsterTune.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}