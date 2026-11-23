using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    public static class NorthWallHoleCheck
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("══════ G7 北墙 (z=25) 门洞检测 ══════");
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter4_Graybox.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();

            sb.AppendLine();
            sb.AppendLine("沿 x 轴扫描 z=25 处，y=1.0（玩家膝盖高度）：");
            sb.AppendLine($"{"x",-8} {"是否有墙",-12} {"撞到的物体"}");
            sb.AppendLine(new string('-', 50));
            for (float x = -10f; x <= 10f; x += 1f)
            {
                bool hit = Physics.CheckSphere(new Vector3(x, 1.0f, 25f), 0.3f, ~0, QueryTriggerInteraction.Ignore);
                string name = "";
                if (hit)
                {
                    if (Physics.Raycast(new Vector3(x, 1.0f, 24f), Vector3.forward, out var h, 3f, ~0, QueryTriggerInteraction.Ignore))
                        name = h.collider.gameObject.name;
                }
                sb.AppendLine($"{x,-8:F1} {(hit ? "■ 有墙" : "□ 空"),-12} {name}");
            }

            sb.AppendLine();
            sb.AppendLine("北墙对象详情：");
            var nw = GameObject.Find("North_G7_Full");
            if (nw != null)
            {
                var c = nw.GetComponent<BoxCollider>();
                if (c != null)
                    sb.AppendLine($"  包围盒 x[{c.bounds.min.x:F1},{c.bounds.max.x:F1}] " +
                                  $"y[{c.bounds.min.y:F1},{c.bounds.max.y:F1}] " +
                                  $"z[{c.bounds.min.z:F1},{c.bounds.max.z:F1}]");
                sb.AppendLine($"  是否有子物体（可能是门洞分段）：{nw.transform.childCount}");
            }

            sb.AppendLine();
            sb.AppendLine("门后 (z>25) 是否有地板/房间：");
            bool floorAfterGate = Physics.Raycast(new Vector3(0, 1f, 26f), Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore);
            sb.AppendLine($"  (0,26) 下方有地板 = {(floorAfterGate ? "✅" : "❌ 没有地板（门外是虚空）")}");

            for (float z = 25f; z <= 40f; z += 2f)
            {
                bool f = Physics.Raycast(new Vector3(0, 1f, z), Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore);
                sb.AppendLine($"    z={z,5:F1}  地板={(f ? "有" : "无")}");
            }

            sb.AppendLine();
            sb.AppendLine("结局触发区 z[37,41] 区域：");
            for (float z = 36f; z <= 42f; z += 2f)
            {
                bool f = Physics.Raycast(new Vector3(0, 1f, z), Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore);
                bool wall = Physics.CheckSphere(new Vector3(0, 1f, z), 0.3f, ~0, QueryTriggerInteraction.Ignore);
                sb.AppendLine($"    z={z,5:F1}  地板={(f ? "有" : "无")}  空间={(wall ? "有障碍" : "空")}");
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\NorthWallHoleCheck.txt", text, new UTF8Encoding(false));
        }
    }
}