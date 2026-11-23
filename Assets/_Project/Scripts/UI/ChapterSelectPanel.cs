using System.Collections.Generic;
using System.Text;

namespace AbyssEcho.UI
{
    /// <summary>
    /// 主菜单的章节选择功能（作为 MainMenuUI 的辅助类，保持主文件不变太大）。
    ///
    /// 设计：
    ///   · 主菜单显示"第一章 / 第二章"两个按钮
    ///   · 点击后设置 ChapterLayout.Current，再进入关卡
    ///   · 第二章若尚未完成会显示提示（但不阻止进入，方便调试）
    /// </summary>
    public class ChapterSelectPanel
    {
        public List<AbyssEcho.LevelGen.ChapterLayout.Chapter> Chapters
            = new List<AbyssEcho.LevelGen.ChapterLayout.Chapter>
            {
                AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter1,
                AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter2,
                AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter3,
                AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter4,
                AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter5,
            };

        public AbyssEcho.LevelGen.ChapterLayout.Chapter Selected { get; private set; }
            = AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter1;

        public ChapterSelectPanel()
        {
            Selected = AbyssEcho.LevelGen.ChapterLayout.Current;
        }

        public void Select(AbyssEcho.LevelGen.ChapterLayout.Chapter c)
        {
            Selected = c;
        }

        /// <summary>把选择写入全局状态。</summary>
        public void Apply()
        {
            AbyssEcho.LevelGen.ChapterLayout.Current = Selected;
        }

        public string GetName(AbyssEcho.LevelGen.ChapterLayout.Chapter c)
        {
            return AbyssEcho.LevelGen.ChapterLayout.GetChapterName(c);
        }

        /// <summary>
        /// 检查章节是否"可玩"。
        /// 目前两章都已实现，这里保留接口以便未来章节逐步开放。
        /// </summary>
        public bool IsPlayable(AbyssEcho.LevelGen.ChapterLayout.Chapter c)
        {
            switch (c)
            {
                case AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter1: return true;
                case AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter2: return true;
                case AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter3: return true;
                case AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter4: return true;
                case AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter5: return true;
                default: return false;
            }
        }
    }
}