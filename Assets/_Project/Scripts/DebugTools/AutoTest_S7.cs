using System.Collections;
using System.IO;
using System.Text;
using AbyssEcho.Inventory;
using AbyssEcho.Save;
using AbyssEcho.UI;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S7 自动测试：物品栏 UI、暂停菜单、存档系统。
    /// </summary>
    public class AutoTest_S7 : MonoBehaviour
    {
        public bool runOnStart = true;

        private int _pass, _fail;
        private readonly StringBuilder _log = new StringBuilder();

        private void Start()
        {
            if (runOnStart) StartCoroutine(RunAll());
        }

        private IEnumerator RunAll()
        {
            _log.AppendLine("ABYSS ECHO · S7 UI 与存档自动测试");
            _log.AppendLine("=====================================");

            yield return new WaitForSeconds(0.5f);

            T54_InventoryUIExists();
            T55_PauseMenuExists();
            T56_AutoSaveExists();
            T57_ItemDisplayNames();
            T58_InventoryToggle();
            yield return new WaitForSeconds(0.1f);
            T59_PauseToggle();
            yield return new WaitForSeconds(0.1f);
            T60_SaveWritesFile();
            T61_SaveRoundTrip();
            T62_SaveDelete();

            _log.AppendLine("=====================================");
            _log.AppendLine($"结果：通过 {_pass}，失败 {_fail}");

            string text = _log.ToString();
            Debug.Log("<b>[AutoTest_S7]</b>\n" + text);
            WriteReport(text);

            yield return new WaitForSeconds(0.2f);
            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }

        private void T54_InventoryUIExists()
        {
            var ui = FindFirstObjectByType<InventoryUI>();
            Check("T54 物品栏 UI 存在", ui != null);
        }

        private void T55_PauseMenuExists()
        {
            var ui = FindFirstObjectByType<PauseMenuUI>();
            Check("T55 暂停菜单存在", ui != null);
        }

        private void T56_AutoSaveExists()
        {
            var s = FindFirstObjectByType<AutoSaveManager>();
            Check("T56 自动存档存在", s != null);
        }

        private void T57_ItemDisplayNames()
        {
            string fuse = ItemIds.GetDisplayName(ItemIds.Fuse);
            string card = ItemIds.GetDisplayName(ItemIds.AccessCard);
            string note = ItemIds.GetDisplayName(ItemIds.Note);
            bool ok = fuse == "保险丝" && card == "门禁卡" && note == "笔记";

            string desc = ItemIds.GetDescription(ItemIds.Fuse);
            bool hasDesc = !string.IsNullOrEmpty(desc);

            Check("T57 物品显示名与描述", ok && hasDesc,
                  $"{fuse}/{card}/{note}  描述长度={desc.Length}");
        }

        private void T58_InventoryToggle()
        {
            var ui = FindFirstObjectByType<InventoryUI>();
            if (ui == null) { Fail("T58 物品栏可开关", "无 UI"); return; }

            bool before = ui.IsOpen;
            ui.Open();
            bool opened = ui.IsOpen;
            ui.Close();
            bool closed = !ui.IsOpen;

            Check("T58 物品栏可开关", opened && closed, $"初始={before} 打开={opened} 关闭={closed}");
        }

        private void T59_PauseToggle()
        {
            var ui = FindFirstObjectByType<PauseMenuUI>();
            if (ui == null) { Fail("T59 暂停菜单可开关", "无 UI"); return; }

            ui.Open();
            bool opened = ui.IsOpen;
            ui.Close();
            bool closed = !ui.IsOpen;

            Check("T59 暂停菜单可开关", opened && closed, $"打开={opened} 关闭={closed}");
        }

        private void T60_SaveWritesFile()
        {
            // 清理旧存档
            SaveSystem.Delete();

            string msg;
            bool ok = SaveSystem.Save(out msg);
            bool fileExists = File.Exists(SaveSystem.SavePath);

            Check("T60 存档写入文件", ok && fileExists,
                  $"路径={SaveSystem.SavePath}");
        }

        private void T61_SaveRoundTrip()
        {
            var data = SaveSystem.Load();
            if (data == null) { Fail("T61 存档可读回", "读取失败"); return; }

            bool ok = !string.IsNullOrEmpty(data.savedAt) && data.version == "1.0";
            Check("T61 存档可读回", ok,
                  $"版本={data.version} 时间={data.savedAt} 位置=({data.posX:F1},{data.posZ:F1})");
        }

        private void T62_SaveDelete()
        {
            SaveSystem.Delete();
            bool gone = !File.Exists(SaveSystem.SavePath);
            Check("T62 存档可删除", gone);
        }

        // ────────────────────────── 工具 ──────────────────────────

        private void Check(string name, bool ok, string detail = "")
        {
            if (ok) { _pass++; _log.AppendLine($"[通过] {name} {detail}"); }
            else { _fail++; _log.AppendLine($"[失败] {name} {detail}"); }
        }

        private void Fail(string name, string detail)
        {
            _fail++;
            _log.AppendLine($"[失败] {name} {detail}");
        }

        private static void WriteReport(string text)
        {
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "S7_AutoTestResult.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}