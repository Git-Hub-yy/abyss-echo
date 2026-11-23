using AbyssEcho.Interaction;
using UnityEngine;

namespace AbyssEcho.Narrative
{
    /// <summary>
    /// 可阅读的线索笔记。
    ///
    /// 交互后把内容推给 NarrativeUI 显示，同时记录到日志（Journal）系统。
    /// 第一章用于：广播室里的一张纸条，写着门禁密码。
    /// </summary>
    public class ReadableNote : InteractableBase
    {
        [Header("内容")]
        [Tooltip("标题")]
        public string title = "手写便条";

        [Tooltip("正文（支持换行 \\n）")]
        [TextArea(4, 12)]
        public string body = "……";

        [Tooltip("是否同时记录到日志")]
        public bool addToJournal = true;

        [Tooltip("记录到日志时的条目标题")]
        public string journalTitle = "门禁密码";

        /// <summary>被阅读时触发（供任务/音效订阅）。</summary>
        public static event System.Action<string> OnNoteRead;

        protected override string DefaultPrompt => "阅读 " + title;

        public override void OnInteract(GameObject interactor)
        {
            if (!CanInteract)
            {
                Debug.LogWarning($"[ReadableNote] {name} 不可交互（CanInteract=false），已忽略");
                return;
            }

            // S22：加日志，方便玩家/开发者确认"按 E 到底有没有触发"
            Debug.Log($"[ReadableNote] 打开便条：{title}");

            var ui = FindFirstObjectByType<AbyssEcho.UI.NarrativeUI>();
            if (ui != null)
            {
                ui.Show(title, body);
            }
            else
            {
                Debug.LogWarning("[ReadableNote] 场景中没有 NarrativeUI，直接在控制台输出：\n" + title + "\n" + body);
            }

            if (addToJournal)
            {
                var journal = FindFirstObjectByType<AbyssEcho.Narrative.JournalSystem>();
                if (journal != null) journal.AddEntry(journalTitle, body);
            }

            OnNoteRead?.Invoke(title);
        }
    }
}