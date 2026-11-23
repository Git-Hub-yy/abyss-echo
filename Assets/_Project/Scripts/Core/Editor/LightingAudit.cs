using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace AbyssEcho.Core.EditorTools
{
    public static class LightingAudit
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== 各章照明来源审计 ===");

            string[] scenes = {
                "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity",
                "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity",
            };

            foreach (var p in scenes)
            {
                EditorSceneManager.OpenScene(p, OpenSceneMode.Single);
                sb.AppendLine();
                sb.AppendLine($"### {Path.GetFileNameWithoutExtension(p)}");

                sb.AppendLine($"  RenderSettings.ambientMode = {RenderSettings.ambientMode}");
                sb.AppendLine($"  RenderSettings.ambientLight = {RenderSettings.ambientLight}");
                sb.AppendLine($"  RenderSettings.ambientIntensity = {RenderSettings.ambientIntensity}");
                sb.AppendLine($"  RenderSettings.ambientSkyColor = {RenderSettings.ambientSkyColor}");
                sb.AppendLine($"  RenderSettings.ambientEquatorColor = {RenderSettings.ambientEquatorColor}");
                sb.AppendLine($"  RenderSettings.ambientGroundColor = {RenderSettings.ambientGroundColor}");
                sb.AppendLine($"  RenderSettings.skybox = {(RenderSettings.skybox != null ? RenderSettings.skybox.name : "NULL")}");
                sb.AppendLine($"  fog = {RenderSettings.fog}  color={RenderSettings.fogColor}  mode={RenderSettings.fogMode}");

                foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                {
                    if (l.type == LightType.Directional)
                    {
                        sb.AppendLine($"  ★ Directional: {l.gameObject.name}");
                        sb.AppendLine($"      intensity={l.intensity} color={l.color} shadows={l.shadows}");
                        sb.AppendLine($"      rotation={l.transform.eulerAngles}");
                        sb.AppendLine($"      enabled={l.enabled} active={l.gameObject.activeInHierarchy}");
                    }
                }

                // 材质检查
                var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
                sb.AppendLine($"  Renderer 数量 = {renderers.Length}");
                int withMat = 0;
                foreach (var r in renderers) if (r.sharedMaterial != null) withMat++;
                sb.AppendLine($"  有材质的 Renderer = {withMat}");
                if (renderers.Length > 0 && renderers[0].sharedMaterial != null)
                {
                    var m = renderers[0].sharedMaterial;
                    sb.AppendLine($"  样例材质 = {m.name}  shader={m.shader.name}");
                }
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports/LightingAudit.txt")), text, new UTF8Encoding(false));
        }
    }
}