using UnityEngine;

namespace AbyssEcho.UI
{
    /// <summary>
    /// 中文字体工具：为 OnGUI 提供能显示中文的 GUIStyle。
    ///
    /// 为什么需要这个：
    ///   Unity 内置的 GUI 字体（Arial/Roboto）**不包含中文字形**，
    ///   直接用 GUI.skin.label 显示中文会全部变成空白/方块。
    ///
    /// 解决方案：
    ///   · 从系统字体目录加载中文字体（Windows 自带微软雅黑）
    ///   · 用 Font.CreateDynamicFontFromOSFont 创建动态字体
    ///   · 缓存在静态变量里，所有 UI 共享一个实例
    ///
    /// 降级策略：
    ///   如果系统里找不到任何中文字体，回退到内置字体并记录警告
    ///   （此时中文会显示为空白，但至少不会崩溃）。
    /// </summary>
    public static class ChineseFont
    {
        private static Font _font;
        private static bool _tried;

        /// <summary>按优先级尝试的系统中文字体名。</summary>
        private static readonly string[] PreferredFonts =
        {
            "Microsoft YaHei UI",
            "Microsoft YaHei",
            "微软雅黑",
            "SimHei",
            "黑体",
            "SimSun",
            "宋体",
            "Noto Sans CJK SC",
            "Source Han Sans SC",
            "WenQuanYi Micro Hei",
            "Arial Unicode MS",
        };

        /// <summary>获取可显示中文的字体（首次调用时加载）。</summary>
        public static Font Get()
        {
            if (_tried) return _font;
            _tried = true;

            try
            {
                // ① 优先用 OS 字体列表精确匹配
                var osFonts = Font.GetOSInstalledFontNames();
                if (osFonts != null && osFonts.Length > 0)
                {
                    foreach (var wanted in PreferredFonts)
                    {
                        foreach (var installed in osFonts)
                        {
                            if (string.IsNullOrEmpty(installed)) continue;
                            if (installed.Equals(wanted, System.StringComparison.OrdinalIgnoreCase) ||
                                installed.Contains(wanted))
                            {
                                var f = Font.CreateDynamicFontFromOSFont(installed, 32);
                                if (f != null)
                                {
                                    _font = f;
                                    Debug.Log($"[ChineseFont] 已加载系统中文字体：{installed}");
                                    return _font;
                                }
                            }
                        }
                    }
                }

                // ② 退化：让 Unity 按名称列表选第一个可用的
                var fallback = Font.CreateDynamicFontFromOSFont(PreferredFonts, 32);
                if (fallback != null)
                {
                    _font = fallback;
                    Debug.Log($"[ChineseFont] 使用字体列表创建：{fallback.name}");
                    return _font;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[ChineseFont] 加载系统字体失败：" + e.Message);
            }

            Debug.LogWarning("[ChineseFont] 未找到中文字体，中文将无法显示");
            _font = null;
            return null;
        }

        /// <summary>创建一个使用中文字体的 GUIStyle。</summary>
        public static GUIStyle CreateStyle(int fontSize, FontStyle fontStyle = FontStyle.Normal,
                                           TextAnchor anchor = TextAnchor.MiddleLeft, bool wrap = false)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                fontStyle = fontStyle,
                alignment = anchor,
                wordWrap = wrap
            };

            var f = Get();
            if (f != null) style.font = f;

            return style;
        }

        /// <summary>给已有样式套上中文字体。</summary>
        public static void Apply(GUIStyle style)
        {
            if (style == null) return;
            var f = Get();
            if (f != null) style.font = f;
        }
    }
}