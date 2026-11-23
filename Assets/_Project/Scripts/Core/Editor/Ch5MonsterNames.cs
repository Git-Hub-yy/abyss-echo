using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class Ch5MonsterNames {
        public static void Run() {
            var sb = new StringBuilder();
            sb.AppendLine("=== 第五章所有怪物相关对象 ===");
            sb.AppendLine();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter5_Graybox.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();

            sb.AppendLine("──── A. 含 Monster/Mimic/Stalker/怪物 的对象 ────");
            sb.AppendLine($"  {"对象名",-34} {"位置",-28} 组件");
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)) {
                string n = t.name;
                if (!n.Contains("Monster") && !n.Contains("Mimic") && !n.Contains("Stalker") &&
                    !n.Contains("怪物") && !n.Contains("Creep") && !n.Contains("Lurker")) continue;
                var comps = new List<string>();
                foreach (var c in t.gameObject.GetComponents<Component>()) {
                    if (c == null) { comps.Add("MISSING!"); continue; }
                    if (c is Transform) continue;
                    comps.Add(c.GetType().Name);
                }
                sb.AppendLine($"  {n,-34} {t.position.ToString(),-28} {string.Join(",", comps)}");
            }

            sb.AppendLine();
            sb.AppendLine("──── B. 带 MonsterActivationTrigger 的对象 ────");
            sb.AppendLine($"  {"对象名",-34} {"位置",-28} {"关联怪物",-24} {"触发范围",-24} oneShot");
            foreach (var tr in Object.FindObjectsByType<AbyssEcho.Monster.MonsterActivationTrigger>(FindObjectsSortMode.None)) {
                if (tr == null) continue;
                var f = typeof(AbyssEcho.Monster.MonsterActivationTrigger).GetField("monster");
                var mon = f?.GetValue(tr) as MonoBehaviour;
                var col = tr.GetComponent<BoxCollider>();
                string size = col != null ? $"{col.size.x:F1}x{col.size.y:F1}x{col.size.z:F1}" : "无Collider";
                var osF = typeof(AbyssEcho.Monster.MonsterActivationTrigger).GetField("oneShot");
                bool os = osF != null ? (bool)osF.GetValue(tr) : true;
                sb.AppendLine($"  {tr.gameObject.name,-34} {tr.transform.position.ToString(),-28} " +
                              $"{(mon != null ? mon.gameObject.name : "❌NULL"),-24} {size,-24} {os}");
            }

            sb.AppendLine();
            sb.AppendLine("──── C. 带 MonsterAI 的对象 ────");
            foreach (var ai in Object.FindObjectsByType<AbyssEcho.Monster.MonsterAI>(FindObjectsSortMode.None)) {
                if (ai == null) continue;
                sb.AppendLine($"  {ai.gameObject.name}  @ {ai.transform.position}");
                // 读关键参数
                var ty = typeof(AbyssEcho.Monster.MonsterAI);
                foreach (var fn in new[] { "sightRange", "catchDistance", "patrolSpeed", "chaseSpeed" }) {
                    var f = ty.GetField(fn);
                    if (f != null) sb.AppendLine($"      {fn} = {f.GetValue(ai)}");
                }
                var ppF = ty.GetField("patrolPoints");
                var pp = ppF?.GetValue(ai) as Transform[];
                sb.AppendLine($"      patrolPoints = {(pp == null ? "null" : pp.Length + " 个")}");
                if (pp != null) foreach (var p in pp)
                    if (p != null) sb.AppendLine($"          {p.name} @ {p.position}");
            }

            sb.AppendLine();
            sb.AppendLine("──── D. 带 MimicStalker 的对象 ────");
            foreach (var ms in Object.FindObjectsByType<AbyssEcho.Horror.MimicStalker>(FindObjectsSortMode.None)) {
                if (ms == null) continue;
                sb.AppendLine($"  {ms.gameObject.name}  @ {ms.transform.position}");
            }

            sb.AppendLine();
            sb.AppendLine("──── E. 场景里所有名字含 H3 的对象（看怪物是否在 H3）────");
            int cnt = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)) {
                var p = t.position;
                // H3 范围：X[-7,7] Z[24,34]
                if (p.x >= -8f && p.x <= 8f && p.z >= 23f && p.z <= 35f) {
                    var comps = new List<string>();
                    foreach (var c in t.gameObject.GetComponents<Component>()) {
                        if (c == null || c is Transform || c is MeshFilter || c is MeshRenderer) continue;
                        comps.Add(c.GetType().Name);
                    }
                    if (comps.Count == 0) continue;
                    sb.AppendLine($"  {t.name,-34} @ {p}  {string.Join(",", comps)}");
                    cnt++;
                }
            }
            sb.AppendLine($"  共 {cnt} 个带组件的对象在 H3 范围内");

            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch5MonsterNames.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}