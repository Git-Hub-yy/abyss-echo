using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S56：第四章专项审计（**只读**，不修改任何文件）。
    ///
    /// 读取当前磁盘上 Ch4 场景的真实状态：
    ///   · 房间边界与通路
    ///   · Door_HeartGate 完整信息
    ///   · 所有怪物的分布
    ///   · 逐段可通行性（胶囊检测）
    ///
    /// ⚠️ 本脚本不调用 SaveScene / SetDirty，场景不会被写回。
    /// </summary>
    public static class Ch4DeepAudit
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════");
            sb.AppendLine("  第四章 专项审计（只读）");
            sb.AppendLine("  " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("═══════════════════════════════════════════════════");

            const string scenePath = "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity";
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // ══════ 一、房间边界（来自 ChapterFourLayout 逻辑数据）══════
            sb.AppendLine();
            sb.AppendLine("【一】房间逻辑边界");
            sb.AppendLine();
            var chap = AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter4;
            var rooms = AbyssEcho.LevelGen.ChapterLayout.GetRooms(chap);
            sb.AppendLine($"{"ID",-14} {"名称",-12} {"X 范围",-20} {"Z 范围",-20} {"高",-6}");
            sb.AppendLine(new string('-', 78));
            foreach (var r in rooms)
            {
                sb.AppendLine($"{r.Id,-14} {r.DisplayName,-12} " +
                              $"[{r.MinX,6:F1},{r.MaxX,6:F1}]      " +
                              $"[{r.MinZ,6:F1},{r.MaxZ,6:F1}]      {r.Height,4:F1}");
            }

            // ══════ 二、Door_HeartGate 完整信息 ══════
            sb.AppendLine();
            sb.AppendLine("【二】Door_HeartGate 完整信息");
            sb.AppendLine();
            var gate = GameObject.Find("Door_HeartGate");
            if (gate == null)
            {
                sb.AppendLine("  ❌❌ 场景中找不到名为 Door_HeartGate 的对象！");
                // 宽松搜索
                sb.AppendLine("  搜索所有名称含 Heart/Gate/Door 的对象：");
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                {
                    if (t.name.Contains("Heart") || t.name.Contains("Gate") || t.name == "Door")
                        sb.AppendLine($"    {GetPath(t)} @ {t.position}");
                }
            }
            else
            {
                DumpGateDetail(sb, gate);
            }

            // ══════ 三、心瓣与门的引用关系 ══════
            sb.AppendLine();
            sb.AppendLine("【三】HeartValve → Door_HeartGate 引用关系");
            sb.AppendLine();
            var valve = GameObject.Find("HeartValve");
            if (valve != null)
            {
                var pulse = valve.GetComponent<AbyssEcho.Puzzle.PulseSyncPuzzle>();
                if (pulse != null)
                {
                    var so = new SerializedObject(pulse);
                    var doorProp = so.FindProperty("doorToUnlock");
                    var refObj = doorProp?.objectReferenceValue as Component;
                    sb.AppendLine($"  心瓣位置 = {valve.transform.position}");
                    sb.AppendLine($"  PulseSyncPuzzle.doorToUnlock = " +
                                  (refObj != null ? $"✅ {GetPath(refObj.transform)}" : "❌ 空引用"));
                    if (refObj != null && gate != null)
                    {
                        bool same = refObj.gameObject == gate;
                        sb.AppendLine($"  引用的门是否就是 Door_HeartGate = {(same ? "✅ 是" : "❌ 不是同一个对象")}");
                    }

                    var lightsProp = so.FindProperty("lightsToEnable");
                    int lc = lightsProp != null ? lightsProp.arraySize : 0;
                    sb.AppendLine($"  lightsToEnable 数量 = {lc}");

                    var intervalProp = so.FindProperty("beatInterval");
                    var windowProp = so.FindProperty("syncWindow");
                    var requiredProp = so.FindProperty("requiredSyncs");
                    sb.AppendLine($"  beatInterval={intervalProp?.floatValue}  " +
                                  $"syncWindow={windowProp?.floatValue}  requiredSyncs={requiredProp?.intValue}");
                }
            }

            // ══════ 四、G7 区域内的所有对象 ══════
            sb.AppendLine();
            sb.AppendLine("【四】G7 核心腔（x[-10,10] z[7,25]）内的所有对象");
            sb.AppendLine();
            var inG7 = new List<string>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                Vector3 p = t.position;
                if (p.x >= -11f && p.x <= 11f && p.z >= 6f && p.z <= 26f)
                {
                    var cols = t.GetComponents<Collider>();
                    bool anyCol = false;
                    string colInfo = "";
                    foreach (var c in cols)
                    {
                        if (c == null || c.isTrigger) continue;
                        anyCol = true;
                        if (c is BoxCollider bc)
                        {
                            Vector3 sz = Vector3.Scale(bc.size, t.lossyScale);
                            colInfo = $"{c.GetType().Name} {sz.x:F1}×{sz.y:F1}×{sz.z:F1}";
                        }
                        else colInfo = c.GetType().Name;
                        break;
                    }
                    var rend = t.GetComponent<Renderer>();
                    inG7.Add($"  {t.name,-26} @ ({p.x,6:F1},{p.y,5:F1},{p.z,6:F1})  " +
                             $"{(anyCol ? "碰撞:" + colInfo : "无碰撞"),-24} " +
                             $"{(rend != null ? "有渲染" : "无渲染")}  " +
                             $"active={t.gameObject.activeInHierarchy}");
                }
            }
            inG7.Sort();
            foreach (var s in inG7) sb.AppendLine(s);

            // ══════ 五、所有怪物 ══════
            sb.AppendLine();
            sb.AppendLine("【五】第四章所有怪物");
            sb.AppendLine();
            var mimics = Object.FindObjectsByType<AbyssEcho.Horror.MimicStalker>(FindObjectsSortMode.None);
            var monsters = Object.FindObjectsByType<AbyssEcho.Monster.MonsterAI>(FindObjectsSortMode.None);
            sb.AppendLine($"MimicStalker = {mimics.Length}   MonsterAI = {monsters.Length}");
            sb.AppendLine();

            foreach (var m in mimics)
            {
                Vector3 p = m.transform.position;
                string room = WhichRoom(rooms, p);

                var so = new SerializedObject(m);
                float atkD = so.FindProperty("attackDistance")?.floatValue ?? -1;
                float vanD = so.FindProperty("vanishDistance")?.floatValue ?? -1;
                float appD = so.FindProperty("approachStartDistance")?.floatValue ?? -1;
                bool enableAtk = so.FindProperty("enableAttack")?.boolValue ?? false;

                var col = m.GetComponent<Collider>();
                var agent = m.GetComponent<NavMeshAgent>();

                // 位置是否在墙里
                var overlaps = Physics.OverlapSphere(p + Vector3.up * 0.9f, 0.45f, ~0, QueryTriggerInteraction.Ignore);
                var wallNames = new List<string>();
                foreach (var o in overlaps)
                {
                    if (o == null || o.isTrigger) continue;
                    if (o.transform.IsChildOf(m.transform)) continue;
                    wallNames.Add(o.gameObject.name);
                }

                // 脚下有地面
                bool hasGround = Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore);

                // NavMesh 位置
                bool onNavMesh = NavMesh.SamplePosition(p, out var navHit, 3f, NavMesh.AllAreas);
                float navDist = onNavMesh ? Vector3.Distance(p, navHit.position) : -1;

                sb.AppendLine($"  ● {m.gameObject.name}");
                sb.AppendLine($"      初始坐标 = ({p.x:F2}, {p.y:F2}, {p.z:F2})");
                sb.AppendLine($"      所属区域 = {room}");
                sb.AppendLine($"      生成方式 = 场景预置（场景中直接存在的对象）");
                sb.AppendLine($"      移动方式 = 直线追击（无 NavMeshAgent），带 SphereCast 绕墙");
                sb.AppendLine($"      NavMeshAgent = {(agent != null ? "有" : "无")}  NavMesh 采样距离 = {(onNavMesh ? navDist.ToString("F2") + "m" : "❌ 不在 NavMesh 上")}");
                sb.AppendLine($"      attackDistance = {atkD}   vanishDistance = {vanD}   approachStartDistance = {appD}");
                sb.AppendLine($"      enableAttack = {enableAtk}");
                sb.AppendLine($"      碰撞体 = {(col != null ? col.GetType().Name : "❌ 无")}");
                sb.AppendLine($"      脚下有地面 = {(hasGround ? "✅" : "❌")}");
                sb.AppendLine($"      与墙体重叠 = {(wallNames.Count > 0 ? $"❌ {string.Join(", ", wallNames)}" : "✅ 无")}");
                sb.AppendLine($"      activeInHierarchy = {m.gameObject.activeInHierarchy}");
                sb.AppendLine();
            }

            // ══════ 六、逐段可通行性 ══════
            sb.AppendLine();
            sb.AppendLine("【六】G5 → G7 → 心瓣 → 门 → 终点 逐段通行检测");
            sb.AppendLine("      （玩家胶囊：半径 0.35，高 1.8）");
            sb.AppendLine();

            var route = new[]
            {
                new Vector3(-19f, 0.05f, 0f),     // G5 中心
                new Vector3(-19f, 0.05f, 4f),     // G5 东北
                new Vector3(-16f, 0.05f, 7.5f),   // 靠近东门洞
                new Vector3(-13.5f, 0.05f, 8f),   // 门洞
                new Vector3(-11.5f, 0.05f, 8f),   // 核心西门
                new Vector3(-9f, 0.05f, 8.5f),    // 进入 G7
                new Vector3(-5f, 0.05f, 11f),     // G7 内
                new Vector3(0f, 0.05f, 13.8f),    // 心瓣前
                new Vector3(0f, 0.05f, 16f),      // 心瓣位置
                new Vector3(0f, 0.05f, 20f),      // 心瓣北
                new Vector3(0f, 0.05f, 24f),      // 门前
                new Vector3(0f, 0.05f, 24.7f),    // 门位置
            };

            for (int i = 0; i < route.Length - 1; i++)
            {
                Vector3 a = route[i], b = route[i + 1];
                Vector3 dir = (b - a).normalized;
                float dist = Vector3.Distance(a, b);

                // 用玩家高度做胶囊检测
                bool blocked = Physics.CapsuleCast(
                    a + Vector3.up * 0.1f, a + Vector3.up * 1.7f, 0.35f,
                    dir, out var hit, dist, ~0, QueryTriggerInteraction.Ignore);

                string hitName = blocked && hit.collider != null ? hit.collider.gameObject.name : "";
                sb.AppendLine($"  [{i,2}] ({a.x,6:F1},{a.z,5:F1}) → ({b.x,6:F1},{b.z,5:F1})  {dist,4:F1}m  " +
                              (blocked ? $"❌ 撞到 '{hitName}'" : "✅ 通畅"));
            }

            // ══════ 七、终点区域 ══════
            sb.AppendLine();
            sb.AppendLine("【七】结局触发区");
            sb.AppendLine();
            var ending = Object.FindFirstObjectByType<AbyssEcho.Narrative.ChapterEndingSystem>();
            if (ending != null)
            {
                var tcF = typeof(AbyssEcho.Narrative.ChapterEndingSystem).GetField("triggerCenter");
                var tsF = typeof(AbyssEcho.Narrative.ChapterEndingSystem).GetField("triggerSize");
                Vector3 tc = (Vector3)tcF.GetValue(ending);
                Vector3 ts = (Vector3)tsF.GetValue(ending);
                sb.AppendLine($"  触发中心 = {tc}");
                sb.AppendLine($"  触发尺寸 = {ts}");
                sb.AppendLine($"  Z 范围 = [{tc.z - ts.z * 0.5f:F1}, {tc.z + ts.z * 0.5f:F1}]");
                sb.AppendLine($"  G7 北边界 z = 25.0，门在 z = 24.7");
                sb.AppendLine($"  → 触发区是否在门之外（北侧）：{(tc.z > 24.7f ? "✅ 是" : "❌ 否（触发区可能被门挡住）")}");
            }

            string text = sb.ToString();
            Debug.Log(text);
            File.WriteAllText(Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(Application.dataPath), "../Reports/Ch4_DeepAudit.txt")),
                text, new UTF8Encoding(false));
        }

        private static void DumpGateDetail(StringBuilder sb, GameObject gate)
        {
            sb.AppendLine($"  ✅ 存在：{GetPath(gate.transform)}");
            sb.AppendLine($"  世界坐标 = {gate.transform.position}");
            sb.AppendLine($"  局部坐标 = {gate.transform.localPosition}");
            sb.AppendLine($"  旋转 = {gate.transform.eulerAngles}");
            sb.AppendLine($"  局部缩放 = {gate.transform.localScale}");
            sb.AppendLine($"  世界缩放 = {gate.transform.lossyScale}");
            sb.AppendLine($"  activeSelf = {gate.activeSelf}");
            sb.AppendLine($"  activeInHierarchy = {gate.activeInHierarchy}");
            sb.AppendLine($"  layer = {LayerMask.LayerToName(gate.layer)} ({gate.layer})");
            sb.AppendLine($"  tag = {gate.tag}");
            sb.AppendLine();

            // 组件
            sb.AppendLine("  组件清单：");
            foreach (var c in gate.GetComponents<Component>())
            {
                if (c == null) { sb.AppendLine("    ❌ Missing Script"); continue; }
                sb.AppendLine($"    {c.GetType().Name}");
            }
            sb.AppendLine();

            // Collider
            var col = gate.GetComponent<Collider>();
            if (col != null)
            {
                sb.AppendLine($"  Collider = {col.GetType().Name}");
                sb.AppendLine($"    isTrigger = {col.isTrigger}  enabled = {col.enabled}");
                sb.AppendLine($"    包围盒 center = {col.bounds.center}");
                sb.AppendLine($"    包围盒 size   = {col.bounds.size}");
                sb.AppendLine($"    包围盒范围 x[{col.bounds.min.x:F2},{col.bounds.max.x:F2}] " +
                              $"y[{col.bounds.min.y:F2},{col.bounds.max.y:F2}] " +
                              $"z[{col.bounds.min.z:F2},{col.bounds.max.z:F2}]");
                if (col is BoxCollider bc)
                    sb.AppendLine($"    BoxCollider.size = {bc.size}  center = {bc.center}");
            }
            else sb.AppendLine("  ❌ 没有 Collider");
            sb.AppendLine();

            // Renderer
            var rend = gate.GetComponent<Renderer>();
            if (rend != null)
            {
                sb.AppendLine($"  Renderer = {rend.GetType().Name}");
                sb.AppendLine($"    enabled = {rend.enabled}");
                sb.AppendLine($"    材质 = {(rend.sharedMaterial != null ? rend.sharedMaterial.name : "NULL")}");
                sb.AppendLine($"    Shader = {(rend.sharedMaterial != null && rend.sharedMaterial.shader != null ? rend.sharedMaterial.shader.name : "NULL")}");
                sb.AppendLine($"    包围盒 = {rend.bounds.center} size={rend.bounds.size}");
                sb.AppendLine($"    isVisible（相机可见性标记）= {rend.isVisible}");
            }
            else sb.AppendLine("  ❌ 没有 Renderer（门没有可见实体）");
            sb.AppendLine();

            var mf = gate.GetComponent<MeshFilter>();
            if (mf != null)
                sb.AppendLine($"  MeshFilter.mesh = {(mf.sharedMesh != null ? mf.sharedMesh.name : "NULL")}");
            else
                sb.AppendLine("  ❌ 没有 MeshFilter");

            var anim = gate.GetComponent<Animator>();
            sb.AppendLine($"  Animator = {(anim != null ? "有" : "无")}");
            sb.AppendLine();

            // 交互脚本
            var door = gate.GetComponent<AbyssEcho.Interaction.DoorInteractable>();
            if (door != null)
            {
                var so = new SerializedObject(door);
                sb.AppendLine($"  DoorInteractable：");
                sb.AppendLine($"    locked = {door.locked}");
                sb.AppendLine($"    IsOpen = {door.IsOpen}");
                sb.AppendLine($"    slideDistance = {so.FindProperty("slideDistance")?.floatValue}");
                sb.AppendLine($"    swingAngle = {so.FindProperty("swingAngle")?.floatValue}");
                sb.AppendLine($"    openSpeed = {so.FindProperty("openSpeed")?.floatValue}");
                sb.AppendLine($"    unlockableByInteraction = {so.FindProperty("unlockableByInteraction")?.boolValue}");
                sb.AppendLine($"    lockedPrompt = \"{so.FindProperty("lockedPrompt")?.stringValue}\"");
                sb.AppendLine($"    requiredItemId = \"{so.FindProperty("requiredItemId")?.stringValue}\"");
            }
            else sb.AppendLine("  ❌ 没有 DoorInteractable");

            sb.AppendLine();
            sb.AppendLine("  父物体链：");
            var t = gate.transform;
            int d = 0;
            while (t != null && d++ < 10)
            {
                sb.AppendLine($"    [{d}] {t.name}  active={t.gameObject.activeSelf} pos={t.position}");
                t = t.parent;
            }
        }

        private static string WhichRoom(List<AbyssEcho.LevelGen.RoomDef> rooms, Vector3 p)
        {
            foreach (var r in rooms)
                if (p.x >= r.MinX - 0.5f && p.x <= r.MaxX + 0.5f &&
                    p.z >= r.MinZ - 0.5f && p.z <= r.MaxZ + 0.5f)
                    return $"{r.Id} {r.DisplayName}";
            return "❌ 不在任何房间内";
        }

        private static string GetPath(Transform t)
        {
            string p = t.name;
            var cur = t.parent; int g = 0;
            while (cur != null && g++ < 20) { p = cur.name + "/" + p; cur = cur.parent; }
            return p;
        }
    }
}