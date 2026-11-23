using System.Collections.Generic;
using System.Linq;

namespace AbyssEcho.LevelGen
{
    /// <summary>
    /// 章节相关的布局元数据。
    ///
    /// 为什么需要：
    ///   LayoutValidator 里原本硬编码了第一章的起点 "A0" 和关键路径房间表，
    ///   导致校验第二章时全部报"不可达"。
    ///   抽出这个类后，校验器按当前章节取对应数据。
    /// </summary>
    public static class ChapterLayoutMeta
    {
        /// <summary>当前章节的起点房间 ID。</summary>
        public static string GetStartRoomId()
        {
            return GetStartRoomId(ChapterLayout.Current);
        }

        public static string GetStartRoomId(ChapterLayout.Chapter c)
        {
            switch (c)
            {
                case ChapterLayout.Chapter.Chapter5: return "H1";
                case ChapterLayout.Chapter.Chapter4: return "G1";
                case ChapterLayout.Chapter.Chapter3: return "F1";
                case ChapterLayout.Chapter.Chapter2: return "D1";
                case ChapterLayout.Chapter.Chapter1:
                default: return "A0";
            }
        }

        /// <summary>当前章节必须可达的关键房间（用于流程卡关检测）。</summary>
        public static string[] GetCriticalPath()
        {
            return GetCriticalPath(ChapterLayout.Current);
        }

        public static string[] GetCriticalPath(ChapterLayout.Chapter c)
        {
            switch (c)
            {
                case ChapterLayout.Chapter.Chapter5:
                    return new[] { "H1", "H2", "H3", "H6", "H7" };

                case ChapterLayout.Chapter.Chapter4:
                    return new[] { "G1", "G2", "G5", "G7" };

                case ChapterLayout.Chapter.Chapter3:
                    return new[] { "F1", "F2", "F6", "F15", "F7" };

                case ChapterLayout.Chapter.Chapter2:
                    // 第二章主干：D1 → D2 → D5 → E1 → E4
                    // D3/D4 是支线（抽水泵、线索），不算关键路径，但也不应不可达
                    return new[]
                    {
                        "D1", "L1", "D2", "L2", "D5", "L3", "E1", "L4", "E4"
                    };

                case ChapterLayout.Chapter.Chapter1:
                default:
                    return new[]
                    {
                        "A0", "A1", "A2", "A3", "B1", "B2", "B3", "ELEV",
                        "C1", "C2", "C3", "C5", "C1_D_Gate"
                    };
            }
        }

        /// <summary>当前章节名（用于报告标题）。</summary>
        public static string GetChapterLabel()
        {
            return ChapterLayout.GetChapterName(ChapterLayout.Current);
        }
    }
}