using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace AbyssEcho.Core.EditorTools
{
    public static class VolumeInspect
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Volume / Profile 详细检查 ===");

            string[] scenes = {
                "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity",
                "Assets/_Project/Scenes/Level_Chapter2_Graybox.unity",
                "Assets/_Project/Scenes/Level_Chapter3_Graybox.unity",
                "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity",
                "Assets/_Project/Scenes/Level_Chapter5_Graybox.unity",
            };

            foreach (var p in scenes)
            {
                EditorSceneManager.OpenScene(p, OpenSceneMode.Single);
                sb.AppendLine();
                sb.AppendLine($"### {Path.GetFileNameWithoutExtension(p)}");

                var vols = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
                sb.AppendLine($"  Volume = {vols.Length}");

                foreach (var v in vols)
                {
                    sb.AppendLine($"    {v.gameObject.name}");
                    sb.AppendLine($"      isGlobal={v.isGlobal} weight={v.weight} priority={v.priority}");
                    sb.AppendLine($"      sharedProfile={(v.sharedProfile != null ? v.sharedProfile.name : "NULL")}");
                    sb.AppendLine($"      profile={(v.profile != null ? v.profile.name : "NULL")}");
                    sb.AppendLine($"      enabled={v.enabled}");
                    sb.AppendLine($"      位置={v.transform.position} localPos={v.transform.localPosition}");
                    sb.AppendLine($"      父级={(v.transform.parent != null ? v.transform.parent.name : "无")}");

                    if (v.sharedProfile != null)
                    {
                        var prof = v.sharedProfile;
                        sb.AppendLine($"      --- Profile 内容 ---");
                        foreach (var c in prof.components)
                        {
                            if (c == null) { sb.AppendLine("        <NULL 组件>"); continue; }
                            int active = 0;
                            try
                            {
                                foreach (var pi in c.GetType().GetProperties())
                                {
                                    if (pi.Name == "active" && pi.PropertyType == typeof(bool))
                                    { if ((bool)pi.GetValue(c)) active = 1; }
                                }
                            }
                            catch { }
                            sb.AppendLine($"        {c.GetType().Name} active={active}");
                        }
                    }

                    // FearVisuals 是否在同一场景
                    var fvs = Object.FindObjectsByType<AbyssEcho.Horror.FearVisuals>(FindObjectsSortMode.None);
                    sb.AppendLine($"  FearVisuals = {fvs.Length}");
                    foreach (var fv in fvs)
                    {
                        sb.AppendLine($"    @ {GetPath(fv.transform)}");
                        sb.AppendLine($"      fear={(fv.fear != null ? fv.fear.gameObject.name : "NULL")}");
                        sb.AppendLine($"      targetCamera={(fv.targetCamera != null ? fv.targetCamera.gameObject.name : "NULL")}");
                        sb.AppendLine($"      vignetteCalm={fv.vignetteCalm} panic={fv.vignettePanic}");
                        sb.AppendLine($"      aberrationCalm={fv.aberrationCalm} panic={fv.aberrationPanic}");
                        sb.AppendLine($"      grainCalm={fv.grainCalm} panic={fv.grainPanic}");
                        sb.AppendLine($"      maxShakeAngle={fv.maxShakeAngle}");
                        sb.AppendLine($"      breathFovAmount={fv.breathFovAmount} breathRate={fv.breathRate}");
                    }

                    // 相机配置
                    var cam = Object.FindFirstObjectByType<Camera>();
                    if (cam != null)
                    {
                        var extra = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                        sb.AppendLine($"  相机 {cam.gameObject.name}: fov={cam.fieldOfView}");
                        sb.AppendLine($"    renderPostProcessing={(extra != null ? extra.renderPostProcessing.ToString() : "无 extra")}");
                        sb.AppendLine($"    renderType={(extra != null ? extra.renderType.ToString() : "?")}");
                        sb.AppendLine($"    volumeLayerMask={(extra != null ? extra.volumeLayerMask.value.ToString() : "?")}");
                        sb.AppendLine($"    antialiasing={(extra != null ? extra.antialiasing.ToString() : "?")}");
                    }
                }
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports/VolumeInspect.txt")), text, new UTF8Encoding(false));
        }

        private static string GetPath(Transform t)
        {
            string p2 = t.name;
            var cur = t.parent;
            int g = 0;
            while (cur != null && g++ < 20) { p2 = cur.name + "/" + p2; cur = cur.parent; }
            return p2;
        }
    }
}