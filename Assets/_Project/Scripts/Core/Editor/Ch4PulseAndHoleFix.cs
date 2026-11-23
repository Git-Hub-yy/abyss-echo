using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S72b：**回退心瓣材质** + **封堵虚空**。
    ///
    /// ══════════════════════ 问题 1：心跳立方不亮 ══════════════════════
    ///
    ///   PulseSyncPuzzle.ApplyColor() 用 MaterialPropertyBlock 设置 _BaseColor：
    ///       暗红 (0.32,0.14,0.14) ←→ 亮红 (1.0,0.35,0.30)
    ///
    ///   S69 材质升级把 HeartValve 的材质从 M_HeartValve（纯色）
    ///   换成了 M_Org_SlimeBio（有黑绿贴图，亮度 0.077）。
    ///
    ///   结果：红色 × 黑色贴图 ≈ 全黑，搏动完全看不出来。
    ///
    ///   修复：把 HeartValve 换回 M_HeartValve（纯色，BaseColor 正好 = idleColor）。
    ///
    /// ══════════════════════ 问题 2：墙上还有黑洞 ══════════════════════
    ///
    ///   玩家截图显示 G5 北侧有大面积虚空。
    ///   上次补的 Ch4_CorePath_Floor 只覆盖 Z[6.90,9.20]，
    ///   但 G5 与其北侧之间的缺口可能更大。
    ///
    ///   本工具先全量扫描 G5 周围所有没有地面的区域，再决定补多大。
    ///
    /// 只读扫描 + 可选修复。
    /// </summary>
    public static class Ch4PulseAndHoleFix
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity";

        [MenuItem("ABYSS ECHO/修复 v9.6/诊断：扫描 G5 周围虚空")]
        public static void ScanOnly() => Run(fix: false);

        [MenuItem("ABYSS ECHO/修复 v9.6/执行：恢复心瓣材质 + 封堵虚空")]
        public static void Fix() => Run(fix: true);

        public static void Run(bool fix)
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════");
            sb.AppendLine(fix ? "  修复：心瓣材质 + 虚空封堵" : "  扫描：G5 周围虚空");
            sb.AppendLine("═══════════════════════════════════════════════════");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();
            bool changed = false;

            // ══════════════════ 1. 恢复心瓣材质 ══════════════════
            var valve = GameObject.Find("HeartValve");
            if (valve != null)
            {
                var mr = valve.GetComponent<MeshRenderer>();
                var oldMat = AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/_Project/Art/Materials/Ch4/M_HeartValve.mat");

                sb.AppendLine();
                sb.AppendLine("──── 心瓣 HeartValve ────");
                sb.AppendLine($"  当前材质 = {(mr != null && mr.sharedMaterial != null ? mr.sharedMaterial.name : "NULL")}");

                if (oldMat == null)
                {
                    sb.AppendLine("  ⚠️ M_HeartValve.mat 不存在，无法恢复");
                }
                else
                {
                    sb.AppendLine($"  目标材质 = {oldMat.name}  BaseColor={oldMat.GetColor("_BaseColor")} " +
                                  $"贴图={(oldMat.GetTexture("_BaseMap") != null ? "有" : "无")}");

                    if (fix && mr != null)
                    {
                        mr.sharedMaterial = oldMat;
                        EditorUtility.SetDirty(mr);
                        changed = true;
                        sb.AppendLine("  ✅ 已恢复为纯色材质（MaterialPropertyBlock 的搏动颜色可正常显示）");
                    }
                    else if (!fix)
                    {
                        sb.AppendLine("  [待修复] 将换回纯色材质");
                    }
                }
            }
            else sb.AppendLine("  ⚠️ 找不到 HeartValve");

            // ══════════════════ 2. 扫描 G5 周围虚空 ══════════════════
            sb.AppendLine();
            sb.AppendLine("──── 扫描 G5 周围虚空（0.5 米网格）────");
            sb.AppendLine("  范围 X[-26,-6] Z[4,12]");
            sb.AppendLine();

            // 用字符图展示
            var holes = new System.Collections.Generic.List<(float x, float z)>();

            sb.AppendLine("     " + BuildHeader(-26f, -6f, 1f));
            for (float z = 12f; z >= 4f; z -= 0.5f)
            {
                var row = new StringBuilder();
                row.Append($"{z,5:F1} ");
                for (float x = -26f; x <= -6f; x += 0.5f)
                {
                    bool hasFloor = ProbeFloor(x, z);
                    row.Append(hasFloor ? '█' : '·');
                    if (!hasFloor) holes.Add((x, z));
                }
                sb.AppendLine(row.ToString());
            }

            sb.AppendLine();
            sb.AppendLine("  图例：█=有地面  ·=虚空");
            sb.AppendLine($"  虚空点 {holes.Count} 个");

            // 归纳虚空区域（连通块）
            sb.AppendLine();
            sb.AppendLine("──── 虚空区域归纳 ────");
            if (holes.Count > 0)
            {
                // 按 z 分段
                float curZMin = holes[0].z, curZMax = holes[0].z;
                float curXMin = holes[0].x, curXMax = holes[0].x;
                var segs = new System.Collections.Generic.List<string>();

                // 简单做法：按 z 行分组输出
                var byZ = new System.Collections.Generic.Dictionary<float, System.Collections.Generic.List<float>>();
                foreach (var h in holes)
                {
                    if (!byZ.ContainsKey(h.z)) byZ[h.z] = new System.Collections.Generic.List<float>();
                    byZ[h.z].Add(h.x);
                }

                var zs = new System.Collections.Generic.List<float>(byZ.Keys);
                zs.Sort();
                foreach (var z in zs)
                {
                    var xs = byZ[z];
                    xs.Sort();
                    sb.AppendLine($"  z={z,5:F1}: x 从 {xs[0],6:F1} 到 {xs[xs.Count - 1],6:F1}  （{xs.Count} 个点）");
                }
            }

            // ══════════════════ 3. 封堵 ══════════════════
            if (fix && holes.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("──── 执行封堵 ────");

                // 计算虚空包围盒
                float xMin = 9999, xMax = -9999, zMin = 9999, zMax = -9999;
                foreach (var h in holes)
                {
                    xMin = Mathf.Min(xMin, h.x); xMax = Mathf.Max(xMax, h.x);
                    zMin = Mathf.Min(zMin, h.z); zMax = Mathf.Max(zMax, h.z);
                }
                // 补 0.25 余量
                xMin -= 0.25f; xMax += 0.25f;
                zMin -= 0.25f; zMax += 0.25f;

                sb.AppendLine($"  虚空包围盒 X[{xMin:F2},{xMax:F2}] Z[{zMin:F2},{zMax:F2}]");

                // 只补 G5 北侧走廊能到达的区域，避免补到墙外
                // G5 北边界 z=5.15，连廊 z 到 9.5
                float fillXMin = Mathf.Max(xMin, -20.5f);
                float fillXMax = Mathf.Min(xMax, -13.0f);
                float fillZMin = Mathf.Max(zMin, 5.0f);
                float fillZMax = Mathf.Min(zMax, 9.5f);

                if (fillXMax > fillXMin + 0.1f && fillZMax > fillZMin + 0.1f)
                {
                    sb.AppendLine($"  补齐区域 X[{fillXMin:F2},{fillXMax:F2}] Z[{fillZMin:F2},{fillZMax:F2}]");
                    changed |= MakeBox("Ch4_CorePath_Floor_V2", fillXMin, fillXMax, fillZMin, fillZMax,
                                       0.00f, 0.50f, "Assets/_Project/Art/Materials/Level/M_Wall_Metal.mat", sb);
                    changed |= MakeBox("Ch4_CorePath_Ceiling_V2", fillXMin, fillXMax, fillZMin, fillZMax,
                                       3.70f, 0.30f, "Assets/_Project/Art/Materials/Level/M_Wall_Metal.mat", sb);
                }
                else
                {
                    sb.AppendLine("  ⚠️ 虚空区域超出预期范围，跳过自动补齐");
                }
            }

            if (fix && changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                bool ok = EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                sb.AppendLine();
                sb.AppendLine(ok ? "场景已保存" : "❌ 保存失败");
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch4PulseAndHole.txt",
                              text, new UTF8Encoding(false));
        }

        private static string BuildHeader(float xMin, float xMax, float step)
        {
            var sb = new StringBuilder();
            for (float x = xMin; x <= xMax; x += step * 4)
                sb.Append($"{x,5:F0}");
            return sb.ToString();
        }

        private static bool ProbeFloor(float x, float z)
        {
            var hits = Physics.RaycastAll(new Vector3(x, 6f, z), Vector3.down, 20f,
                                          ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h.collider == null) continue;
                var b = h.collider.bounds;
                if (x < b.min.x - 0.01f || x > b.max.x + 0.01f) continue;
                if (z < b.min.z - 0.01f || z > b.max.z + 0.01f) continue;
                if (b.max.y > 1.0f || b.max.y < -1.0f) continue;   // 只看地面
                return true;
            }
            return false;
        }

        private static bool MakeBox(string name, float xMin, float xMax, float zMin, float zMax,
                                     float topY, float thick, string matPath, StringBuilder sb)
        {
            if (GameObject.Find(name) != null) { sb.AppendLine($"  = {name} 已存在"); return false; }

            var go = new GameObject(name);
            float cx = (xMin + xMax) * 0.5f, cz = (zMin + zMax) * 0.5f;
            go.transform.position = new Vector3(cx, topY - thick * 0.5f, cz);
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = new Vector3(xMax - xMin, thick, zMax - zMin);

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mf.sharedMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);

            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat != null) mr.sharedMaterial = mat;

            var col = go.AddComponent<BoxCollider>();
            col.size = Vector3.one;

            sb.AppendLine($"  + {name}  X[{xMin:F2},{xMax:F2}] Z[{zMin:F2},{zMax:F2}] 材质={mat?.name ?? "NULL"}");
            return true;
        }
    }
}