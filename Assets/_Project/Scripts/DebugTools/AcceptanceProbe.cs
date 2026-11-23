using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// 验收审计用的只读运行时探测。
    ///
    /// ⚠️ 只读：只检查组件状态、调用只读属性，**不修改任何游戏状态**。
    ///   唯一例外是调用各谜题系统的 OnInteract 来验证链路 ——
    ///   但那会改变状态，所以这里**不调用**，只做静态检查。
    ///
    /// 覆盖：
    ///   玩家系统（移动/视角/交互/手电筒/背包）
    ///   物品 ID 一致性
    ///   门锁状态与解锁条件
    ///   恐怖事件配置
    ///   音效系统
    /// </summary>
    public class AcceptanceProbe : MonoBehaviour
    {
        public bool runOnStart = true;

        /// <summary>
        /// 强制章节（0 = 用 PlayerPrefs 里的当前章节）。
        /// ⚠️ 必须设置：否则所有探测都会跑成 PlayerPrefs 里残留的那个章节。
        /// </summary>
        public int forceChapter = 0;

        private readonly StringBuilder _log = new StringBuilder();

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return new WaitForSeconds(0.6f);

            if (forceChapter >= 1 && forceChapter <= 5)
                AbyssEcho.LevelGen.ChapterLayout.Current =
                    (AbyssEcho.LevelGen.ChapterLayout.Chapter)forceChapter;

            var chapter = AbyssEcho.LevelGen.ChapterLayout.Current;

            _log.AppendLine($"===== 验收探测：{AbyssEcho.LevelGen.ChapterLayout.GetChapterName(chapter)} =====");
            _log.AppendLine();

            // ═══════ 1. 玩家基础系统 ═══════
            _log.AppendLine("--- 1. 玩家基础系统 ---");

            var motor = FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            var look = FindFirstObjectByType<AbyssEcho.Player.PlayerLook>();
            var input = FindFirstObjectByType<AbyssEcho.Player.PlayerInputReader>();
            var cam = Camera.main;
            var cc = motor != null ? motor.GetComponent<CharacterController>() : null;

            Line("PlayerMotor", motor != null);
            Line("PlayerLook", look != null);
            Line("PlayerInputReader", input != null);
            Line("Camera.main", cam != null);
            Line("CharacterController", cc != null);

            if (cc != null)
                _log.AppendLine($"    CC: height={cc.height:F2} radius={cc.radius:F2} center={cc.center} enabled={cc.enabled}");

            if (motor != null)
                _log.AppendLine($"    玩家位置: {motor.transform.position}");

            // ═══════ 2. 交互系统 ═══════
            _log.AppendLine();
            _log.AppendLine("--- 2. 交互系统 ---");

            var det = FindFirstObjectByType<AbyssEcho.Interaction.InteractionDetector>();
            var cross = FindFirstObjectByType<AbyssEcho.UI.CrosshairUI>();
            Line("InteractionDetector", det != null);
            Line("CrosshairUI", cross != null);

            if (det != null)
            {
                var f = typeof(AbyssEcho.Interaction.InteractionDetector);
                var distF = f.GetField("interactDistance",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public);
                if (distF != null)
                    _log.AppendLine($"    interactDistance = {distF.GetValue(det)}");
            }

            // ═══════ 3. 手电筒 ═══════
            _log.AppendLine();
            _log.AppendLine("--- 3. 手电筒 ---");

            var flash = FindFirstObjectByType<AbyssEcho.Tools.FlashlightSystem>();
            Line("FlashlightSystem", flash != null);

            if (flash != null)
            {
                var t = typeof(AbyssEcho.Tools.FlashlightSystem);
                var slF = t.GetField("spotLight");
                var mrF = t.GetField("modelRoot");
                var onF = t.GetField("IsOn") ?? t.GetProperty("IsOn")?.DeclaringType?.GetField("IsOn");
                var battF = t.GetField("battery") ?? t.GetField("maxBattery") ?? t.GetField("batteryLevel");

                object sl = slF?.GetValue(flash);
                object mr = mrF?.GetValue(flash);
                _log.AppendLine($"    spotLight 引用 = {(sl != null ? "有" : "❌ 空")}");
                _log.AppendLine($"    modelRoot 引用 = {(mr != null ? "有" : "❌ 空")}");
            }

            // ═══════ 4. 背包 ═══════
            _log.AppendLine();
            _log.AppendLine("--- 4. 背包 ---");

            var inv = AbyssEcho.Inventory.PlayerInventory.Instance;
            Line("PlayerInventory.Instance", inv != null);
            var invUI = FindFirstObjectByType<AbyssEcho.UI.InventoryUI>();
            Line("InventoryUI", invUI != null);

            // ═══════ 5. 物品 ID 一致性 ═══════
            _log.AppendLine();
            _log.AppendLine("--- 5. 物品 ID 一致性 ---");

            var knownIds = new System.Collections.Generic.HashSet<string>
            {
                AbyssEcho.Inventory.ItemIds.Fuse,
                AbyssEcho.Inventory.ItemIds.AccessCard,
                AbyssEcho.Inventory.ItemIds.Note,
                AbyssEcho.Inventory.ItemIds.Tape,
                AbyssEcho.Inventory.ItemIds.Bulb,
                AbyssEcho.Inventory.ItemIds.Glove,
            };

            foreach (var p in FindObjectsByType<AbyssEcho.Interaction.PickupItem>(FindObjectsSortMode.None))
            {
                bool known = knownIds.Contains(p.itemId);
                _log.AppendLine($"    拾取物 {p.gameObject.name}: itemId='{p.itemId}' {(known ? "OK" : "❌ 未知 ID")}");
            }

            // 谜题需要的物品
            foreach (var panel in FindObjectsByType<AbyssEcho.Puzzle.PowerRestorePanel>(FindObjectsSortMode.None))
            {
                bool known = string.IsNullOrEmpty(panel.requiredItemId) || knownIds.Contains(panel.requiredItemId);
                _log.AppendLine($"    配电箱 {panel.gameObject.name}: requiredItemId='{panel.requiredItemId}' {(known ? "OK" : "❌")} doorsToUnlock={panel.doorsToUnlock?.Length}");
            }

            foreach (var altar in FindObjectsByType<AbyssEcho.Puzzle.OfferingAltar>(FindObjectsSortMode.None))
            {
                _log.AppendLine($"    供奉台 {altar.gameObject.name}: A='{altar.itemForEndingA}' C='{altar.itemForEndingC}' " +
                                $"{(knownIds.Contains(altar.itemForEndingA) ? "OK" : "❌")} {(knownIds.Contains(altar.itemForEndingC) ? "OK" : "❌")}");
            }

            // ═══════ 6. 门与解锁条件 ═══════
            _log.AppendLine();
            _log.AppendLine("--- 6. 门与解锁条件 ---");

            foreach (var d in FindObjectsByType<AbyssEcho.Interaction.DoorInteractable>(FindObjectsSortMode.None))
            {
                _log.AppendLine($"    门 {d.name}: locked={d.locked} unlockable={d.unlockableByInteraction} " +
                                $"slide={d.slideDistance:F2} IsOpen={d.IsOpen}");
                _log.AppendLine($"       prompt='{d.lockedPrompt}'");
            }

            // 谜题 → 门 的关联
            foreach (var k in FindObjectsByType<AbyssEcho.Puzzle.KeypadPanel>(FindObjectsSortMode.None))
                _log.AppendLine($"    键盘 {k.gameObject.name}: code='{k.correctCode}' doors={k.doorsToUnlock?.Length} IsUnlocked={k.IsUnlocked}");

            foreach (var w in FindObjectsByType<AbyssEcho.Puzzle.WaterPumpSwitch>(FindObjectsSortMode.None))
                _log.AppendLine($"    水泵 {w.gameObject.name}: doors={w.doorsToUnlock?.Length} IsDrained={w.IsDrained}");

            foreach (var fc in FindObjectsByType<AbyssEcho.Puzzle.FrequencyConsole>(FindObjectsSortMode.None))
                _log.AppendLine($"    频率 {fc.gameObject.name}: correct={fc.correctFrequency} doors={fc.doorsToUnlock?.Length} IsUnlocked={fc.IsUnlocked}");

            foreach (var pu in FindObjectsByType<AbyssEcho.Puzzle.PulseSyncPuzzle>(FindObjectsSortMode.None))
                _log.AppendLine($"    脉搏 {pu.gameObject.name}: window={pu.syncWindow} need={pu.requiredSyncs} door={(pu.doorToUnlock != null ? pu.doorToUnlock.name : "❌ 空")}");

            foreach (var ev in FindObjectsByType<AbyssEcho.Puzzle.EvacuationConsole>(FindObjectsSortMode.None))
                _log.AppendLine($"    撤离台 {ev.gameObject.name}: exitDoor={(ev.exitDoor != null ? ev.exitDoor.name : "空")} HasChosen={ev.HasChosen}");

            // ═══════ 7. 恐怖系统 ═══════
            _log.AppendLine();
            _log.AppendLine("--- 7. 恐怖系统 ---");

            var fear = FindFirstObjectByType<AbyssEcho.Horror.FearSystem>();
            var fearV = FindFirstObjectByType<AbyssEcho.Horror.FearVisuals>();
            var death = FindFirstObjectByType<AbyssEcho.Horror.PlayerDeathSystem>();
            Line("FearSystem", fear != null);
            Line("FearVisuals", fearV != null);
            Line("PlayerDeathSystem", death != null);

            foreach (var t in FindObjectsByType<AbyssEcho.Horror.HorrorTrigger>(FindObjectsSortMode.None))
                _log.AppendLine($"    HorrorTrigger {t.name}: delay={t.delay} oneShot={t.oneShot} " +
                                $"killLights={t.lightsToKill?.Length} flicker={t.lightsToFlicker?.Length} " +
                                $"dimAmbient={t.dimAmbient}");

            foreach (var f in FindObjectsByType<AbyssEcho.Horror.FlickerLight>(FindObjectsSortMode.None))
                _log.AppendLine($"    FlickerLight {f.gameObject.name} enabled={f.enabled}");

            // ═══════ 8. 音频 ═══════
            _log.AppendLine();
            _log.AppendLine("--- 8. 音频 ---");

            var audio = AbyssEcho.Audio.AudioManager.Instance;
            Line("AudioManager.Instance", audio != null);
            if (audio != null)
                _log.AppendLine($"    master={audio.masterVolume:F2} sfx={audio.sfxVolume:F2} ambient={audio.ambientVolume:F2}");
            Line("AudioListener", FindFirstObjectByType<AudioListener>() != null);

            // ═══════ 9. 存读档 ═══════
            _log.AppendLine();
            _log.AppendLine("--- 9. 存读档 ---");
            var save = FindFirstObjectByType<AbyssEcho.Save.AutoSaveManager>();
            Line("AutoSaveManager", save != null);
            _log.AppendLine($"    SaveSystem.HasSave() = {AbyssEcho.Save.SaveSystem.HasSave()}");

            // ═══════ 10. 后处理 ═══════
            _log.AppendLine();
            _log.AppendLine("--- 10. 后处理 ---");
            var vols = FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None);
            _log.AppendLine($"    Volume 数量 = {vols.Length}");
            foreach (var v in vols)
                _log.AppendLine($"    {v.gameObject.name}: profile={(v.sharedProfile != null ? v.sharedProfile.name : "❌ NULL")} weight={v.weight}");

            if (cam != null)
            {
                var extra = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                _log.AppendLine($"    相机 postProcessing = {(extra != null ? extra.renderPostProcessing.ToString() : "无 UniversalAdditionalCameraData")}");
            }

            _log.AppendLine();
            _log.AppendLine("--- 环境 ---");
            _log.AppendLine($"    ambientMode={RenderSettings.ambientMode} ambient={RenderSettings.ambientLight}");
            _log.AppendLine($"    fog={RenderSettings.fog} density={RenderSettings.fogDensity} color={RenderSettings.fogColor}");

            string text = _log.ToString();
            Debug.Log("<b>[AcceptanceProbe]</b>\n" + text);

            WriteResult(text);

            if (!Application.isEditor) Application.Quit(0);
        }

        private void Line(string label, bool ok)
        {
            _log.AppendLine($"    {(ok ? "✓" : "✗")} {label}");
        }

        private void WriteResult(string text)
        {
            var di = new DirectoryInfo(Application.dataPath);
            for (int hop = 0; hop < 6 && di != null; hop++)
            {
                string reports = Path.Combine(di.FullName, "Reports");
                if (Directory.Exists(reports))
                {
                    try
                    {
                        string fn = $"AcceptanceProbe_Ch{(int)AbyssEcho.LevelGen.ChapterLayout.Current}.txt";
                        File.WriteAllText(Path.Combine(reports, fn), text, new UTF8Encoding(false));
                        return;
                    }
                    catch { }
                }
                di = di.Parent;
            }
            try
            {
                File.WriteAllText(Path.Combine(Application.persistentDataPath, "AcceptanceProbe.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}