using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class SocketInteractCheck {
        public static void Run() {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter3_Graybox.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();

            sb.AppendLine("══════ 长明灯座的可交互性 ══════");
            foreach (var n in new[] {"长明灯座1","长明灯座2"}) {
                var go = GameObject.Find(n);
                if (go == null) { sb.AppendLine($"  {n} 不存在"); continue; }
                sb.AppendLine($"  {n} @ {go.transform.position}");
                sb.AppendLine($"    layer = {LayerMask.LayerToName(go.layer)} ({go.layer})");
                sb.AppendLine($"    active = {go.activeInHierarchy}");
                sb.AppendLine($"    组件：");
                foreach (var c in go.GetComponents<Component>()) {
                    sb.AppendLine($"      {(c == null ? "Missing Script" : c.GetType().Name)}");
                }
                var col = go.GetComponent<Collider>();
                if (col != null) {
                    var b = col.bounds;
                    sb.AppendLine($"    碰撞体 {col.GetType().Name}: x[{b.min.x:F2},{b.max.x:F2}] y[{b.min.y:F2},{b.max.y:F2}] z[{b.min.z:F2},{b.max.z:F2}] isTrigger={col.isTrigger}");
                } else sb.AppendLine($"    ❌ 无 Collider");
                var inter = go.GetComponent<AbyssEcho.Interaction.InteractableBase>();
                sb.AppendLine($"    InteractableBase = {(inter != null ? inter.GetType().Name : "❌ 无")}");

                // 子物体
                sb.AppendLine($"    子物体：");
                foreach (Transform c in go.transform) {
                    var cc = c.GetComponent<Collider>();
                    sb.AppendLine($"      {c.name} active={c.gameObject.activeSelf} 碰撞={(cc!=null?cc.GetType().Name:"无")}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("══════ 对比：供奉台的可交互性 ══════");
            var altar = GameObject.Find("供奉台");
            if (altar != null) {
                sb.AppendLine($"  供奉台 @ {altar.transform.position}");
                sb.AppendLine($"    layer = {LayerMask.LayerToName(altar.layer)} ({altar.layer})");
                foreach (var c in altar.GetComponents<Component>()) {
                    sb.AppendLine($"      {(c == null ? "Missing Script" : c.GetType().Name)}");
                }
                var col = altar.GetComponent<Collider>();
                if (col != null) { var b = col.bounds; sb.AppendLine($"    碰撞体: x[{b.min.x:F2},{b.max.x:F2}] y[{b.min.y:F2},{b.max.y:F2}] z[{b.min.z:F2},{b.max.z:F2}]"); }
            }

            sb.AppendLine();
            sb.AppendLine("══════ 对比：灯泡拾取物 ══════");
            var bulb = GameObject.Find("Pickup_Bulb");
            if (bulb != null) {
                sb.AppendLine($"  Pickup_Bulb @ {bulb.transform.position}");
                sb.AppendLine($"    layer = {LayerMask.LayerToName(bulb.layer)} ({bulb.layer})");
                foreach (var c in bulb.GetComponents<Component>())
                    sb.AppendLine($"      {(c == null ? "Missing Script" : c.GetType().Name)}");
                var col = bulb.GetComponent<Collider>();
                if (col != null) { var b = col.bounds; sb.AppendLine($"    碰撞体: {col.GetType().Name} x[{b.min.x:F2},{b.max.x:F2}] y[{b.min.y:F2},{b.max.y:F2}] z[{b.min.z:F2},{b.max.z:F2}]"); }
            }

            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\SocketInteractCheck.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}