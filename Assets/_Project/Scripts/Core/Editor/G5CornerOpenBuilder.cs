using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S59c：打通 G5 过渡区与连廊之间的最后一道墙。
    ///
    /// ══════════════════ 现状（网格扫描实测）══════════════════
    ///
    ///   过渡区地板 x[-20.2,-9.9] z[5,6.9] 已铺好，但：
    ///     x=-14.0 以西  → Transit_Wall_North（我加的，封北边，合理）
    ///     x=-13.6~-12.8 → West_G5_G7_Link_Ext（我加的，多余）
    ///                    + East_G5_Seg0（G5 东墙，过长）
    ///     x=-12.4~-10.4 → 通（连廊南段）
    ///
    /// ══════════════════ 为什么 East_G5_Seg0 过长 ══════════════════
    ///
    ///   G5 房间 z[-5,5]，东墙理应在 z=5 结束。
    ///   但 S44 在布局里写了 AddDoor(g5, East, 8f)，
    ///   门洞被切在 z[6.8,9.2]，墙段因此被生成到 z=6.8，
    ///   **比房间长了 1.8 米**，伸进了连廊应该占用的位置。
    ///
    ///   布局数据已在本批改成北墙开口（AddDoor(g5, North, 0) + 连廊向南延伸），
    ///   所以 East 门洞不再需要，这截多余的墙要收回房间边界。
    ///
    /// ══════════════════ 修改 ══════════════════
    ///
    ///   ① 删除 West_G5_G7_Link_Ext（多余）
    ///   ② East_G5_Seg0 从 z[-5, 6.8] 缩短到 z[-5, 5.0]
    ///
    ///   不动其他任何东西。
    /// </summary>
    public static class G5CornerOpenBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity";

        [MenuItem("ABYSS ECHO/修复 v6.5/第四章 G5 拐角打通")]
        public static void Run() => Fix();

        public static void Fix()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Log("已打开场景");
            Physics.SyncTransforms();

            int n = 0;

            // ① 删除多余的连廊西墙延长段
            var westExt = GameObject.Find("West_G5_G7_Link_Ext");
            if (westExt != null)
            {
                Log("  删除 West_G5_G7_Link_Ext（多余，挡住拐角）");
                Object.DestroyImmediate(westExt);
                n++;
            }
            else Log("  West_G5_G7_Link_Ext 不存在，跳过");

            // ② 把 East_G5_Seg0 收回房间边界 z=5
            var eastSeg = GameObject.Find("East_G5_Seg0");
            if (eastSeg != null)
            {
                var bc = eastSeg.GetComponent<BoxCollider>();
                var b = bc.bounds;
                Log($"  East_G5_Seg0 现状 x[{b.min.x:F2},{b.max.x:F2}] z[{b.min.z:F2},{b.max.z:F2}]");

                if (b.max.z > 5.05f)
                {
                    // 目标 z[-5, 5]
                    float newMinZ = b.min.z;
                    float newMaxZ = 5.0f;
                    float newSizeZ = newMaxZ - newMinZ;
                    float newCenterZ = (newMinZ + newMaxZ) * 0.5f;

                    eastSeg.transform.position = new Vector3(b.center.x, b.center.y, newCenterZ);
                    eastSeg.transform.localScale = new Vector3(b.size.x, b.size.y, newSizeZ);

                    Log($"  ✅ East_G5_Seg0 缩短到 z[{newMinZ:F2},{newMaxZ:F2}]");
                    n++;
                }
                else Log("  East_G5_Seg0 已在范围内，跳过");
            }

            // ③ Transit_Wall_North 保持不动（它封住过渡区北边是对的）

            if (n > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                bool ok = EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Log(ok ? $"场景已保存（{n} 项改动）" : "❌ 保存失败");
            }

            Verify();
        }

        private static void Verify()
        {
            Log("---- 验证 ----");
            Physics.SyncTransforms();

            // 网格扫描
            Log("可通行网格（x -20→-10 每0.4m，z 5→10 每0.4m）：");
            Log("     " + new string('0', 1) + "(每格0.4m)");
            int gaps = 0;
            for (float z = 5f; z <= 10f; z += 0.4f)
            {
                var row = new StringBuilder();
                row.Append($"z={z,5:F1} ");
                for (float x = -20f; x <= -9.9f; x += 0.4f)
                {
                    bool blocked = Physics.CheckCapsule(
                        new Vector3(x, 0.35f, z), new Vector3(x, 1.45f, z),
                        0.35f, ~0, QueryTriggerInteraction.Ignore);
                    bool floor = Physics.Raycast(new Vector3(x, 2f, z), Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore);
                    if (!floor) { row.Append(" "); gaps++; }
                    else row.Append(blocked ? "■" : "·");
                }
                Log("  " + row);
            }
            Log("图例: · 可走  ■ 障碍  (空格) 无地板");

            // 实际路径测试
            Log("实际路径（玩家胶囊逐段）：");
            var path = new[]
            {
                new Vector3(-19, 0, 3),
                new Vector3(-19, 0, 5),
                new Vector3(-18, 0, 5.8f),
                new Vector3(-16, 0, 6.2f),
                new Vector3(-14, 0, 6.4f),
                new Vector3(-12.5f, 0, 6.4f),
                new Vector3(-11.5f, 0, 7.5f),
                new Vector3(-11, 0, 8.5f),
                new Vector3(-9.5f, 0, 8.5f),
            };
            for (int i = 0; i < path.Length - 1; i++)
            {
                Vector3 a = path[i], b = path[i + 1];
                Vector3 dir = (b - a).normalized;
                dir.y = 0;
                float d = Vector3.Distance(new Vector3(a.x, 0, a.z), new Vector3(b.x, 0, b.z));
                bool blk = Physics.CapsuleCast(
                    new Vector3(a.x, 0.35f, a.z), new Vector3(a.x, 1.45f, a.z),
                    0.35f, dir, out var hit, d, ~0, QueryTriggerInteraction.Ignore);
                Log($"  ({a.x,6:F1},{a.z,4:F1}) → ({b.x,6:F1},{b.z,4:F1})  {d,4:F1}m  {(blk ? "❌ " + hit.collider.gameObject.name : "✅")}");
            }

            Log("完成");
        }

        private static void Log(string msg)
        {
            string line = $"[G5Corner] {msg}";
            Debug.Log(line);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.AppendAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\G5Corner_Result.txt",
                                   line + "\n", new UTF8Encoding(false));
            }
            catch { }
        }
    }
}