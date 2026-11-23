using System;
using System.Collections.Generic;
using UnityEngine;

namespace AbyssEcho.Narrative
{
    /// <summary>
    /// 日志系统：记录玩家读到的线索、完成的事件。
    ///
    /// 第一章用它在"已读线索"列表里保存门禁密码，
    /// 玩家忘记时可按 J 打开日志查看。
    /// </summary>
    public class JournalSystem : MonoBehaviour
    {
        public static JournalSystem Instance { get; private set; }

        [Serializable]
        public class Entry
        {
            public string Title;
            public string Body;
            public float Time;
        }

        private readonly List<Entry> _entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => _entries;

        /// <summary>有新条目时触发。</summary>
        public event Action OnEntriesChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void AddEntry(string title, string body)
        {
            if (string.IsNullOrEmpty(title)) return;

            // 去重：同标题只保留一条（首次读到的内容为准）
            foreach (var e in _entries)
                if (e.Title == title) return;

            _entries.Add(new Entry { Title = title, Body = body, Time = Time.time });
            OnEntriesChanged?.Invoke();
            Debug.Log($"[Journal] 记录：{title}");
        }

        /// <summary>是否已记录某个条目。</summary>
        public bool Has(string title)
        {
            foreach (var e in _entries) if (e.Title == title) return true;
            return false;
        }
    }
}