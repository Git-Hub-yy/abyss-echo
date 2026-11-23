using System;
using System.Collections.Generic;
using UnityEngine;

namespace AbyssEcho.LevelGen
{
    /// <summary>
    /// 章节布局的统一入口：根据"当前章节"返回对应房间列表。
    ///
    /// 为什么需要这一层：
    ///   在 S10 之前，各 Builder（灯光、道具、交互物）都直接写死
    ///   `ChapterOneLayout.Build()`。要加第二章，就得逐个文件改，
    ///   而且改漏一处就会出现"用第一章坐标布置第二章房间"的严重错位。
    ///
    ///   现在所有 Builder 统一调用 `ChapterLayout.GetRooms()`，
    ///   新增章节只需要：① 写一个 XxxLayout ② 在这里注册。
    ///   切换章节只需改 `Current`，所有系统自动跟随。
    ///
    /// ⚠️ 兼容性：
    ///   默认值仍是 Chapter1，且 GetRooms() 在未注册时回退到第一章，
    ///   因此**不改变任何现有行为**。
    /// </summary>
    public static class ChapterLayout
    {
        /// <summary>支持的章节。</summary>
        public enum Chapter
        {
            Chapter1 = 1,
            Chapter2 = 2,
            Chapter3 = 3,
            Chapter4 = 4,
            Chapter5 = 5,
        }

        private const string PrefsKey = "game.currentChapter";

        private static Chapter _current = Chapter.Chapter1;
        private static bool _loaded;

        /// <summary>当前章节。修改后会持久化到 PlayerPrefs。</summary>
        public static Chapter Current
        {
            get
            {
                if (!_loaded) Load();
                return _current;
            }
            set
            {
                if (_current == value) return;
                _current = value;
                _loaded = true;
                Save();
                Debug.Log($"[ChapterLayout] 当前章节切换为 {value}");
            }
        }

        /// <summary>章节中文名（用于 UI 显示）。</summary>
        public static string GetChapterName(Chapter c)
        {
            switch (c)
            {
                case Chapter.Chapter1: return "第一章 · 深潜";
                case Chapter.Chapter3: return "第三章 · 镜沼";
                case Chapter.Chapter4: return "第四章 · 心腔";
                case Chapter.Chapter5: return "第五章 · 出口";
                case Chapter.Chapter2: return "第二章 · 回声";
                default: return "未知章节";
            }
        }

        /// <summary>章节场景名（用于场景加载）。</summary>
        public static string GetSceneName(Chapter c)
        {
            switch (c)
            {
                case Chapter.Chapter1: return "Level_Chapter1_Graybox";
                case Chapter.Chapter3: return "Level_Chapter3_Graybox";
                case Chapter.Chapter4: return "Level_Chapter4_Graybox";
                case Chapter.Chapter5: return "Level_Chapter5_Graybox";
                case Chapter.Chapter2: return "Level_Chapter2_Graybox";
                default: return "Level_Chapter1_Graybox";
            }
        }

        /// <summary>
        /// 获取当前章节的房间布局。
        /// 这是所有 Builder 应该调用的唯一入口。
        /// </summary>
        public static List<RoomDef> GetRooms()
        {
            return GetRooms(Current);
        }

        /// <summary>获取指定章节的房间布局。</summary>
        public static List<RoomDef> GetRooms(Chapter c)
        {
            switch (c)
            {
                case Chapter.Chapter5:
                    return ChapterFiveLayout.Build();

                case Chapter.Chapter4:
                    return ChapterFourLayout.Build();

                case Chapter.Chapter3:
                    return ChapterThreeLayout.Build();

                case Chapter.Chapter2:
                    return ChapterTwoLayout.Build();

                case Chapter.Chapter1:
                default:
                    return ChapterOneLayout.Build();
            }
        }

        /// <summary>当前章节的玩家起点。</summary>
        public static Vector3 GetPlayerStart()
        {
            return GetPlayerStart(Current);
        }

        public static Vector3 GetPlayerStart(Chapter c)
        {
            switch (c)
            {
                case Chapter.Chapter5:
                    return ChapterFiveLayout.PlayerStart;

                case Chapter.Chapter4:
                    return ChapterFourLayout.PlayerStart;

                case Chapter.Chapter3:
                    return ChapterThreeLayout.PlayerStart;

                case Chapter.Chapter2:
                    return ChapterTwoLayout.PlayerStart;
                default:
                    return ChapterOneLayout.PlayerStart;
            }
        }

        /// <summary>当前章节的终点。</summary>
        public static Vector3 GetChapterEndPoint()
        {
            return GetChapterEndPoint(Current);
        }

        public static Vector3 GetChapterEndPoint(Chapter c)
        {
            switch (c)
            {
                case Chapter.Chapter5:
                    return ChapterFiveLayout.ChapterEndPoint;

                case Chapter.Chapter4:
                    return ChapterFourLayout.ChapterEndPoint;

                case Chapter.Chapter3:
                    return ChapterThreeLayout.ChapterEndPoint;

                case Chapter.Chapter2:
                    return ChapterTwoLayout.ChapterEndPoint;
                default:
                    return ChapterOneLayout.ChapterEndPoint;
            }
        }

        // ────────────────────────── 持久化 ──────────────────────────

        private static void Load()
        {
            _loaded = true;
            int v = PlayerPrefs.GetInt(PrefsKey, 1);

            // ══════════════════ S53 修复：第三章之后的章节存不住 ══════════════════
            //
            //   原代码：_current = v == 2 ? Chapter.Chapter2 : Chapter.Chapter1;
            //
            //   只认 1 和 2。存进去的 3/4/5 读出来全变成 Chapter1。
            //   后果：
            //     · ChapterEndingSystem.GoToNextChapter() 把 Current 设成 Chapter4，
            //       调用 Save() 存了 4；但下一次 Load() 读到 4 → 判定为"不是2" → 变回 Chapter1
            //     · 场景加载后 ChapterEndingSystem.Awake() 读到的章节是错的，
            //       于是装载了第一章的结局文案与终点触发区
            //     · 玩家走完第三章却触发了"第一章·深潜"结局，或干脆哪里都不触发
            //
            //   修复：按枚举值范围安全解析（1~5 都认，越界回退到第一章）。
            if (v >= (int)Chapter.Chapter1 && v <= (int)Chapter.Chapter5)
                _current = (Chapter)v;
            else
                _current = Chapter.Chapter1;
        }

        private static void Save()
        {
            PlayerPrefs.SetInt(PrefsKey, (int)_current);
            PlayerPrefs.Save();
        }

        /// <summary>重置为第一章（新游戏时调用）。</summary>
        public static void ResetToFirst()
        {
            Current = Chapter.Chapter1;
        }
    }
}