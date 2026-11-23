using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class AltarSceneCheck {
        public static void Run() {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter3_Graybox.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();

            sb.AppendLine("══════ 供奉台及其周边对象 ══════");
            var altar = GameObject.Find("供奉台");
            if (altar == null) { sb.AppendLine("找不到 供奉台"); }
            else {
                sb.AppendLine($"  供奉台 @ {altar.transform.position}");
                var comp = altar.GetComponent<AbyssEcho.Puzzle.OfferingAltar>();
                if (comp != null) {
                    sb.AppendLine($"    itemForEndingA = \"{comp.itemForEndingA}\"");
                    sb.AppendLine($"    itemForEndingC = \"{comp.itemForEndingC}\"");
                    var so = new SerializedObject(comp);
                    var mk = so.FindProperty("offeringMarker");
                    sb.AppendLine($"    offeringMarker = {(mk != null && mk.objectReferenceValue != null ? mk.objectReferenceValue.name : "空")}");
                    var dr = so.FindProperty("doorToUnlock");
                    sb.AppendLine($"    doorToUnlock = {(dr != null && dr.objectReferenceValue != null ? dr.objectReferenceValue.name : "空")}");
                    var lt = so.FindProperty("lightsToEnable");
                    sb.AppendLine($"    lightsToEnable = {(lt != null ? lt.arraySize : 0)} 盏");
                }
            }

            sb.AppendLine();
            sb.AppendLine("══════ F9 祭室 (x[10,18] z[26,34]) 内所有对象 ══════");
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)) {
                Vector3 p = t.position;
                if (p.x >= 9f && p.x <= 19f && p.z >= 25f && p.z <= 35f) {
                    var c = t.GetComponent<Collider>();
                    string ci = "";
                    if (c is BoxCollider bc) { var b = bc.bounds; ci = $" 碰撞 {b.size.x:F1}x{b.size.y:F1}x{b.size.z:F1}"; }
                    sb.AppendLine($"  {t.name,-28} @ ({p.x:F1},{p.y:F1},{p.z:F1}){ci}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("══════ 第三章场景里的灯（F9/F7 附近）══════");
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) {
                Vector3 p = l.transform.position;
                if ((p.x >= 8f && p.x <= 20f && p.z >= 24f && p.z <= 36f) ||
                    (p.x >= -10f && p.x <= 10f && p.z >= 52f && p.z <= 68f))
                    sb.AppendLine($"  {l.gameObject.name,-28} @ ({p.x:F1},{p.y:F1},{p.z:F1}) int={l.intensity:F1} enabled={l.enabled}");
            }

            sb.AppendLine();
            sb.AppendLine("══════ F7 镜沼 (x[-8,8] z[53,67]) 内对象 ══════");
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)) {
                Vector3 p = t.position;
                if (p.x >= -9f && p.x <= 9f && p.z >= 52f && p.z <= 68f)
                    sb.AppendLine($"  {t.name,-28} @ ({p.x:F1},{p.y:F1},{p.z:F1})");
            }

            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\AltarSceneCheck.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}