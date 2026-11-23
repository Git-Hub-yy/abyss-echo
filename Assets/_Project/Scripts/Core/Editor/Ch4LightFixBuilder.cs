using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S60：给第四章重新布置灯光。
    ///
    /// ══════════════════ 问题 ══════════════════
    ///
    ///   RoomLightBuilder 的灯具表里原来一个 G 区都没有，
    ///   第四章调用它时 77 盏灯全部堆在原点 (0,0,0)，
    ///   G2/G3/G5/G7 等区域完全无光。
    ///
    ///   已修复 RoomLightBuilder（新增 G 区灯具 + 跳过无效房间）。
    ///   本 Builder 在第四章场景里执行重新布置。
    /// </summary>
    public static class Ch4LightFixBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity";

        [MenuItem("ABYSS ECHO/修复 v6.6/第四章 重新布置灯光")]
        public static void Build() => Run();

        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Log("已打开场景");

            // 强制章节为 Chapter4（RoomLightBuilder 读当前章节）
            var prev = AbyssEcho.LevelGen.ChapterLayout.Current;
            AbyssEcho.LevelGen.ChapterLayout.Current =
                AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter4;

            int before = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length;
            Log($"重布置前 Light 数量 = {before}");

            RoomLightBuilder.Build();

            int after = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length;
            Log($"重布置后 Light 数量 = {after}");

            AbyssEcho.LevelGen.ChapterLayout.Current = prev;

            // 保存
            EditorSceneManager.MarkSceneDirty(scene);
            bool ok = EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log(ok ? "场景已保存" : "❌ 保存失败");

            Verify();
        }

        private static void Verify()
        {
            Log("---- 验证：重新布置后的光源分布 ----");
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            Log($"总灯数 = {lights.Length}");

            var regions = new (string name, float x0, float x1, float z0, float z1)[]
            {
                ("G1 入口腔",     -4, 4,   -4, 4),
                ("G1_G2_Link",   -1.3f, 1.3f, -10, -4),
                ("G2 环形廊",    -12, 12, -14, -10),
                ("G2_G3_Link",   -14, -12, -12.7f, -11.3f),
                ("G3 消化囊",    -24, -14, -17, -7),
                ("G3_G5_Link",   -20.1f, -17.9f, -7, -5),
                ("G5 组织层",    -25, -13, -5, 5),
                ("G5_G7_Link",   -13, -10, 5, 9.2f),
                ("G7 核心腔",    -10, 10, 7, 25),
                ("G6 神经束",    13, 25, -5, 5),
                ("G4 膜层",      14, 24, -17, -7),
            };

            int empty = 0;
            foreach (var r in regions)
            {
                int cnt = 0;
                var detail = new StringBuilder();
                foreach (var l in lights)
                {
                    Vector3 p = l.transform.position;
                    if (p.x >= r.x0 && p.x <= r.x1 && p.z >= r.z0 && p.z <= r.z1)
                    {
                        cnt++;
                        if (l.enabled && l.intensity > 0.1f) detail.Append($"[{p.x:F1},{p.y:F1},{p.z:F1}] ");
                    }
                }
                if (cnt == 0) empty++;
                Log($"  {r.name,-14} {cnt,2} 盏  {detail}");
            }

            Log($"无光区域数 = {empty}");

            // 原点附近还有多少灯（检测堆叠）
            int atOrigin = 0;
            foreach (var l in lights)
            {
                Vector3 p = l.transform.position;
                if (Mathf.Abs(p.x) < 0.01f && Mathf.Abs(p.z) < 0.01f) atOrigin++;
            }
            Log($"在原点 (0,0) 处的灯 = {atOrigin}（应远小于总数）");
        }

        private static void Log(string msg)
        {
            string line = $"[Ch4Light] {msg}";
            Debug.Log(line);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.AppendAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch4LightFix_Result.txt",
                                   line + "\n", new UTF8Encoding(false));
            }
            catch { }
        }
    }
}