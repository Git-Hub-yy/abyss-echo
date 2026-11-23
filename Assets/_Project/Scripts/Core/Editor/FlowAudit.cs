using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// 阶段一审计（只读）：完整列出第一章所有门、保险丝、配电箱的实际 Inspector 配置。
    /// 不做任何修改。
    /// </summary>
    public static class FlowAudit
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter1_Graybox.unity";

        [MenuItem("ABYSS ECHO/审计/第一章流程审计（只读）")]
        public static void Run()
        {
            // 只读打开，不保存
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var sb = new StringBuilder();
            sb.AppendLine("========== ABYSS ECHO 第一章流程审计 ==========");
            sb.AppendLine($"场景：{ScenePath}");
            sb.AppendLine();

            // ── 1. 所有门 ──
            sb.AppendLine("=== 1. 门（DoorInteractable）===");
            var doors = Object.FindObjectsByType<AbyssEcho.Interaction.DoorInteractable>(FindObjectsSortMode.None);
            sb.AppendLine($"共 {doors.Length} 扇");
            sb.AppendLine();

            foreach (var d in doors)
            {
                sb.AppendLine($"--- {d.name} ---");
                sb.AppendLine($"  Transform.position   = {d.transform.position}");
                sb.AppendLine($"  Transform.localPos   = {d.transform.localPosition}");
                sb.AppendLine($"  Transform.localScale = {d.transform.localScale}");
                sb.AppendLine($"  locked                    = {d.locked}");
                sb.AppendLine($"  requiredItemId            = \"{d.requiredItemId}\"");
                sb.AppendLine($"  unlockableByInteraction   = {d.unlockableByInteraction}");
                sb.AppendLine($"  lockedPrompt              = \"{d.lockedPrompt}\"");
                sb.AppendLine($"  slideDistance             = {d.slideDistance}");
                sb.AppendLine($"  swingAngle                = {d.swingAngle}");
                sb.AppendLine($"  speed                     = {d.speed}");
                sb.AppendLine($"  IsOpen                    = {d.IsOpen}");
                sb.AppendLine($"  interactable(基类)        = {d.interactable}");
                sb.AppendLine($"  GameObject.activeInHier   = {d.gameObject.activeInHierarchy}");
                sb.AppendLine($"  enabled                   = {d.enabled}");

                // 碰撞体
                var col = d.GetComponent<BoxCollider>();
                if (col != null)
                {
                    Vector3 wc = d.transform.TransformPoint(col.center);
                    Vector3 ws = Vector3.Scale(col.size, d.transform.lossyScale);
                    sb.AppendLine($"  BoxCollider: center={col.center} size={col.size} isTrigger={col.isTrigger} enabled={col.enabled}");
                    sb.AppendLine($"    世界包围盒: 中心=({wc.x:F2},{wc.y:F2},{wc.z:F2}) 尺寸=({ws.x:F2},{ws.y:F2},{ws.z:F2})");
                    sb.AppendLine($"    y范围=[{wc.y - ws.y * 0.5f:F2}, {wc.y + ws.y * 0.5f:F2}]  z范围=[{wc.z - ws.z * 0.5f:F2}, {wc.z + ws.z * 0.5f:F2}]");
                }
                else
                {
                    sb.AppendLine("  BoxCollider: ❌ 无");
                }

                // 组件清单
                var comps = d.GetComponents<Component>();
                sb.Append("  Components: ");
                foreach (var c in comps) sb.Append(c != null ? c.GetType().Name + " " : "MISSING ");
                sb.AppendLine();

                // 子物体
                if (d.transform.childCount > 0)
                {
                    sb.Append("  Children: ");
                    foreach (Transform ch in d.transform) sb.Append(ch.name + " ");
                    sb.AppendLine();
                }

                sb.AppendLine();
            }

            // ── 2. 保险丝 ──
            sb.AppendLine("=== 2. 拾取物（PickupItem）===");
            var picks = Object.FindObjectsByType<AbyssEcho.Interaction.PickupItem>(FindObjectsSortMode.None);
            sb.AppendLine($"共 {picks.Length} 个");
            foreach (var p in picks)
            {
                sb.AppendLine($"--- {p.name} ---");
                sb.AppendLine($"  位置 = {p.transform.position}");
                sb.AppendLine($"  itemId = \"{p.itemId}\"   displayName = \"{p.displayName}\"");
                sb.AppendLine($"  amount = {p.amount}   destroyOnPickup = {p.destroyOnPickup}");
                sb.AppendLine($"  interactable = {p.interactable}   enabled = {p.enabled}");
                var c = p.GetComponent<BoxCollider>();
                sb.AppendLine($"  Collider = {(c != null ? $"Box size={c.size} isTrigger={c.isTrigger}" : "无")}");
                sb.AppendLine();
            }

            // ── 3. 配电箱 ──
            sb.AppendLine("=== 3. 配电箱（PowerRestorePanel）===");
            var powers = Object.FindObjectsByType<AbyssEcho.Puzzle.PowerRestorePanel>(FindObjectsSortMode.None);
            sb.AppendLine($"共 {powers.Length} 个");
            foreach (var p in powers)
            {
                sb.AppendLine($"--- {p.name} ---");
                sb.AppendLine($"  位置 = {p.transform.position}");
                sb.AppendLine($"  requiredItemId = \"{p.requiredItemId}\"");
                sb.AppendLine($"  consumeItem = {p.consumeItem}");
                sb.AppendLine($"  IsPowered = {p.IsPowered}");
                sb.AppendLine($"  lightsToEnable = {(p.lightsToEnable != null ? p.lightsToEnable.Length + " 盏" : "null")}");
                sb.AppendLine($"  doorsToUnlock  = {(p.doorsToUnlock != null ? p.doorsToUnlock.Length + " 扇" : "null ← 问题可能在这里")}");
                if (p.doorsToUnlock != null)
                    foreach (var dd in p.doorsToUnlock)
                        sb.AppendLine($"      → {(dd != null ? dd.name : "NULL")}");
                sb.AppendLine($"  interactable = {p.interactable}  enabled = {p.enabled}");
                sb.AppendLine();
            }

            // ── 4. PlayerInventory ──
            sb.AppendLine("=== 4. 玩家背包（PlayerInventory）===");
            var invs = Object.FindObjectsByType<AbyssEcho.Inventory.PlayerInventory>(FindObjectsSortMode.None);
            sb.AppendLine($"共 {invs.Length} 个");
            foreach (var i in invs)
            {
                sb.AppendLine($"  {i.name}: enabled={i.enabled} activeInHierarchy={i.gameObject.activeInHierarchy}");
            }
            sb.AppendLine();

            // ── 5. 交互检测器 ──
            sb.AppendLine("=== 5. InteractionDetector ===");
            var dets = Object.FindObjectsByType<AbyssEcho.Interaction.InteractionDetector>(FindObjectsSortMode.None);
            sb.AppendLine($"共 {dets.Length} 个");
            foreach (var dt in dets)
            {
                sb.AppendLine($"  {dt.name}: enabled={dt.enabled}");
            }
            sb.AppendLine();

            // ── 6. 恐怖系统 ──
            sb.AppendLine("=== 6. 恐怖系统 ===");
            var triggers = Object.FindObjectsByType<AbyssEcho.Horror.HorrorTrigger>(FindObjectsSortMode.None);
            sb.AppendLine($"HorrorTrigger 共 {triggers.Length} 个");
            foreach (var t in triggers)
            {
                var c = t.GetComponent<BoxCollider>();
                sb.AppendLine($"--- {t.name} ---");
                sb.AppendLine($"  位置 = {t.transform.position}");
                sb.AppendLine($"  Collider = {(c != null ? $"size={c.size} isTrigger={c.isTrigger} enabled={c.enabled} active={c.enabled && t.gameObject.activeInHierarchy}" : "无")}");
                sb.AppendLine($"  enabled={t.enabled}  delay={t.delay}  oneShot={t.oneShot}");
                sb.AppendLine($"  maxFearToTrigger={t.maxFearToTrigger}  fearJump={t.fearJump}");
                sb.AppendLine($"  lightsToKill={(t.lightsToKill != null ? t.lightsToKill.Length + " 盏" : "null")}");
                if (t.lightsToKill != null)
                    foreach (var l in t.lightsToKill)
                        sb.AppendLine($"      → {(l != null ? $"{l.name} enabled={l.enabled} 光强={l.intensity:F2}" : "NULL")}");
                sb.AppendLine($"  dimAmbient={t.dimAmbient}  dimAmbientTo={t.dimAmbientTo}  dimDuration={t.dimDuration}");
                sb.AppendLine($"  lightsToFlicker={(t.lightsToFlicker != null ? t.lightsToFlicker.Length + " 个" : "null")}");
                sb.AppendLine();
            }

            var fear = Object.FindFirstObjectByType<AbyssEcho.Horror.FearSystem>();
            sb.AppendLine($"FearSystem: {(fear != null ? fear.name + " 恐惧=" + fear.currentFear : "❌ 无")}");
            var fv = Object.FindFirstObjectByType<AbyssEcho.Horror.FearVisuals>();
            sb.AppendLine($"FearVisuals: {(fv != null ? fv.name + " enabled=" + fv.enabled : "❌ 无")}");

            // Volume
            var vols = Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None);
            sb.AppendLine($"Volume 共 {vols.Length} 个");
            foreach (var v in vols)
            {
                sb.AppendLine($"  {v.name}: isGlobal={v.isGlobal} priority={v.priority} weight={v.weight} enabled={v.enabled} profile={(v.profile != null ? v.profile.name : "NULL")}");
                if (v.profile != null)
                {
                    sb.AppendLine($"    profile.components 数量 = {v.profile.components.Count}");
                    foreach (var comp in v.profile.components)
                        sb.AppendLine($"      - {comp.GetType().Name} active={comp.active}");
                }
            }

            // 相机
            var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            sb.AppendLine($"Camera 共 {cams.Length} 个");
            foreach (var cam in cams)
            {
                var data = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                sb.AppendLine($"  {cam.name}: enabled={cam.enabled} depth={cam.depth}");
                if (data != null)
                {
                    sb.AppendLine($"    volumeLayerMask={data.volumeLayerMask.value} renderPostProcessing={data.renderPostProcessing}");
                    sb.AppendLine($"    antialiasing={data.antialiasing}");
                }
                else sb.AppendLine("    ❌ 无 UniversalAdditionalCameraData");
            }

            sb.AppendLine();
            sb.AppendLine("=== A2 区域的灯 ===");
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (Mathf.Abs(l.transform.position.z - (-24.5f)) < 6f)
                    sb.AppendLine($"  {l.name}: 位置={l.transform.position} type={l.type} enabled={l.enabled} 强度={l.intensity:F2} 颜色={l.color}");
            }

            string text = sb.ToString();
            Debug.Log("<b>[FlowAudit]</b>\n" + text);

            try
            {
                string dir = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "FlowAudit.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}