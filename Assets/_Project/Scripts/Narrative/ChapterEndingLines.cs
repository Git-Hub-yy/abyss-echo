using System.Collections.Generic;

namespace AbyssEcho.Narrative
{
    /// <summary>
    /// 各章节的结局文案。
    ///
    /// 为什么单独抽出来：
    ///   ChapterEndingSystem 是播放器（负责淡入、逐行、标题卡），
    ///   文案是数据。分开后加新章节只需要在这里加一段，不动播放逻辑。
    ///
    /// 写作原则（与第一章一致）：
    ///   · 不解释全部，留悬念
    ///   · 每句短，适合黑屏逐行显示
    ///   · 最后一句留钩子，指向下一章
    /// </summary>
    public static class ChapterEndingLines
    {
        public class EndingDef
        {
            public string[] Lines;
            public string ChapterTitle;
            public string Footer;
        }

        /// <summary>获取指定章节的结局文案。</summary>
        public static EndingDef Get(AbyssEcho.LevelGen.ChapterLayout.Chapter chapter)
        {
            switch (chapter)
            {
                case AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter5:
                    return Chapter5;

                case AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter4:
                    return Chapter4;

                case AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter2:
                    return Chapter2;

                case AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter1:
                default:
                    return Chapter1;
            }
        }

        /// <summary>
        /// 获取指定章节的指定结局分支。
        ///
        /// ⚠️ 目前只有第三章有三分支（A/B/C），
        ///   第一、二章忽略 endingId 直接返回各自结局，保证向后兼容。
        /// </summary>
        public static EndingDef Get(AbyssEcho.LevelGen.ChapterLayout.Chapter chapter, string endingId)
        {
            if (chapter == AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter5)
            {
                switch (endingId)
                {
                    case "A": return Chapter5_EndingA;
                    case "C": return Chapter5_EndingC;
                    case "B":
                    default: return Chapter5;
                }
            }

            if (chapter != AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter3)
                return Get(chapter);

            switch (endingId)
            {
                case "A": return Chapter3_EndingA;
                case "C": return Chapter3_EndingC;
                case "D": return Chapter3_EndingD;   // S65：灯泡 → 照亮
                case "B":
                default: return Chapter3_EndingB;
            }
        }

        // ────────────────────────── 第一章 ──────────────────────────

        public static readonly EndingDef Chapter1 = new EndingDef
        {
            ChapterTitle = "第一章 · 深潜",
            Footer = "未完待续",
            Lines = new[]
            {
                "你按下了广播键。",
                "频道里只有海水的压力声。",
                "然后有人回话了——",
                "「这里已经十九年了。」",
                "「你为什么才来？」"
            }
        };

        // ────────────────────────── 第二章 ──────────────────────────

        public static readonly EndingDef Chapter2 = new EndingDef
        {
            ChapterTitle = "第二章 · 回声",
            Footer = "未完待续",
            Lines = new[]
            {
                "观景台外没有水。",
                "只有一片更深的地方——",
                "下面亮着灯。",
                "整整齐齐，像有人住。",
                "「你听到了吗？」",
                "「他们还在下面。」"
            }
        };

        // ══════════════════════ 第三章 · 三个结局 ══════════════════════
        //
        //  A · 回声      —— 供奉录音带：你被接受，成为新的"下面的人"
        //  B · 离开      —— 什么都不供奉：你转身往回走
        //  C · 深入      —— 供奉保险丝：灯全亮，露出更深的通道 → 第四章

        // ────────────────────────── 第三章 · 结局 A（回声）──────────────────────────

        public static readonly EndingDef Chapter3_EndingA = new EndingDef
        {
            ChapterTitle = "第三章 · 镜沼",
            Footer = "结局：回声",
            Lines = new[]
            {
                "你把录音带放在石台上。",
                "水面平静下来。",
                "然后你听见自己的声音——",
                "从水里，慢慢地，一句一句浮上来。",
                "「这里已经十九年了。」",
                "「你为什么才来？」",
                "这一次你终于听懂了。",
                "这句话不是问你。",
                "是上一句回声，在问下一个人。",
                "而你现在，也是回声了。"
            }
        };

        // ────────────────────────── 第三章 · 结局 B（离开）──────────────────────────

        public static readonly EndingDef Chapter3_EndingB = new EndingDef
        {
            ChapterTitle = "第三章 · 镜沼",
            Footer = "结局：离开",
            Lines = new[]
            {
                "你什么也没有放。",
                "你转身往回走。",
                "天井的灯一盏一盏熄灭。",
                "身后没有脚步声。",
                "但你知道它一直在那里。",
                "你不知道自己还能不能上去。",
                "也不知道——",
                "上面那个地方，还算不算「上面」。"
            }
        };

        // ────────────────────────── 第三章 · 结局 C（深入）──────────────────────────

        // ────────────────────────── 第四章 ──────────────────────────

        public static readonly EndingDef Chapter4 = new EndingDef
        {
            ChapterTitle = "第四章 · 心腔",
            Footer = "未完待续",
            Lines = new[]
            {
                "核心在你面前慢慢打开。",
                "里面不是机械，也不是血肉。",
                "是一间办公室。",
                "桌上有你的工牌。",
                "名字对得上。",
                "入职日期是十九年前。",
                "照片上的人——",
                "是你。"
            }
        };

        // ────────────────────────── 第五章（终章）──────────────────────────
        //
        //  ⚠️ 第一版到此结束，但**不把故事讲死**：
        //    结尾留一个明显的悬念钩子（舱门外的声音），
        //    为后续版本接第六章留口子。

        public static readonly EndingDef Chapter5 = new EndingDef
        {
            ChapterTitle = "第五章 · 出口",
            Footer = "第一版 · 完",
            Lines = new[]
            {
                "舱门开了。",
                "外面很亮。",
                "你走出去，脚下是干燥的地面。",
                "空气有温度。",
                "你回头看了一眼。",
                "那道门还在，但门里已经没有走廊了——",
                "只有水。",
                "很远的地方，有人在叫你的名字。",
                "用的是你自己的声音。"
            }
        };

        // ────────────────────────── 第五章 · 结局 A（独行）──────────────────────────

        public static readonly EndingDef Chapter5_EndingA = new EndingDef
        {
            ChapterTitle = "第五章 · 出口",
            Footer = "结局 A · 独行 —— 第一版 · 完",
            Lines = new[]
            {
                "你选了第一项：只带自己走。",
                "舱门开了。",
                "外面很亮，干燥，有温度。",
                "你一个人走出去。",
                "门在你身后合上，没有声音。",
                "你没有回头。",
                "但你知道，如果你回头——",
                "水还会在那里。",
                "而它还在等下一个。",
                "",
                "（你什么都没带出来。）"
            }
        };

        // ────────────────────────── 第五章 · 结局 C（封门）──────────────────────────

        public static readonly EndingDef Chapter5_EndingC = new EndingDef
        {
            ChapterTitle = "第五章 · 出口",
            Footer = "结局 C · 封门 —— 第一版 · 完",
            Lines = new[]
            {
                "你选了第三项：留下，关上它。",
                "控制台问你确认吗。",
                "你按下了确认。",
                "出口舱门慢慢合上，然后焊死。",
                "你听见外面有人在叫你的名字。",
                "用的是你自己的声音。",
                "你没有回答。",
                "因为你终于想明白了——",
                "工牌上那个日期，不是入职日期。",
                "是上一次你下来的时候。",
                "",
                "（你把入口封死了。这次不会再有人被叫下来。）"
            }
        };

        // ────────────────────────── 第三章 · 结局 C（深入）──────────────────────────

        public static readonly EndingDef Chapter3_EndingC = new EndingDef
        {
            ChapterTitle = "第三章 · 镜沼",
            Footer = "结局：深入",
            Lines = new[]
            {
                "你把保险丝放在石台上。",
                "灯全部亮了。",
                "整整齐齐，一排一排，亮到看不见尽头。",
                "镜沼的水退下去。",
                "露出底下的通道。",
                "还有更下面。",
                "「你终于肯往下走了。」",
                "那个声音说。",
                "这次它用的是你的声音。"
            }
        };

        // ────────────────────────── 第三章 · 结局 D（照亮）──────────────────────────
        //
        //   S65 新增。
        //
        //   来源：玩家捡到「灯泡」(F4 工作间) 后无处可用 —— 是个死道具。
        //        祭室里本来就有两个空的长明灯座，正好接上。
        //
        //   与其余三个结局的区别：
        //     A 回声   —— 献出录音带，成为循环的一部分
        //     B 离开   —— 什么都不做，退回去
        //     C 深入   —— 修好保险丝，往下走
        //     D 照亮   —— 点亮长明灯，**看清这里本来的样子**
        //
        //   核心意象：灯泡给的不是出路，是「看见」。
        //     光一进宫，镜沼第一次不反射 —— 因为它终于有东西可照。

        public static readonly EndingDef Chapter3_EndingD = new EndingDef
        {
            ChapterTitle = "第三章 · 镜沼",
            Footer = "结局：照亮",
            Lines = new[]
            {
                "你把灯泡拧进长明灯座。",
                "它先是抖了一下。",
                "然后亮了。",
                "十九年来第一次，镜沼有了不属于自己的光。",
                "水面不动了。",
                "不是变平静——是终于不用再映别的东西。",
                "你低头看下去。",
                "水底没有通道。",
                "没有尸体。",
                "只有一间很小的房间，一张桌子，一把椅子。",
                "桌上摊着一本记录，最后一页写着：",
                "「他今天问，为什么灯一直是坏的。」",
                "「我说，因为有人还需要在黑里待一会儿。」"
            }
        };
    }
}
