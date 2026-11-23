using System.IO;
using System.Text;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class G5PathFind {
        public static void Run() {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter4_Graybox.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();
            sb.AppendLine("══════ G5 → G7 可通行区域网格扫描 ══════");
            sb.AppendLine("玩家胶囊半径 0.35，逐点检测");
            sb.AppendLine();
            sb.AppendLine("     " + string.Join("", Enumerable.Range(0, 26).Select(i => (i % 10).ToString())));
            sb.AppendLine("     X: -20 ← → -10（每 0.4 米一格）");

            for (float z = 4f; z <= 10f; z += 0.4f) {
                var row = new StringBuilder();
                row.Append($"z={z,5:F1} ");
                for (float x = -20f; x <= -9.9f; x += 0.4f) {
                    bool blocked = Physics.CheckCapsule(
                        new Vector3(x, 0.35f, z), new Vector3(x, 1.45f, z),
                        0.35f, ~0, QueryTriggerInteraction.Ignore);
                    bool floor = Physics.Raycast(new Vector3(x, 2f, z), Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore);
                    if (!floor) row.Append(" ");
                    else row.Append(blocked ? "■" : "·");
                }
                sb.AppendLine(row.ToString());
            }
            sb.AppendLine();
            sb.AppendLine("图例: · = 可站立且无阻挡    ■ = 有碰撞    (空格) = 无地板");

            Debug.Log(sb.ToString());
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\G5PathFind.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }
}