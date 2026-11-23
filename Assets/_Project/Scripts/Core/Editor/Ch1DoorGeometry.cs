using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    public static class Ch1DoorGeometry
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter1_Graybox.unity", OpenSceneMode.Single);

            sb.AppendLine("=== 第一章门几何 ===");

            foreach (var d in Object.FindObjectsByType<AbyssEcho.Interaction.DoorInteractable>(FindObjectsSortMode.None))
            {
                sb.AppendLine();
                sb.AppendLine($"--- {d.name} ---");
                sb.AppendLine($"  位置 = {d.transform.position}");
                sb.AppendLine($"  局部位置 = {d.transform.localPosition}");
                sb.AppendLine($"  缩放 = {d.transform.localScale}  lossyScale = {d.transform.lossyScale}");
                sb.AppendLine($"  slideDistance = {d.slideDistance}");
                sb.AppendLine($"  autoSlideAxis = {d.autoSlideAxis}  slideAlongZ = {d.slideAlongZ}");
                sb.AppendLine($"  swingAngle = {d.swingAngle}");

                var col = d.GetComponent<Collider>();
                if (col != null)
                {
                    sb.AppendLine($"  Collider = {col.GetType().Name}");
                    sb.AppendLine($"    bounds.center = {col.bounds.center}");
                    sb.AppendLine($"    bounds.size   = {col.bounds.size}");
                    sb.AppendLine($"    isTrigger     = {col.isTrigger}");
                }

                var mr = d.GetComponent<MeshRenderer>();
                if (mr != null)
                    sb.AppendLine($"  MeshRenderer bounds = {mr.bounds.center} / {mr.bounds.size}");
            }

            // Room B1 与 A3 之间的墙
            sb.AppendLine();
            sb.AppendLine("=== 门附近的墙段 ===");
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t == null) continue;
                if (!t.gameObject.scene.IsValid()) continue;
                if (!t.name.Contains("B1") && !t.name.Contains("A3")) continue;
                var c = t.GetComponent<Collider>();
                if (c == null) continue;
                // 距离门 -2.15 附近
                if (Mathf.Abs(t.position.z - (-2.15f)) > 3f) continue;
                sb.AppendLine($"  {t.name}: pos={t.position} size={c.bounds.size}");
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports/Ch1DoorGeometry.txt")), text, new UTF8Encoding(false));
        }
    }
}