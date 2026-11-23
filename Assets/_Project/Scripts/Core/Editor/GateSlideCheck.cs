using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    public static class GateSlideCheck
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter4_Graybox.unity", OpenSceneMode.Single);

            sb.AppendLine("══════ 门滑动方向与门洞设计参数 ══════");

            var gate = GameObject.Find("Door_HeartGate");
            var door = gate.GetComponent<AbyssEcho.Interaction.DoorInteractable>();
            var so = new SerializedObject(door);

            sb.AppendLine();
            sb.AppendLine("DoorInteractable 全部序列化字段：");
            var iter = so.GetIterator();
            while (iter.NextVisible(true))
            {
                string v = "";
                switch (iter.propertyType)
                {
                    case SerializedPropertyType.Float: v = iter.floatValue.ToString("F3"); break;
                    case SerializedPropertyType.Integer: v = iter.intValue.ToString(); break;
                    case SerializedPropertyType.Boolean: v = iter.boolValue.ToString(); break;
                    case SerializedPropertyType.String: v = "\"" + iter.stringValue + "\""; break;
                    case SerializedPropertyType.Vector3: v = iter.vector3Value.ToString(); break;
                    case SerializedPropertyType.Enum: v = iter.enumValueIndex.ToString(); break;
                    case SerializedPropertyType.ObjectReference: v = iter.objectReferenceValue != null ? iter.objectReferenceValue.name : "NULL"; break;
                    default: continue;
                }
                if (iter.name == "m_Script") continue;
                sb.AppendLine($"  {iter.name} = {v}");
            }

            sb.AppendLine();
            sb.AppendLine("门滑动方向分析：");
            sb.AppendLine("  门世界位置 X = 0.00");
            sb.AppendLine("  门宽 = 3.00（x[-1.5, 1.5]）");
            sb.AppendLine("  slideDistance = 2.94");
            sb.AppendLine("  → 打开后门会移动到 X = ±2.94");

            sb.AppendLine();
            sb.AppendLine("G7 北墙细节：");
            var nw = GameObject.Find("North_G7_Full");
            if (nw != null)
            {
                var bc = nw.GetComponent<BoxCollider>();
                var mf = nw.GetComponent<MeshFilter>();
                var mr = nw.GetComponent<MeshRenderer>();
                sb.AppendLine($"  位置 = {nw.transform.position}");
                sb.AppendLine($"  Collider.size = {bc.size}  worldSize = {Vector3.Scale(bc.size, nw.transform.lossyScale)}");
                sb.AppendLine($"  Mesh = {(mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "无")}");
                sb.AppendLine($"  材质 = {(mr != null && mr.sharedMaterial != null ? mr.sharedMaterial.name : "无")}");
                sb.AppendLine($"  子物体数 = {nw.transform.childCount}");
                sb.AppendLine($"  父 = {(nw.transform.parent != null ? nw.transform.parent.name : "无")}");
            }

            sb.AppendLine();
            sb.AppendLine("G7 地板：");
            var fl = GameObject.Find("Floor_G7");
            if (fl != null)
            {
                var bc = fl.GetComponent<BoxCollider>();
                sb.AppendLine($"  位置 = {fl.transform.position}");
                sb.AppendLine($"  包围盒 = {bc.bounds.min} ~ {bc.bounds.max}");
                sb.AppendLine($"  顶面 Y = {bc.bounds.max.y:F3}");
                var mr = fl.GetComponent<MeshRenderer>();
                sb.AppendLine($"  材质 = {(mr != null && mr.sharedMaterial != null ? mr.sharedMaterial.name : "无")}");
            }

            sb.AppendLine();
            sb.AppendLine("G7 天花板：");
            var cl = GameObject.Find("Ceiling_G7");
            if (cl != null)
            {
                var bc = cl.GetComponent<BoxCollider>();
                sb.AppendLine($"  位置 = {cl.transform.position}");
                sb.AppendLine($"  底面 Y = {bc.bounds.min.y:F3}");
                var mr = cl.GetComponent<MeshRenderer>();
                sb.AppendLine($"  材质 = {(mr != null && mr.sharedMaterial != null ? mr.sharedMaterial.name : "无")}");
            }

            sb.AppendLine();
            sb.AppendLine("G7 东西墙（供通道墙参考）：");
            foreach (var n in new[] { "West_G7_SegEnd", "East_G7_SegEnd" })
            {
                var o = GameObject.Find(n);
                if (o != null)
                {
                    var bc = o.GetComponent<BoxCollider>();
                    var mr = o.GetComponent<MeshRenderer>();
                    sb.AppendLine($"  {n}: pos={o.transform.position} size={Vector3.Scale(bc.size, o.transform.lossyScale)} " +
                                  $"材质={(mr != null && mr.sharedMaterial != null ? mr.sharedMaterial.name : "无")}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("核心腔内的灯光（供照明参考）：");
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                Vector3 p = l.transform.position;
                if (p.x > -11 && p.x < 11 && p.z > 5 && p.z < 27)
                    sb.AppendLine($"  {l.gameObject.name} @ {p}  type={l.type} int={l.intensity} range={l.range} color={l.color}");
            }

            sb.AppendLine();
            sb.AppendLine("结局触发区：");
            var ending = Object.FindFirstObjectByType<AbyssEcho.Narrative.ChapterEndingSystem>();
            if (ending != null)
            {
                var tcF = typeof(AbyssEcho.Narrative.ChapterEndingSystem).GetField("triggerCenter");
                var tsF = typeof(AbyssEcho.Narrative.ChapterEndingSystem).GetField("triggerSize");
                sb.AppendLine($"  triggerCenter = {(Vector3)tcF.GetValue(ending)}");
                sb.AppendLine($"  triggerSize   = {(Vector3)tsF.GetValue(ending)}");
                sb.AppendLine($"  → 这是 OnGUI 检测，无 Collider，纯坐标判定");
            }

            sb.AppendLine();
            sb.AppendLine("场景中现有材质清单（找可复用的）：");
            var mats = new System.Collections.Generic.HashSet<string>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (r.sharedMaterial != null) mats.Add(r.sharedMaterial.name);
            foreach (var m in mats) sb.AppendLine($"  {m}");

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\GateSlideCheck.txt", text, new UTF8Encoding(false));
        }
    }
}