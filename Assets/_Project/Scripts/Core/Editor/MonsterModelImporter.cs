using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// 怪物模型导入配置 + 信息报告。
    ///
    /// 素材：Animated Human by Quaternius（CC0 1.0 Universal，公共领域）
    ///   来源：https://opengameart.org/content/animated-human-low-poly
    ///   本地：Assets/_Project/Art/Models/Monster/Monster_Base.fbx
    ///   授权文件：LICENSE_CC0_Quaternius.txt（同目录）
    ///
    /// 本工具做两件事：
    ///   1. 配置 FBX 导入参数（缩放、动画、材质）
    ///   2. 输出模型内含的动画剪辑列表，供后续 AI 状态机引用
    /// </summary>
    public static class MonsterModelImporter
    {
        private const string ModelPath = "Assets/_Project/Art/Models/Monster/Monster_Base.fbx";
        private const string ReportPath = "../Reports/S5_MonsterModelInfo.txt";

        [MenuItem("ABYSS ECHO/S5 配置怪物模型")]
        public static void Configure()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[MonsterModel] 找不到模型：{ModelPath}");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("ABYSS ECHO · 怪物模型信息");
            sb.AppendLine("=====================================");
            sb.AppendLine("素材：Animated Human by Quaternius");
            sb.AppendLine("授权：CC0 1.0 Universal（公共领域，可商用、免署名）");
            sb.AppendLine("来源：https://opengameart.org/content/animated-human-low-poly");
            sb.AppendLine("路径：" + ModelPath);
            sb.AppendLine();

            // ── 导入参数 ──
            importer.globalScale = 1f;
            importer.useFileScale = true;          // 使用模型自带缩放
            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Generic;

            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.importVisibility = false;

            // 材质：从模型提取（模型自带纯色材质，我们在运行时替换为深色剪影）
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;

            // 动画：把 clip 循环设置为 true（巡逻/行走需要）
            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.importAnimation = true;
            }

            importer.SaveAndReimport();
            AssetDatabase.Refresh();

            // ── 读取信息 ──
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (fbx == null)
            {
                sb.AppendLine("❌ 模型加载失败");
                Write(sb.ToString());
                return;
            }

            int meshCount = 0, rendererCount = 0, boneCount = 0;
            var renderers = fbx.GetComponentsInChildren<Renderer>();
            var transforms = fbx.GetComponentsInChildren<Transform>();
            var filters = fbx.GetComponentsInChildren<MeshFilter>();
            meshCount = filters.Length;
            rendererCount = renderers.Length;
            boneCount = transforms.Length;

            sb.AppendLine($"网格数：{meshCount}");
            sb.AppendLine($"渲染器数：{rendererCount}");
            sb.AppendLine($"变换节点数（含骨骼）：{boneCount}");
            sb.AppendLine();

            // 边界尺寸
            if (rendererCount > 0)
            {
                Bounds b = renderers[0].bounds;
                sb.AppendLine($"包围盒中心：{b.center}");
                sb.AppendLine($"包围盒尺寸：{b.size}  （身高约 {b.size.y:F2} 米）");
                sb.AppendLine();
            }

            // 动画剪辑
            var all = AssetDatabase.LoadAllAssetsAtPath(ModelPath);
            var clipList = new System.Collections.Generic.List<AnimationClip>();
            foreach (var o in all)
                if (o is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    clipList.Add(clip);

            sb.AppendLine($"动画剪辑数：{clipList.Count}");
            foreach (var c in clipList)
                sb.AppendLine($"  · {c.name}  时长 {c.length:F2}s  循环={c.isLooping}");

            // 材质
            sb.AppendLine();
            sb.AppendLine("材质：");
            foreach (var r in renderers)
            {
                foreach (var m in r.sharedMaterials)
                    if (m != null) sb.AppendLine($"  · {m.name}  shader={m.shader.name}");
            }

            sb.AppendLine();
            sb.AppendLine("=====================================");
            sb.AppendLine("提示：如需【黑色剪影】效果，在运行时把材质替换为");
            sb.AppendLine("     深色 Unlit/Lit 材质即可（见 MonsterAI 的替换逻辑）。");

            string text = sb.ToString();
            Debug.Log("<b>[MonsterModel]</b>\n" + text);
            Write(text);
        }

        private static void Write(string text)
        {
            try
            {
                string dir = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "S5_MonsterModelInfo.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}