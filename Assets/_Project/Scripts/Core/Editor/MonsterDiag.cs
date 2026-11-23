using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    public static class MonsterDiag
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== MonsterAI / MimicStalker 依赖核实 ===");

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
                sb.AppendLine($"--- {Path.GetFileName(p)} ---");

                var monsters = Object.FindObjectsByType<AbyssEcho.Monster.MonsterAI>(FindObjectsSortMode.None);
                sb.AppendLine($"  MonsterAI: {monsters.Length}");
                foreach (var m in monsters)
                {
                    var so = new SerializedObject(m);
                    sb.AppendLine($"    {m.gameObject.name}");
                    foreach (var f in new[] { "player", "flashlight", "animator", "patrolPoints" })
                    {
                        var pr = so.FindProperty(f);
                        string val;
                        if (pr == null) val = "<无>";
                        else if (pr.propertyType == SerializedPropertyType.ObjectReference)
                            val = pr.objectReferenceValue != null ? pr.objectReferenceValue.name : "NULL";
                        else val = pr.ToString();
                        sb.AppendLine($"      {f} = {val}");
                    }
                    var nav = m.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    sb.AppendLine($"      NavMeshAgent = {(nav != null ? "✓ 有" : "❌ 无")}");
                    sb.AppendLine($"      RequireComponent 应保证有 agent");
                    sb.AppendLine($"      enabled={m.enabled} activeInHierarchy={m.gameObject.activeInHierarchy}");
                }

                var mimics = Object.FindObjectsByType<AbyssEcho.Horror.MimicStalker>(FindObjectsSortMode.None);
                sb.AppendLine($"  MimicStalker: {mimics.Length}");
                foreach (var mi in mimics)
                {
                    var so = new SerializedObject(mi);
                    sb.AppendLine($"    {mi.gameObject.name} enabled={mi.enabled} state={mi.StateName}");
                    var col = mi.GetComponent<Collider>();
                    sb.AppendLine($"      Collider = {(col != null ? col.GetType().Name : "❌ 无")}");
                }

                // NavMesh
                var navMeshData = UnityEngine.AI.NavMesh.CalculateTriangulation();
                sb.AppendLine($"  NavMesh 顶点数 = {(navMeshData.vertices != null ? navMeshData.vertices.Length : 0)}");

                var surfaces = Object.FindObjectsByType<Unity.AI.Navigation.NavMeshSurface>(FindObjectsSortMode.None);
                sb.AppendLine($"  NavMeshSurface = {surfaces.Length}");
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports/MonsterDiag.txt")), text, new UTF8Encoding(false));
        }
    }
}