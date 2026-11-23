using System.IO;
using UnityEngine;

namespace AbyssEcho.Save
{
    /// <summary>
    /// 存档系统：记录玩家进度（位置、背包、已解锁的门、恐惧值等）。
    ///
    /// 存储方式：JSON 文件，位于 Application.persistentDataPath。
    ///   Windows 实际路径：%USERPROFILE%\AppData\LocalLow\AbyssEchoStudio\ABYSS ECHO\save_ch1.json
    ///
    /// 为什么用 JSON 而不是 PlayerPrefs：
    ///   · 存档结构会随章节增加而变复杂，JSON 便于扩展
    ///   · 可直接用文本编辑器查看/修复（调试友好）
    ///   · 不受 PlayerPrefs 的大小限制
    ///
    /// 存什么（第一章）：
    ///   · 玩家位置与朝向
    ///   · 背包物品
    ///   · 保险丝是否已安装（电力状态）
    ///   · 门禁是否已解锁
    ///   · 已读线索标题列表
    ///   · 恐惧值
    /// </summary>
    public static class SaveSystem
    {
        private const string FileName = "save_ch1.json";
        private const string Version = "1.0";

        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>存档数据（可序列化）。</summary>
        [System.Serializable]
        public class SaveData
        {
            public string version = Version;
            public string savedAt;          // ISO 时间戳，便于调试

            // 玩家
            public float posX, posY, posZ;
            public float yaw, pitch;
            public float fear;

            // 背包（ID 与数量分开存储，便于 JSON 阅读）
            public string[] itemIds = new string[0];
            public int[] itemCounts = new int[0];

            // 进度
            public bool powerRestored;
            public bool keypadUnlocked;
            public string[] journalTitles = new string[0];
        }

        /// <summary>是否存在存档。</summary>
        public static bool HasSave() => File.Exists(SavePath);

        /// <summary>
        /// S82：恢复存档位置时向下探测地面的最大距离（米）。
        ///
        /// 探测不到地面 → 判定存档位置无效 → 回退到章节出生点。
        /// 6 米足够覆盖正常层高（3~5 米），又不会把二楼存档误判为悬空。
        /// </summary>
        private const float saveGroundProbeDistance = 6f;

        // ────────────────────────── 保存 ──────────────────────────

        public static bool Save(out string message)
        {
            try
            {
                var data = new SaveData
                {
                    savedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                // 玩家位置
                var motor = Object.FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
                var look = Object.FindFirstObjectByType<AbyssEcho.Player.PlayerLook>();
                if (motor != null)
                {
                    var p = motor.transform.position;
                    data.posX = p.x; data.posY = p.y; data.posZ = p.z;
                }
                if (look != null)
                {
                    data.yaw = look.transform.eulerAngles.y;
                }

                // 恐惧
                if (AbyssEcho.Horror.FearSystem.Instance != null)
                    data.fear = AbyssEcho.Horror.FearSystem.Instance.currentFear;

                // 背包
                var inv = AbyssEcho.Inventory.PlayerInventory.Instance;
                if (inv != null)
                {
                    var ids = new System.Collections.Generic.List<string>();
                    var counts = new System.Collections.Generic.List<int>();
                    foreach (var id in new[] { AbyssEcho.Inventory.ItemIds.Fuse,
                                               AbyssEcho.Inventory.ItemIds.AccessCard,
                                               AbyssEcho.Inventory.ItemIds.Note })
                    {
                        int c = inv.Count(id);
                        if (c > 0) { ids.Add(id); counts.Add(c); }
                    }
                    data.itemIds = ids.ToArray();
                    data.itemCounts = counts.ToArray();
                }

                // 谜题进度
                var power = Object.FindFirstObjectByType<AbyssEcho.Puzzle.PowerRestorePanel>();
                if (power != null) data.powerRestored = power.IsPowered;

                var keypad = Object.FindFirstObjectByType<AbyssEcho.Puzzle.KeypadPanel>();
                if (keypad != null) data.keypadUnlocked = keypad.IsUnlocked;

                // 日志
                var journal = AbyssEcho.Narrative.JournalSystem.Instance;
                if (journal != null)
                {
                    var titles = new System.Collections.Generic.List<string>();
                    foreach (var e in journal.Entries) titles.Add(e.Title);
                    data.journalTitles = titles.ToArray();
                }

                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(SavePath, json, new System.Text.UTF8Encoding(false));

                message = "已保存至 " + SavePath;
                Debug.Log("[Save] " + message);
                return true;
            }
            catch (System.Exception e)
            {
                message = "保存失败：" + e.Message;
                Debug.LogError("[Save] " + message);
                return false;
            }
        }

        // ────────────────────────── 读取 ──────────────────────────

        public static SaveData Load()
        {
            if (!File.Exists(SavePath)) return null;

            try
            {
                string json = File.ReadAllText(SavePath, new System.Text.UTF8Encoding(false));
                var data = JsonUtility.FromJson<SaveData>(json);

                if (data == null) return null;
                if (data.version != Version)
                    Debug.LogWarning($"[Save] 存档版本 {data.version} 与当前 {Version} 不同，可能不兼容");

                return data;
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Save] 读取失败：" + e.Message);
                return null;
            }
        }

        /// <summary>把存档应用到当前场景。</summary>
        public static void Apply(SaveData data)
        {
            if (data == null) return;

            // ══════════════════ 玩家位置（S82 安全校验）══════════════════
            //
            //   问题：如果存档里的位置悬空（在虚空里保存、或存档被外部改坏），
            //         恢复后玩家会无限下坠，永远落不到地面，游戏无法继续。
            //
            //         实测复现：
            //           posY = -8.66 的存档 → 开局下坠到 -615 米，速度 -155 m/s
            //
            //   修复：
            //     恢复到存档位置**之前**，先向下探测是否有可站立的地面。
            //     探测不到 → 视为存档位置无效 → 回退到章节出生点。
            //
            //   为什么用 6 米：
            //     正常楼层高度 3~5 米，6 米足够覆盖从存档点落到下层地面的情况，
            //     又不会误判"玩家在二楼存档"为悬空。
            var motor = Object.FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            if (motor != null)
            {
                Vector3 savedPos = new Vector3(data.posX, data.posY, data.posZ);
                Vector3 finalPos = savedPos;

                // 从存档点上方 1 米向下探测地面
                bool hasFloor = Physics.Raycast(
                    savedPos + Vector3.up * 1.0f,
                    Vector3.down,
                    out var groundHit,
                    saveGroundProbeDistance,
                    ~0,
                    QueryTriggerInteraction.Ignore);

                if (!hasFloor)
                {
                    // 存档位置悬空 → 回退到章节出生点
                    Vector3 fallback = AbyssEcho.LevelGen.ChapterLayout.GetPlayerStart();
                    Debug.LogWarning(
                        $"[Save] 存档位置 {savedPos} 下方 {saveGroundProbeDistance} 米内没有地面，" +
                        $"判定为无效存档位置，回退到章节出生点 {fallback}");
                    finalPos = fallback;
                }
                else if (groundHit.point.y > savedPos.y + 0.1f)
                {
                    // 存档点在地下（地面比存档点还高）→ 抬到地面上
                    Debug.LogWarning(
                        $"[Save] 存档位置 {savedPos} 在地面以下（地面 y={groundHit.point.y:F2}），" +
                        $"已抬升到地面之上");
                    finalPos = new Vector3(savedPos.x, groundHit.point.y + 0.05f, savedPos.z);
                }

                var cc = motor.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                motor.transform.position = finalPos;
                if (cc != null) cc.enabled = true;
            }

            var look = Object.FindFirstObjectByType<AbyssEcho.Player.PlayerLook>();
            if (look != null && data.yaw != 0f) look.SetYaw(data.yaw);

            // 恐惧
            if (AbyssEcho.Horror.FearSystem.Instance != null)
                AbyssEcho.Horror.FearSystem.Instance.SetFear(data.fear);

            // 背包
            var inv = AbyssEcho.Inventory.PlayerInventory.Instance;
            if (inv != null)
            {
                inv.Clear();
                int n = Mathf.Min(data.itemIds?.Length ?? 0, data.itemCounts?.Length ?? 0);
                for (int i = 0; i < n; i++)
                    inv.Add(data.itemIds[i], data.itemCounts[i]);
            }

            // 谜题
            if (data.keypadUnlocked)
            {
                var keypad = Object.FindFirstObjectByType<AbyssEcho.Puzzle.KeypadPanel>();
                if (keypad != null) keypad.Submit(keypad.correctCode);
            }

            // 日志
            var journal = AbyssEcho.Narrative.JournalSystem.Instance;
            if (journal != null && data.journalTitles != null)
                foreach (var t in data.journalTitles)
                    journal.AddEntry(t, "");

            Debug.Log("[Save] 存档已应用");
        }

        /// <summary>删除存档。</summary>
        public static void Delete()
        {
            try
            {
                if (File.Exists(SavePath)) File.Delete(SavePath);
                Debug.Log("[Save] 存档已删除");
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Save] 删除失败：" + e.Message);
            }
        }
    }
}